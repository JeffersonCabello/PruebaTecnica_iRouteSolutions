USE CommerceDb;
GO
/* =====================================================================
   sp_create_commerce
   Recibe todo el CSV como un arreglo JSON (@rows_json) donde cada objeto
   tiene como claves las columnas del archivo, y:
     1) crea dbo.commerce y dbo.commerce_quarantine si no existen, con las
        columnas del CSV (estructura igual al archivo);
     2) agrega a ambas las columnas nuevas que traiga un CSV posterior;
     3) inserta todas las filas con un unico INSERT ... SELECT FROM OPENJSON.
   Todo en una transaccion. OUTPUT: @inserted_rows = filas insertadas.
   Los nombres de columna se validan (solo [A-Za-z0-9_]) y se escapan con
   QUOTENAME, por lo que el SQL dinamico no es inyectable.
   Errores de negocio: THROW 50001..50003 (el API los traduce a HTTP 400).
   ===================================================================== */
CREATE OR ALTER PROCEDURE dbo.sp_create_commerce
    @rows_json     NVARCHAR(MAX),
    @inserted_rows INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;   -- cualquier error revierte la carga completa

    IF ISJSON(@rows_json) <> 1 OR JSON_QUERY(@rows_json, '$[0]') IS NULL
        THROW 50001, N'El archivo no contiene registros.', 1;

    /* Columnas del CSV = claves del primer objeto (el API rellena todas las filas con las mismas) */
    SELECT [key] AS name, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS pos
    INTO #cols
    FROM OPENJSON(@rows_json, '$[0]');

    IF NOT EXISTS (SELECT 1 FROM #cols WHERE name = N'pc_processdate')
        THROW 50002, N'El archivo no contiene la columna pc_processdate.', 1;

    IF EXISTS (SELECT 1 FROM #cols
               WHERE name = N'' OR LEN(name) > 100 OR name LIKE N'%[^A-Za-z0-9_]%'
                  OR name IN (N'id', N'commerce_id', N'created_at', N'motivo'))
        THROW 50003, N'El archivo tiene nombres de columna no validos (solo letras, numeros y _; no se permiten id, commerce_id, created_at ni motivo).', 1;

    DECLARE @sql NVARCHAR(MAX), @cols NVARCHAR(MAX), @n INT;

    BEGIN TRANSACTION;

    /* Serializa los cambios de esquema entre cargas simultaneas */
    EXEC sp_getapplock @Resource = N'commerce_schema', @LockMode = N'Exclusive', @LockOwner = N'Transaction';

    SELECT @cols = STRING_AGG(CAST(QUOTENAME(name) +
                        CASE WHEN name = N'pc_processdate' THEN N' DATE NOT NULL' ELSE N' NVARCHAR(MAX) NULL' END
                        AS NVARCHAR(MAX)), N', ') WITHIN GROUP (ORDER BY pos)
    FROM #cols;

    /* 1) Tablas dinamicas: se crean solo la primera vez */
    IF OBJECT_ID(N'dbo.commerce', N'U') IS NULL
    BEGIN
        SET @sql = N'CREATE TABLE dbo.commerce (
            id BIGINT IDENTITY(1,1) CONSTRAINT PK_commerce PRIMARY KEY, ' + @cols + N',
            created_at DATETIME2(0) NOT NULL CONSTRAINT DF_commerce_created DEFAULT SYSUTCDATETIME());
            CREATE INDEX IX_commerce_processdate ON dbo.commerce (pc_processdate);';
        EXEC sp_executesql @sql;
    END

    IF OBJECT_ID(N'dbo.commerce_quarantine', N'U') IS NULL
    BEGIN
        SET @sql = N'CREATE TABLE dbo.commerce_quarantine (
            id BIGINT IDENTITY(1,1) CONSTRAINT PK_commerce_quarantine PRIMARY KEY,
            commerce_id BIGINT NOT NULL, ' + @cols + N',
            created_at DATETIME2(0) NOT NULL CONSTRAINT DF_commerce_q_created DEFAULT SYSUTCDATETIME());
            CREATE INDEX IX_commerce_quarantine_processdate ON dbo.commerce_quarantine (pc_processdate);';
        EXEC sp_executesql @sql;
    END

    /* Requisito: "modificar la tabla commerce_quarantine agregando la columna motivo" */
    IF COL_LENGTH(N'dbo.commerce_quarantine', N'motivo') IS NULL
        ALTER TABLE dbo.commerce_quarantine ADD motivo NVARCHAR(500) NOT NULL
            CONSTRAINT DF_commerce_q_motivo DEFAULT N'';

    /* 2) Columnas nuevas en CSV posteriores */
    SET @sql = NULL;
    SELECT @sql = STRING_AGG(CAST(N'ALTER TABLE dbo.commerce ADD ' + QUOTENAME(name) + N' NVARCHAR(MAX) NULL;' AS NVARCHAR(MAX)), N' ')
    FROM #cols WHERE COL_LENGTH(N'dbo.commerce', name) IS NULL;
    IF @sql IS NOT NULL EXEC sp_executesql @sql;

    SET @sql = NULL;
    SELECT @sql = STRING_AGG(CAST(N'ALTER TABLE dbo.commerce_quarantine ADD ' + QUOTENAME(name) + N' NVARCHAR(MAX) NULL;' AS NVARCHAR(MAX)), N' ')
    FROM #cols WHERE COL_LENGTH(N'dbo.commerce_quarantine', name) IS NULL;
    IF @sql IS NOT NULL EXEC sp_executesql @sql;

    /* 3) Insercion set-based de todas las filas */
    SELECT @sql = N'INSERT INTO dbo.commerce (' +
                  STRING_AGG(CAST(QUOTENAME(name) AS NVARCHAR(MAX)), N', ') WITHIN GROUP (ORDER BY pos) +
                  N') SELECT ' +
                  STRING_AGG(CAST(QUOTENAME(name) AS NVARCHAR(MAX)), N', ') WITHIN GROUP (ORDER BY pos) +
                  N' FROM OPENJSON(@json) WITH (' +
                  STRING_AGG(CAST(QUOTENAME(name) + CASE WHEN name = N'pc_processdate' THEN N' DATE' ELSE N' NVARCHAR(MAX)' END +
                             N' ''$.' + name + N'''' AS NVARCHAR(MAX)), N', ') WITHIN GROUP (ORDER BY pos) +
                  N'); SET @n = @@ROWCOUNT;'
    FROM #cols;

    EXEC sp_executesql @sql, N'@json NVARCHAR(MAX), @n INT OUTPUT', @json = @rows_json, @n = @n OUTPUT;

    COMMIT TRANSACTION;
    SET @inserted_rows = @n;
END
GO

/* Limpieza: el TVP fijo de la version anterior ya no se usa */
IF TYPE_ID(N'dbo.commerce_type') IS NOT NULL
    DROP TYPE dbo.commerce_type;
GO
