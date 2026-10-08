USE CommerceDb;
GO
/* =====================================================================
   sp_process_commerce
   Valida los registros de dbo.commerce de una fecha de proceso:
     1) pc_nomcomred no debe estar vacio.
     2) pc_numdoc no debe estar vacio, ni contener letras ni caracteres especiales
        (solo digitos 0-9).
   Los registros invalidos se mueven (INSERT + DELETE en una transaccion) a
   dbo.commerce_quarantine con el campo motivo. Un registro con varios
   problemas guarda todos los motivos separados por "; ".
   Las columnas copiadas a cuarentena son las de dbo.commerce (creadas desde el CSV).
   OUTPUT: @quarantined_rows = cantidad insertada en commerce_quarantine.
   ===================================================================== */
CREATE OR ALTER PROCEDURE dbo.sp_process_commerce
    @processdate      DATE,
    @quarantined_rows INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    /* Las tablas se crean al cargar el primer CSV (sp_create_commerce) */
    IF OBJECT_ID(N'dbo.commerce', N'U') IS NULL OR OBJECT_ID(N'dbo.commerce_quarantine', N'U') IS NULL
        THROW 50010, N'Aun no se ha cargado ningun archivo: las tablas commerce y commerce_quarantine no existen.', 1;
    IF COL_LENGTH(N'dbo.commerce', N'pc_nomcomred') IS NULL OR COL_LENGTH(N'dbo.commerce', N'pc_numdoc') IS NULL
        THROW 50011, N'La tabla commerce no tiene las columnas pc_nomcomred y pc_numdoc necesarias para validar.', 1;

    BEGIN TRANSACTION;

    /* Se evalua cada fila UNA vez y se materializa el motivo */
    SELECT c.id,
           motivo = STUFF(
               CONCAT(
                   -- Regla 1: nombre comercial
                   CASE WHEN LTRIM(RTRIM(ISNULL(c.pc_nomcomred, N''))) = N''
                        THEN N'; El nombre del comercio (nomcomred) se encuentra vacio' END,
                   -- Regla 2: numero de documento
                   CASE WHEN LTRIM(RTRIM(ISNULL(c.pc_numdoc, N''))) = N''
                        THEN N'; El número (numdoc) se encuentra vacio' END,
                   CASE WHEN t.v LIKE N'%[A-Za-z]%'
                        THEN N'; El número (numdoc) contiene letras' END,
                   CASE WHEN t.v LIKE N'%[^0-9A-Za-z]%'
                        THEN N'; El número (numdoc) contiene caracteres especiales' END
               ), 1, 2, N'')
    INTO #invalid
    FROM dbo.commerce AS c
    CROSS APPLY (SELECT v = LTRIM(RTRIM(ISNULL(c.pc_numdoc, N'')))) AS t
    WHERE c.pc_processdate = @processdate
      AND (   LTRIM(RTRIM(ISNULL(c.pc_nomcomred, N''))) = N''
           OR t.v = N''
           OR t.v LIKE N'%[^0-9]%');

    /* Las columnas de negocio dependen del CSV: se leen de dbo.commerce y se copian por SQL dinamico */
    DECLARE @cols NVARCHAR(MAX), @ccols NVARCHAR(MAX), @sql NVARCHAR(MAX), @n INT;

    SELECT @cols  = STRING_AGG(CAST(QUOTENAME(name) AS NVARCHAR(MAX)), N', ') WITHIN GROUP (ORDER BY column_id),
           @ccols = STRING_AGG(CAST(N'c.' + QUOTENAME(name) AS NVARCHAR(MAX)), N', ') WITHIN GROUP (ORDER BY column_id)
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.commerce') AND name NOT IN (N'id', N'created_at');

    SET @sql = N'INSERT INTO dbo.commerce_quarantine (commerce_id, ' + @cols + N', motivo)
                 SELECT c.id, ' + @ccols + N', i.motivo
                 FROM dbo.commerce AS c
                 JOIN #invalid     AS i ON i.id = c.id;
                 SET @n = @@ROWCOUNT;';
    EXEC sp_executesql @sql, N'@n INT OUTPUT', @n = @n OUTPUT;

    SET @quarantined_rows = @n;

    DELETE c
    FROM dbo.commerce AS c
    JOIN #invalid     AS i ON i.id = c.id;

    COMMIT TRANSACTION;
END
GO
