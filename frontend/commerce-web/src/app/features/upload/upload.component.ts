import { Component, computed, inject, signal } from '@angular/core';
import { CommerceApiService } from '../../core/commerce-api.service';
import { ParsedCsv, isValidFileName, parseCsv } from '../../core/csv.util';
import { ToastService } from '../../core/toast.service';

const PREVIEW_ROWS = 100;
// Columnas que necesita el proceso de validacion (espejo del parser del API)
const REQUIRED_COLUMNS = ['pc_processdate', 'pc_nomcomred', 'pc_numdoc'];

@Component({
  selector: 'app-upload',
  template: `
    <h1>Cargar archivo</h1>
    <p class="hint">Selecciona un archivo <code>commerce_DDMMYYYY.csv</code>. Podras revisarlo antes de enviarlo.</p>

    <div class="card">
      <input type="file" accept=".csv" (change)="onFile($event)" [disabled]="sending()" />
    </div>

    @if (file(); as f) {
      @if (problems().length) {
        <div class="card error-box">
          <strong>No se puede enviar:</strong>
          <ul>@for (p of problems(); track p) { <li>{{ p }}</li> }</ul>
        </div>
      }

      @if (csv(); as c) {
        <div class="card">
          <div class="row">
            <span><strong>{{ f.name }}</strong> · {{ c.rows.length }} registros · {{ c.headers.length }} columnas</span>
            <button class="primary" (click)="send()" [disabled]="problems().length > 0 || sending()">
              {{ sending() ? 'Enviando…' : 'Enviar al servidor' }}
            </button>
          </div>
          @if (c.rows.length > previewRows) {
            <p class="hint">Vista previa de los primeros {{ previewRows }} registros.</p>
          }
          <div class="table-wrap">
            <table>
              <thead><tr><th>#</th>@for (h of c.headers; track $index) { <th>{{ h }}</th> }</tr></thead>
              <tbody>
                @for (r of preview(); track $index) {
                  <tr><td>{{ $index + 1 }}</td>@for (v of r; track $index) { <td>{{ v }}</td> }</tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    }
  `
})
export class UploadComponent {
  private readonly api = inject(CommerceApiService);
  private readonly toast = inject(ToastService);

  protected readonly previewRows = PREVIEW_ROWS;
  protected readonly file = signal<File | null>(null);
  protected readonly csv = signal<ParsedCsv | null>(null);
  protected readonly sending = signal(false);

  protected readonly preview = computed(() => this.csv()?.rows.slice(0, PREVIEW_ROWS) ?? []);

  /** Validaciones locales: evitan un viaje al servidor con un archivo que seguro seria rechazado. */
  protected readonly problems = computed(() => {
    const f = this.file();
    const c = this.csv();
    if (!f) return [];
    const list: string[] = [];
    if (!isValidFileName(f.name)) list.push('El nombre debe ser commerce_DDMMYYYY.csv con una fecha valida.');
    if (f.size === 0 || !c || (c.headers.length === 0)) list.push('El archivo esta vacio.');
    else {
      for (const col of REQUIRED_COLUMNS)
        if (!c.headers.includes(col)) list.push(`Falta la columna ${col}.`);
      if (c.rows.length === 0) list.push('El archivo no contiene registros.');
    }
    return list;
  });

  protected async onFile(event: Event) {
    const input = event.target as HTMLInputElement;
    const f = input.files?.[0] ?? null;
    this.file.set(f);
    this.csv.set(f ? parseCsv(await f.text()) : null);
  }

  protected send() {
    const f = this.file();
    if (!f) return;
    this.sending.set(true);
    this.api.upload(f).subscribe({
      next: res => {
        this.toast.ok(`${res.insertedRows} registros cargados desde ${res.fileName}.`);
        this.file.set(null);
        this.csv.set(null);
        this.sending.set(false);
      },
      error: () => this.sending.set(false)
    });
  }
}
