#!/usr/bin/env bash
# Ejecuta los scripts SQL dentro del contenedor commerce-sql (docker compose up -d antes).
set -euo pipefail
cd "$(dirname "$0")/.."
source .env
for f in 01_schema 02_sp_create_commerce 03_sp_process_commerce; do
  echo ">> $f.sql"
  docker exec commerce-sql /opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -i /scripts/$f.sql
done
echo "Base de datos lista."
