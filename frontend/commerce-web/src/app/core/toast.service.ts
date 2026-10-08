import { Injectable, signal } from '@angular/core';

export interface Toast { kind: 'ok' | 'error'; text: string; }

/** Mensajes globales (exito / error) mostrados por el shell de la aplicacion. */
@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly toast = signal<Toast | null>(null);
  private timer?: ReturnType<typeof setTimeout>;

  ok(text: string) { this.show({ kind: 'ok', text }); }
  error(text: string) { this.show({ kind: 'error', text }); }

  dismiss() { this.toast.set(null); }

  private show(t: Toast) {
    clearTimeout(this.timer);
    this.toast.set(t);
    this.timer = setTimeout(() => this.dismiss(), 6000);
  }
}
