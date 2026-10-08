import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'cargar' },
  { path: 'cargar', title: 'Cargar archivo', loadComponent: () => import('./features/upload/upload.component').then(m => m.UploadComponent) },
  { path: 'procesar', title: 'Procesar', loadComponent: () => import('./features/process/process.component').then(m => m.ProcessComponent) },
  { path: 'errores', title: 'Errores', loadComponent: () => import('./features/quarantine/quarantine.component').then(m => m.QuarantineComponent) },
  { path: '**', redirectTo: 'cargar' }
];
