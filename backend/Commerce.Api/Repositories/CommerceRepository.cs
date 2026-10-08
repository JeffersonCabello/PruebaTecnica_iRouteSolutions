using System.Data;
using System.Text;
using System.Text.Json;
using Commerce.Api.Infrastructure;
using Commerce.Api.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Commerce.Api.Repositories;

/// <summary>Acceso a datos. Toda la logica de validacion vive en los stored procedures.</summary>
public interface ICommerceRepository
{
    Task<int> CreateAsync(CsvData csv, CancellationToken ct);
    Task<int> ProcessAsync(DateOnly processDate, CancellationToken ct);
    Task<QuarantinePage> GetQuarantineAsync(DateOnly? processDate, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<PendingDate>> GetPendingDatesAsync(CancellationToken ct);
}

public sealed class CommerceRepository(IConfiguration configuration) : ICommerceRepository
{
    private readonly string _connectionString =
        configuration.GetConnectionString("CommerceDb")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:CommerceDb");

    private SqlConnection NewConnection() => new(_connectionString);

    public async Task<int> CreateAsync(CsvData csv, CancellationToken ct)
    {
        // Todo el CSV viaja como un unico JSON -> una sola ida y vuelta; el SP crea las tablas si faltan.
        var p = new DynamicParameters();
        p.Add("@rows_json", ToJson(csv), DbType.String, size: -1);
        p.Add("@inserted_rows", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await using var conn = NewConnection();
        await ExecuteSpAsync(conn, "dbo.sp_create_commerce", p, ct);
        return p.Get<int>("@inserted_rows");
    }

    public async Task<int> ProcessAsync(DateOnly processDate, CancellationToken ct)
    {
        var p = new DynamicParameters();
        p.Add("@processdate", processDate.ToDateTime(TimeOnly.MinValue), DbType.Date);
        p.Add("@quarantined_rows", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await using var conn = NewConnection();
        await ExecuteSpAsync(conn, "dbo.sp_process_commerce", p, ct);
        return p.Get<int>("@quarantined_rows");
    }

    public async Task<QuarantinePage> GetQuarantineAsync(
        DateOnly? processDate, int page, int pageSize, CancellationToken ct)
    {
        await using var conn = NewConnection();

        // La tabla (y sus columnas) nacen del CSV: se leen del catalogo. Sin cargas aun -> pagina vacia.
        var columns = (await conn.QueryAsync<string>(new CommandDefinition("""
            SELECT name FROM sys.columns
            WHERE object_id = OBJECT_ID(N'dbo.commerce_quarantine') AND name NOT IN (N'id', N'commerce_id', N'created_at')
            ORDER BY CASE WHEN name = N'motivo' THEN 1 ELSE 0 END, column_id
            """, cancellationToken: ct))).AsList();
        if (columns.Count == 0)
            return new QuarantinePage([], [], 0, page, pageSize);

        // Los nombres vienen de sys.columns y se escapan igual que QUOTENAME.
        var select = string.Join(", ", columns.Select(c => $"[{c.Replace("]", "]]")}]"));
        var sql = $"""
            SELECT COUNT(*) FROM dbo.commerce_quarantine
            WHERE (@date IS NULL OR pc_processdate = @date);

            SELECT id, {select}
            FROM dbo.commerce_quarantine
            WHERE (@date IS NULL OR pc_processdate = @date)
            ORDER BY id
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
            """;

        await using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            date = processDate?.ToDateTime(TimeOnly.MinValue),
            offset = (page - 1) * pageSize,
            pageSize
        }, cancellationToken: ct));

        var total = await grid.ReadSingleAsync<int>();
        var items = (await grid.ReadAsync())
            .Select(row => (IDictionary<string, object?>)((IDictionary<string, object?>)row).ToDictionary(
                kv => kv.Key,
                kv => kv.Value is DateTime d ? d.ToString("yyyy-MM-dd") : kv.Value))
            .ToList();
        return new QuarantinePage(columns, items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<PendingDate>> GetPendingDatesAsync(CancellationToken ct)
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.commerce', N'U') IS NULL
                SELECT CAST(NULL AS DATE) AS ProcessDate, 0 AS Records WHERE 1 = 0;
            ELSE
                SELECT pc_processdate AS ProcessDate, COUNT(*) AS Records
                FROM dbo.commerce GROUP BY pc_processdate ORDER BY pc_processdate DESC;
            """;
        await using var conn = NewConnection();
        return (await conn.QueryAsync<PendingDate>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    /// <summary>Ejecuta un SP; los THROW 50xxx de negocio (rango 50000-50099) se devuelven como 400.</summary>
    private static async Task ExecuteSpAsync(SqlConnection conn, string name, DynamicParameters p, CancellationToken ct)
    {
        try
        {
            await conn.ExecuteAsync(new CommandDefinition(name, p,
                commandType: CommandType.StoredProcedure, cancellationToken: ct));
        }
        catch (SqlException ex) when (ex.Number is >= 50000 and < 50100)
        {
            throw new CsvValidationException(ex.Message);
        }
    }

    /// <summary>Arreglo JSON de objetos {columna: valor}; las claves salen del encabezado del CSV.</summary>
    private static string ToJson(CsvData csv)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartArray();
            foreach (var row in csv.Rows)
            {
                w.WriteStartObject();
                for (var i = 0; i < csv.Columns.Count; i++)
                    w.WriteString(csv.Columns[i], row[i]);
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }
        return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
    }
}
