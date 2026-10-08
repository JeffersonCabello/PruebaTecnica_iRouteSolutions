# Prueba tecnica iRoute - Commerce

Angular 19 + .NET 8 (Web API) + SQL Server 2022 (Docker).

```
database/   scripts SQL (base de datos, sp_create_commerce, sp_process_commerce) + init.sh
backend/    Commerce.Api (.NET 8)
frontend/   commerce-web (Angular)
samples/    commerce_10052024.csv de ejemplo (con registros validos e invalidos)
```

## Ejecutar

Requisitos: Docker, .NET 8 SDK, Node 22. (Si estan instalados solo en tu usuario:
`export PATH=$HOME/.dotnet:$HOME/.local/node/bin:$PATH DOTNET_ROOT=$HOME/.dotnet`)

```bash
docker compose up -d          # SQL Server en localhost:1433 (password en .env)
./database/init.sh            # crea la BD y los stored procedures (idempotente); las tablas las crea la primera carga

cd backend/Commerce.Api && dotnet run --urls http://localhost:5026     # Swagger: /swagger
cd frontend/commerce-web && npm install && npx ng serve                # http://localhost:4200
```

El front usa un proxy (`proxy.conf.json`) hacia `/api`, asi que no hay problemas de CORS en desarrollo.

## API

| Metodo | Ruta | Descripcion |
|---|---|---|
| POST | `/api/commerce/upload` | multipart `file` = `commerce_DDMMYYYY.csv`. 400 si esta vacio, sin registros, nombre/fecha invalidos. Inserta con `sp_create_commerce`. |
| POST | `/api/commerce/process` | body `{"processDate":"2024-05-10"}`. Ejecuta `sp_process_commerce`; devuelve `quarantinedRows`. |
| GET | `/api/commerce/quarantine` | Lista `commerce_quarantine` (`?processDate=&page=&pageSize=`). |
| GET | `/api/commerce/process-dates` | Fechas cargadas con su cantidad de registros (para el selector). |

## Decisiones de diseno

- **Las tablas se crean dinamicamente al cargar el CSV**: `sp_create_commerce` crea `commerce` y `commerce_quarantine` con las columnas del encabezado (todas `NVARCHAR(MAX)`, salvo `pc_processdate` que es `DATE`) si no existen, y les agrega las columnas nuevas que traiga un CSV posterior. `commerce` acumula varios dias y el proceso filtra por `pc_processdate`. `motivo` se agrega a `commerce_quarantine` con un `ALTER`. Columnas obligatorias en el CSV: `pc_processdate`, `pc_nomcomred`, `pc_numdoc` (las usa la validacion). Nombres de columna: solo `[A-Za-z0-9_]` (y no `id`, `commerce_id`, `created_at`, `motivo`), validados en el API y en el SP, y escapados con `QUOTENAME`.
- **El CSV de ejemplo no venia en el enunciado**; `samples/commerce_10052024.csv` usa `pc_codcomercio, pc_nomcomred, pc_razonsocial, pc_tipodoc, pc_numdoc, pc_ciudad, pc_direccion, pc_telefono, pc_email` + `pc_processdate`, pero ya no hay que mantenerlas sincronizadas en codigo: la estructura sale del archivo.
- **Eficiencia:** todo el CSV viaja en *un* parametro JSON y `sp_create_commerce` hace un unico `INSERT...SELECT FROM OPENJSON`. La validacion es set-based (sin cursores) y el movimiento a cuarentena es `INSERT` + `DELETE` dentro de una transaccion (`XACT_ABORT ON`).
- **Reglas de validacion** (`sp_process_commerce`): `nomcomred` vacio; `numdoc` vacio, con letras o con caracteres especiales (solo se aceptan digitos). Si un registro incumple varias, `motivo` las concatena con "; ".
- **Capas en el API:** Controller (HTTP) -> `ICommerceCsvParser` (CSV) -> `ICommerceRepository` (Dapper/SP). Errores centralizados en `ApiExceptionHandler` (ProblemDetails).
- **Front:** componentes standalone con carga perezosa, signals, servicio de API unico, interceptor de errores y vista previa/validacion del CSV en el cliente antes de enviarlo.
- **Limitacion conocida:** cargar dos veces el mismo archivo duplica los registros (no es parte del requisito).
