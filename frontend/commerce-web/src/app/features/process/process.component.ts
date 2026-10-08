import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CommerceApiService } from '../../core/commerce-api.service';
import { PendingDate, ProcessResult } from '../../core/models';
import { ToastService } from '../../core/toast.service';

@Component({
  selector: 'app-process',
  imports: [DatePipe, RouterLink],
  template: `
    <h1>Procesar informacion</h1>
    <p class="hint">Valida los registros cargados de un dia (<code>pc_processdate</code>). Los que no cumplan se mueven a cuarentena.</p>

    <div class="card">
      <label>Fecha de proceso
        <select #sel [disabled]="processing()" (change)="date.set(sel.value)">
          <option value="">— Selecciona —</option>
          @for (d of dates(); track d.processDate) {
            <option [value]="d.processDate" [selected]="d.processDate === date()">
              {{ d.processDate | date:'dd/MM/yyyy' }} ({{ d.records }} registros)
            </option>
          }
        </select>
      </label>
      <button class="primary" (click)="process()" [disabled]="!date() || processing()">
        {{ processing() ? 'Procesando…' : 'Procesar' }}
      </button>
      @if (loaded() && dates().length === 0) {
        <p class="hint">No hay registros pendientes. <a routerLink="/cargar">Carga un archivo</a>.</p>
      }
    </div>

    @if (result(); as r) {
      <div class="card result">
        <strong>Fecha {{ r.processDate | date:'dd/MM/yyyy' }}</strong>
        <p>
          @if (r.quarantinedRows > 0) {
            {{ r.quarantinedRows }} registros con errores enviados a cuarentena.
            <a routerLink="/errores" [queryParams]="{ date: r.processDate }">Ver errores</a>
          } @else {
            Todos los registros son validos.
          }
        </p>
      </div>
    }
  `
})
export class ProcessComponent implements OnInit {
  private readonly api = inject(CommerceApiService);
  private readonly toast = inject(ToastService);

  protected readonly dates = signal<PendingDate[]>([]);
  protected readonly loaded = signal(false);
  protected readonly date = signal('');
  protected readonly processing = signal(false);
  protected readonly result = signal<ProcessResult | null>(null);

  ngOnInit() { this.loadDates(); }

  protected process() {
    this.processing.set(true);
    this.api.process(this.date()).subscribe({
      next: res => {
        this.result.set(res);
        this.toast.ok('Proceso finalizado.');
        this.date.set('');
        this.processing.set(false);
        this.loadDates();
      },
      error: () => this.processing.set(false)
    });
  }

  private loadDates() {
    this.api.getPendingDates().subscribe(d => { this.dates.set(d); this.loaded.set(true); });
  }
}
