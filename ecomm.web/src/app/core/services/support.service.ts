import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';
import { Ticket, TicketThread } from '../models/support.model';

@Injectable({ providedIn: 'root' })
export class SupportService {
  private readonly http = inject(HttpClient);
  private readonly merchant = `${API_BASE_URL}/support/tickets`;
  private readonly admin = `${API_BASE_URL}/superadmin/support/tickets`;

  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  // Merchant
  myTickets(): Observable<Ticket[]> { return this.unwrap(this.http.get<ApiResponse<Ticket[]>>(this.merchant)); }
  createTicket(subject: string, message: string): Observable<Ticket> { return this.unwrap(this.http.post<ApiResponse<Ticket>>(this.merchant, { subject, message })); }
  thread(id: number): Observable<TicketThread> { return this.unwrap(this.http.get<ApiResponse<TicketThread>>(`${this.merchant}/${id}`)); }
  reply(id: number, body: string): Observable<unknown> { return this.http.post<ApiResponse<unknown>>(`${this.merchant}/${id}/reply`, { body }); }

  // Platform
  queue(status = ''): Observable<Ticket[]> {
    const q = status ? `?status=${status}` : '';
    return this.unwrap(this.http.get<ApiResponse<Ticket[]>>(`${this.admin}${q}`));
  }
  adminThread(id: number): Observable<TicketThread> { return this.unwrap(this.http.get<ApiResponse<TicketThread>>(`${this.admin}/${id}`)); }
  adminReply(id: number, body: string, isInternal: boolean): Observable<unknown> { return this.http.post<ApiResponse<unknown>>(`${this.admin}/${id}/reply`, { body, isInternal }); }
  setStatus(id: number, status: string): Observable<unknown> { return this.http.put<ApiResponse<unknown>>(`${this.admin}/${id}/status`, { status }); }
}
