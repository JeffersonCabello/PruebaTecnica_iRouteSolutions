namespace Commerce.Api.Models;

/// <summary>
/// Contenido de un commerce_DDMMYYYY.csv. Las columnas no son fijas: salen del encabezado del archivo
/// y la tabla commerce se crea con ellas. Cada fila trae un valor por columna (mismo orden que Columns);
/// pc_processdate ya viene normalizada a yyyy-MM-dd.
/// </summary>
public sealed record CsvData(IReadOnlyList<string> Columns, IReadOnlyList<string?[]> Rows);

/// <summary>Pagina de commerce_quarantine. Las columnas dependen del CSV, por eso cada fila es un diccionario.</summary>
public sealed record QuarantinePage(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IDictionary<string, object?>> Items,
    int Total, int Page, int PageSize);

/// <summary>Fecha de proceso pendiente de procesar y cuantos registros tiene.</summary>
public sealed record PendingDate(DateOnly ProcessDate, int Records);

public sealed record UploadResult(string FileName, int InsertedRows);

public sealed record ProcessRequest(DateOnly ProcessDate);

public sealed record ProcessResult(DateOnly ProcessDate, int QuarantinedRows);
