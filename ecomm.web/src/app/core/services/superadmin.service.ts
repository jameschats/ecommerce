import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';
import {
  Announcement, AnnouncementUpsert, AuditEntry, BillingCharge, BlocklistEntry, CreditPack, ImpersonationResult,
  PackUpsert, PlanOption, PlanUpsert, PlatformAnalytics, PlatformBilling, PlatformEmail, PlatformEmailUpsert, PlatformPayment, PlatformPaymentUpsert,
  PlatformRevenue, PlatformStaff, SubStatusRow, TenantDetail, TenantDiagnostics, TenantSummary,
} from '../models/superadmin.model';

@Injectable({ providedIn: 'root' })
export class SuperAdminService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/superadmin`;

  tenants(search?: string): Observable<TenantSummary[]> {
    const q = search ? `?search=${encodeURIComponent(search)}` : '';
    return this.http.get<ApiResponse<TenantSummary[]>>(`${this.base}/tenants${q}`).pipe(map((r) => r.data ?? []));
  }
  tenant(id: number): Observable<TenantDetail> {
    return this.http.get<ApiResponse<TenantDetail>>(`${this.base}/tenants/${id}`).pipe(map((r) => r.data as TenantDetail));
  }
  revenue(): Observable<PlatformRevenue> {
    return this.http.get<ApiResponse<PlatformRevenue>>(`${this.base}/revenue`).pipe(map((r) => r.data as PlatformRevenue));
  }
  analytics(from: string, to: string): Observable<PlatformAnalytics> {
    return this.http.get<ApiResponse<PlatformAnalytics>>(`${this.base}/analytics?from=${from}&to=${to}`).pipe(map((r) => r.data as PlatformAnalytics));
  }
  setStanding(id: number, standing: string, reason: string | null): Observable<unknown> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/tenants/${id}/standing`, { standing, reason });
  }
  suspend(id: number): Observable<unknown> { return this.http.post<ApiResponse<unknown>>(`${this.base}/tenants/${id}/suspend`, {}); }
  activate(id: number): Observable<unknown> { return this.http.post<ApiResponse<unknown>>(`${this.base}/tenants/${id}/activate`, {}); }
  offboard(id: number): Observable<unknown> { return this.http.post<ApiResponse<unknown>>(`${this.base}/tenants/${id}/offboard`, {}); }
  plans(): Observable<PlanOption[]> {
    return this.http.get<ApiResponse<PlanOption[]>>(`${this.base}/plans`).pipe(map((r) => r.data ?? []));
  }
  createPlan(req: PlanUpsert): Observable<unknown> { return this.http.post<ApiResponse<unknown>>(`${this.base}/plans`, req); }
  updatePlan(id: number, req: PlanUpsert): Observable<unknown> { return this.http.put<ApiResponse<unknown>>(`${this.base}/plans/${id}`, req); }
  packs(): Observable<CreditPack[]> {
    return this.http.get<ApiResponse<CreditPack[]>>(`${this.base}/credit-packs`).pipe(map((r) => r.data ?? []));
  }
  createPack(req: PackUpsert): Observable<unknown> { return this.http.post<ApiResponse<unknown>>(`${this.base}/credit-packs`, req); }
  updatePack(id: number, req: PackUpsert): Observable<unknown> { return this.http.put<ApiResponse<unknown>>(`${this.base}/credit-packs/${id}`, req); }
  grantCredits(id: number, amount: number, reason: string | null): Observable<unknown> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/tenants/${id}/grant-credits`, { amount, reason });
  }
  subscriptions(status = ''): Observable<SubStatusRow[]> {
    const q = status ? `?status=${status}` : '';
    return this.http.get<ApiResponse<SubStatusRow[]>>(`${this.base}/subscriptions${q}`).pipe(map((r) => r.data ?? []));
  }
  charges(limit = 100): Observable<BillingCharge[]> {
    return this.http.get<ApiResponse<BillingCharge[]>>(`${this.base}/charges?limit=${limit}`).pipe(map((r) => r.data ?? []));
  }
  recordPayment(id: number, planId: number, amount: number, reference: string | null): Observable<unknown> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/tenants/${id}/record-payment`, { planId, amount, reference });
  }
  changePlan(id: number, planId: number): Observable<unknown> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/tenants/${id}/plan`, { planId });
  }
  setTrial(id: number, trialEndsAt: string | null): Observable<unknown> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/tenants/${id}/trial`, { trialEndsAt });
  }
  setTags(id: number, tags: string | null): Observable<unknown> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/tenants/${id}/tags`, { tags });
  }
  addNote(id: number, note: string): Observable<unknown> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/tenants/${id}/notes`, { note });
  }
  impersonate(id: number, mode: 'view' | 'full'): Observable<ImpersonationResult> {
    return this.http.post<ApiResponse<ImpersonationResult>>(`${this.base}/tenants/${id}/impersonate?mode=${mode}`, {}).pipe(map((r) => r.data as ImpersonationResult));
  }
  blocklist(): Observable<BlocklistEntry[]> {
    return this.http.get<ApiResponse<BlocklistEntry[]>>(`${this.base}/blocklist`).pipe(map((r) => r.data ?? []));
  }
  addBlock(type: string, value: string, reason: string | null): Observable<unknown> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/blocklist`, { type, value, reason });
  }
  removeBlock(id: number): Observable<unknown> { return this.http.delete<ApiResponse<unknown>>(`${this.base}/blocklist/${id}`); }
  diagnostics(id: number): Observable<TenantDiagnostics> {
    return this.http.get<ApiResponse<TenantDiagnostics>>(`${this.base}/tenants/${id}/diagnostics`).pipe(map((r) => r.data as TenantDiagnostics));
  }
  resendNotification(historyId: number): Observable<unknown> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/notifications/${historyId}/resend`, {});
  }
  announcements(): Observable<Announcement[]> {
    return this.http.get<ApiResponse<Announcement[]>>(`${this.base}/announcements`).pipe(map((r) => r.data ?? []));
  }
  createAnnouncement(req: AnnouncementUpsert): Observable<unknown> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/announcements`, req);
  }
  setAnnouncementActive(id: number, active: boolean): Observable<unknown> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/announcements/${id}/active`, { active });
  }
  platformPayment(): Observable<PlatformPayment> {
    return this.http.get<ApiResponse<PlatformPayment>>(`${this.base}/payment-settings`).pipe(map((r) => r.data as PlatformPayment));
  }
  savePlatformPayment(req: PlatformPaymentUpsert): Observable<unknown> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/payment-settings`, req);
  }
  refundCharge(billingHistoryId: number, amount: number, reason: string | null): Observable<unknown> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/refund`, { billingHistoryId, amount, reason });
  }
  platformBilling(): Observable<PlatformBilling> {
    return this.http.get<ApiResponse<PlatformBilling>>(`${this.base}/billing-settings`).pipe(map((r) => r.data as PlatformBilling));
  }
  savePlatformBilling(req: PlatformBilling): Observable<unknown> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/billing-settings`, req);
  }
  platformEmail(): Observable<PlatformEmail> {
    return this.http.get<ApiResponse<PlatformEmail>>(`${this.base}/email-settings`).pipe(map((r) => r.data as PlatformEmail));
  }
  savePlatformEmail(req: PlatformEmailUpsert): Observable<unknown> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/email-settings`, req);
  }
  sendTestEmail(to: string): Observable<unknown> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/email-settings/test`, { to });
  }
  staff(): Observable<PlatformStaff[]> {
    return this.http.get<ApiResponse<PlatformStaff[]>>(`${this.base}/staff`).pipe(map((r) => r.data ?? []));
  }
  createStaff(email: string, fullName: string | null, password: string): Observable<unknown> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/staff`, { email, fullName, password });
  }
  setStaffActive(userId: number, active: boolean): Observable<unknown> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/staff/${userId}/active`, { active });
  }
  audit(tenantId?: number, limit = 100): Observable<AuditEntry[]> {
    const q = tenantId ? `?tenantId=${tenantId}&limit=${limit}` : `?limit=${limit}`;
    return this.http.get<ApiResponse<AuditEntry[]>>(`${this.base}/audit${q}`).pipe(map((r) => r.data ?? []));
  }
}
