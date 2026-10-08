/* =====================================================================
   01_schema.sql  -  Solo crea la base de datos.
   Las tablas dbo.commerce y dbo.commerce_quarantine NO se crean aqui:
   las crea dinamicamente sp_create_commerce con las columnas del CSV
   en la primera carga (y le agrega columnas si un CSV posterior trae nuevas).
   ===================================================================== */
IF DB_ID(N'CommerceDb') IS NULL
    CREATE DATABASE CommerceDb;
GO
