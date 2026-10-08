import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ToastService } from './core/toast.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header class="topbar">
      <strong>Commerce</strong>
      <nav>
        <a routerLink="/cargar" routerLinkActive="active">1. Cargar CSV</a>
        <a routerLink="/procesar" routerLinkActive="active">2. Procesar</a>
        <a routerLink="/errores" routerLinkActive="active">3. Errores</a>
      </nav>
    </header>
    <main><router-outlet /></main>

    @if (toast.toast(); as t) {
      <div class="toast" [class.error]="t.kind === 'error'" role="alert" (click)="toast.dismiss()">{{ t.text }}</div>
    }
  `
})
export class AppComponent {
  protected readonly toast = inject(ToastService);
}
