import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { CommerceApiService } from '../../core/commerce-api.service';
import { QuarantinePage } from '../../core/models';

@Component({
  selector: 'app-quarantine',
  imports: [DatePipe],
  template: `
    <h1>Registros con errores</h1>

    <div class="card">
      <label>Filtrar por fecha
        <input type="date" #d [value]="date()" (change)="setDate(d.value)" />
      </label>
      @if (date()) { <button (click)="setDate('')">Quitar filtro</button> }
    </div>

    <div class="card">
      @if (loading()) { <p class="hint">Cargando…</p> }
      @if (data(); as r) {
        @if (r.items.length === 0) { <p class="hint">No hay registros en cuarentena.</p> }
        @if (r.items.length > 0) {
          <div class="table-wrap">
            <table>
              <thead>
                <tr>@for (c of r.columns; track c) { <th>{{ c }}</th> }</tr>
              </thead>
              <tbody>
                @for (i of r.items; track i['id']) {
                  <tr>
                    @for (c of r.columns; track c) {
                      @if (c === 'pc_processdate') { <td>{{ i[c] | date:'dd/MM/yyyy' }}</td> }
                      @else { <td [class.motivo]="c === 'motivo'">{{ i[c] }}</td> }
                    }
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <div class="row pager">
            <span>{{ r.total }} registros · pagina {{ r.page }} de {{ pages(r) }}</span>
            <span>
              <button (click)="go(r.page - 1)" [disabled]="r.page <= 1">Anterior</button>
              <button (click)="go(r.page + 1)" [disabled]="r.page >= pages(r)">Siguiente</button>
            </span>
          </div>
        }
      }
    </div>
  `
})
export class QuarantineComponent implements OnInit {
  private readonly api = inject(CommerceApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly pageSize = 20;

  protected readonly data = signal<QuarantinePage | null>(null);
  protected readonly loading = signal(false);
  protected readonly date = signal('');

  ngOnInit() {
    this.date.set(this.route.snapshot.queryParamMap.get('date') ?? '');
    this.go(1);
  }

  protected setDate(value: string) { this.date.set(value); this.go(1); }

  protected pages(r: QuarantinePage) { return Math.max(1, Math.ceil(r.total / r.pageSize)); }

  protected go(page: number) {
    this.loading.set(true);
    this.api.getQuarantine(page, this.pageSize, this.date() || undefined).subscribe({
      next: r => { this.data.set(r); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }
}
