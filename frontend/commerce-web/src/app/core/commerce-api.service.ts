import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PendingDate, ProcessResult, QuarantinePage, UploadResult } from './models';

@Injectable({ providedIn: 'root' })
export class CommerceApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/commerce';

  upload(file: File): Observable<UploadResult> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<UploadResult>(`${this.base}/upload`, form);
  }

  process(processDate: string): Observable<ProcessResult> {
    return this.http.post<ProcessResult>(`${this.base}/process`, { processDate });
  }

  getPendingDates(): Observable<PendingDate[]> {
    return this.http.get<PendingDate[]>(`${this.base}/process-dates`);
  }

  getQuarantine(page: number, pageSize: number, processDate?: string): Observable<QuarantinePage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (processDate) params = params.set('processDate', processDate);
    return this.http.get<QuarantinePage>(`${this.base}/quarantine`, { params });
  }
}
