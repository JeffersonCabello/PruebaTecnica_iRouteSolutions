export interface ParsedCsv { headers: string[]; rows: string[][]; }

/** Nombre esperado: commerce_DDMMYYYY.csv con una fecha real. */
export function isValidFileName(name: string): boolean {
  const m = /^commerce_(\d{2})(\d{2})(\d{4})\.csv$/i.exec(name);
  if (!m) return false;
  const [d, mo, y] = [+m[1], +m[2], +m[3]];
  const date = new Date(y, mo - 1, d);
  return date.getFullYear() === y && date.getMonth() === mo - 1 && date.getDate() === d;
}

/** Parser CSV minimo: comillas dobles, saltos de linea dentro de comillas y delimitador "," o ";". */
export function parseCsv(text: string): ParsedCsv {
  const src = text.replace(/^﻿/, '');
  const firstLine = src.split(/\r?\n/, 1)[0] ?? '';
  const delimiter = (firstLine.match(/;/g)?.length ?? 0) > (firstLine.match(/,/g)?.length ?? 0) ? ';' : ',';

  const records: string[][] = [];
  let row: string[] = [];
  let field = '';
  let quoted = false;

  for (let i = 0; i < src.length; i++) {
    const c = src[i];
    if (quoted) {
      if (c === '"' && src[i + 1] === '"') { field += '"'; i++; }
      else if (c === '"') quoted = false;
      else field += c;
    } else if (c === '"') quoted = true;
    else if (c === delimiter) { row.push(field.trim()); field = ''; }
    else if (c === '\n' || c === '\r') {
      if (c === '\r' && src[i + 1] === '\n') i++;
      row.push(field.trim()); field = '';
      if (row.some(v => v !== '')) records.push(row);
      row = [];
    } else field += c;
  }
  row.push(field.trim());
  if (row.some(v => v !== '')) records.push(row);

  const [headers = [], ...rows] = records;
  return { headers: headers.map(h => h.toLowerCase()), rows };
}
