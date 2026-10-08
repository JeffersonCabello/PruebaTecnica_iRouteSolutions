using System.Globalization;
using System.Text.RegularExpressions;
using Commerce.Api.Infrastructure;
using Commerce.Api.Models;
using CsvHelper;
using CsvHelper.Configuration;

namespace Commerce.Api.Services;

/// <summary>Valida el nombre del archivo y lo convierte en registros (streaming, sin cargar todo en texto).</summary>
public interface ICommerceCsvParser
{
    CsvData Parse(string fileName, Stream content);
}

public sealed partial class CommerceCsvParser : ICommerceCsvParser
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd/MM/yyyy", "ddMMyyyy", "yyyyMMdd", "dd-MM-yyyy"];

    // Columnas que necesita el proceso de validacion (sp_process_commerce)
    private static readonly string[] RequiredColumns = ["pc_processdate", "pc_nomcomred", "pc_numdoc"];

    // Los nombres del encabezado se convierten en columnas SQL: solo letras, numeros y _
    private static readonly string[] ReservedColumns = ["id", "commerce_id", "created_at", "motivo"];

    [GeneratedRegex(@"^[a-z_][a-z0-9_]{0,99}$")]
    private static partial Regex ColumnNameRegex();

    [GeneratedRegex(@"^commerce_(\d{8})\.csv$", RegexOptions.IgnoreCase)]
    private static partial Regex FileNameRegex();

    public CsvData Parse(string fileName, Stream content)
    {
        var match = FileNameRegex().Match(fileName);
        if (!match.Success ||
            !DateOnly.TryParseExact(match.Groups[1].Value, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new CsvValidationException("El archivo debe llamarse commerce_DDMMYYYY.csv con una fecha valida.");

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            DetectDelimiter = true,          // acepta "," o ";"
            TrimOptions = TrimOptions.Trim,
            PrepareHeaderForMatch = a => a.Header.Trim().ToLowerInvariant(),
            MissingFieldFound = null,
            BadDataFound = null
        };

        using var reader = new StreamReader(content, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, config);

        if (!csv.Read() || !csv.ReadHeader())
            throw new CsvValidationException("El archivo esta vacio.");

        var columns = csv.HeaderRecord!.Select(h => h.Trim().ToLowerInvariant()).ToArray();

        foreach (var name in columns)
            if (!ColumnNameRegex().IsMatch(name) || ReservedColumns.Contains(name))
                throw new CsvValidationException(
                    $"El nombre de columna '{name}' no es valido (solo letras, numeros y _; no se permiten id, commerce_id, created_at ni motivo).");

        if (columns.Distinct().Count() != columns.Length)
            throw new CsvValidationException("El archivo tiene columnas repetidas.");

        foreach (var required in RequiredColumns)
            if (!columns.Contains(required))
                throw new CsvValidationException($"El archivo no contiene la columna {required}.");

        var dateIndex = Array.IndexOf(columns, "pc_processdate");
        var rows = new List<string?[]>();
        while (csv.Read())
        {
            var line = csv.Context.Parser!.Row;
            var values = new string?[columns.Length];
            for (var i = 0; i < columns.Length; i++)
                values[i] = csv.TryGetField<string>(i, out var v) ? v : null;

            var rawDate = values[dateIndex];
            if (!DateOnly.TryParseExact(rawDate, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                throw new CsvValidationException($"Linea {line}: pc_processdate '{rawDate}' no es una fecha valida.");
            values[dateIndex] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            rows.Add(values);
        }

        if (rows.Count == 0)
            throw new CsvValidationException("El archivo no contiene registros.");

        return new CsvData(columns, rows);
    }
}
