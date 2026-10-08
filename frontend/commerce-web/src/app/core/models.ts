export interface UploadResult { fileName: string; insertedRows: number; }
export interface ProcessResult { processDate: string; quarantinedRows: number; }
export interface PendingDate { processDate: string; records: number; }

/** Las columnas dependen del CSV cargado (la tabla se crea dinamicamente); cada fila trae id + una clave por columna. */
export interface QuarantinePage {
  columns: string[];
  items: Record<string, string | number | null>[];
  total: number;
  page: number;
  pageSize: number;
}
