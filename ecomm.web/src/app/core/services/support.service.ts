import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';
import { Agent, QueueFilter, Ticket, TicketThread } from '../models/support.model';

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
  queue(filter: QueueFilter = {}): Observable<Ticket[]> {
    const q = new URLSearchParams();
    if (filter.status) q.set('status', filter.status);
    if (filter.priority) q.set('priority', filter.priority);
    if (filter.tier) q.set('tier', filter.tier);
    if (filter.mine) q.set('mine', 'true');
    if (filter.unassigned) q.set('unassigned', 'true');
    const qs = q.toString();
    return this.unwrap(this.http.get<ApiResponse<Ticket[]>>(`${this.admin}${qs ? '?' + qs : ''}`));
  }
  agents(): Observable<Agent[]> { return this.unwrap(this.http.get<ApiResponse<Agent[]>>(`${this.admin}/agents`)); }
  adminThread(id: number): Observable<TicketThread> { return this.unwrap(this.http.get<ApiResponse<TicketThread>>(`${this.admin}/${id}`)); }
  adminReply(id: number, body: string, isInternal: boolean): Observable<unknown> { return this.http.post<ApiResponse<unknown>>(`${this.admin}/${id}/reply`, { body, isInternal }); }
  setStatus(id: number, status: string): Observable<Ticket> { return this.unwrap(this.http.put<ApiResponse<Ticket>>(`${this.admin}/${id}/status`, { status })); }
  setPriority(id: number, priority: string): Observable<Ticket> { return this.unwrap(this.http.put<ApiResponse<Ticket>>(`${this.admin}/${id}/triage`, { priority })); }
  escalate(id: number, tier?: string): Observable<Ticket> { return this.unwrap(this.http.put<ApiResponse<Ticket>>(`${this.admin}/${id}/escalate`, { tier })); }
  assign(id: number, assigneeUserId: number | null): Observable<Ticket> { return this.unwrap(this.http.put<ApiResponse<Ticket>>(`${this.admin}/${id}/assign`, { assigneeUserId })); }
  setTags(id: number, tags: string): Observable<Ticket> { return this.unwrap(this.http.put<ApiResponse<Ticket>>(`${this.admin}/${id}/tags`, { tags })); }
}
