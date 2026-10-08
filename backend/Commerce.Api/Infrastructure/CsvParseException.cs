namespace Commerce.Api.Infrastructure;

/// <summary>Error de validacion del archivo subido; se traduce a HTTP 400.</summary>
public sealed class CsvValidationException(string message) : Exception(message);
