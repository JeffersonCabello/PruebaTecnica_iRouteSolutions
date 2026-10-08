import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ToastService } from './toast.service';

/** Convierte los ProblemDetails del API en un mensaje legible para el usuario. */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);
  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      const message = err.status === 0
        ? 'No se pudo conectar con el servidor.'
        : err.error?.title ?? err.error?.errors?.ProcessDate?.[0] ?? `Error ${err.status}`;
      toast.error(message);
      return throwError(() => err);
    })
  );
};
