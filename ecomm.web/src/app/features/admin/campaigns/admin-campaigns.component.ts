import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { API_BASE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';

interface Campaign {
  campaignId: number;
  name: string;
  subject: string;
  body: string;
  audience: string;
  status: string;
  totalRecipients: number;
  sentCount: number;
  failedCount: number;
  startedAt: string | null;
  completedAt: string | null;
  createdAt: string;
}

interface AudienceCounts {
  contacts: number; customers: number; both: number;
  /** Every contact with an email, consented or not. */
  allContacts: number;
  /** How many of allContacts never ticked the box — what the warning reports. */
  notConsented: number;
}
interface SendResult { attempted: number; sent: number; failed: number; remaining: number; status: string; }

/**
 * Promotional campaigns.
 *
 * Sends in batches, driven from here rather than a background worker: each request stays
 * short, and an interrupted send resumes because the recipient rows record who has already
 * been mailed. The screen keeps calling until nothing is left.
 */
@Component({
  selector: 'app-admin-campaigns',
  imports: [FormsModule, DatePipe],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <div class="flex items-center justify-between mb-1">
        <h1 class="text-xl font-bold text-slate-900">Campaigns</h1>
        <button type="button" (click)="startNew()" class="btn-primary">+ New campaign</button>
      </div>
      <p class="text-sm text-slate-500 mb-5">
        Promotional emails to people who opted in.
        @if (audience(); as a) {
          <span class="ml-1">
            {{ a.contacts }} opted in · {{ a.customers }} customers · {{ a.both }} combined · {{ a.allContacts }} contacts in total.
          </span>
        }
      </p>

      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      @if (editing(); as e) {
        <div class="bg-white border border-slate-200 rounded-xl p-5 mb-5 space-y-3">
          <h2 class="font-semibold text-slate-900">{{ e.campaignId ? 'Edit campaign' : 'New campaign' }}</h2>
          <label class="block"><span class="lbl">Name (internal)</span>
            <input [(ngModel)]="e.name" class="input" placeholder="Diwali 2026 offer" /></label>
          <label class="block"><span class="lbl">Subject</span>
            <input [(ngModel)]="e.subject" class="input" placeholder="What the recipient sees" /></label>
          <label class="block"><span class="lbl">Message</span>
            <textarea [(ngModel)]="e.body" rows="10" class="input font-mono text-xs"
                      placeholder="Hi {{ '{{Name}}' }}, ..."></textarea>
            <span class="text-xs text-slate-500 mt-1 block">
              {{ '{{Name}}' }} is replaced with the recipient's name, or "there" when we do not have one.
            </span>
          </label>
          <label class="block"><span class="lbl">Send to</span>
            <select [(ngModel)]="e.audience" class="input">
              <option value="Contacts">Contacts who opted in ({{ audience()?.contacts ?? 0 }})</option>
              <option value="Customers">Customers who have ordered ({{ audience()?.customers ?? 0 }})</option>
              <option value="Both">Both ({{ audience()?.both ?? 0 }})</option>
              <option value="AllContacts">All contacts ({{ audience()?.allContacts ?? 0 }})</option>
            </select>
            @if (e.audience === 'AllContacts' && (audience()?.notConsented ?? 0) > 0) {
              <div class="mt-2 rounded-lg bg-amber-50 border border-amber-200 text-[13px] text-amber-900 px-3 py-2">
                <strong>{{ audience()?.notConsented }} of these never agreed to receive emails.</strong>
                They wrote in with a question; that is not the same as subscribing. Sending anyway
                is your call, but complaints from people who did not ask are what get a sending
                domain blocked — and Brevo suspends accounts over it. Spam-marked contacts are
                excluded either way.
              </div>
            }
          </label>
          <div class="flex gap-2">
            <button type="button" (click)="save()" [disabled]="saving()" class="btn-primary">
              {{ saving() ? 'Saving…' : 'Save draft' }}
            </button>
            <button type="button" (click)="editing.set(null)" class="btn-ghost border border-slate-300">Cancel</button>
          </div>
        </div>
      }

      @if (loading()) {
        <div class="p-10 text-center text-slate-400">Loading…</div>
      } @else if (!campaigns().length) {
        <div class="bg-white border border-slate-200 rounded-xl p-12 text-center text-slate-500">No campaigns yet.</div>
      } @else {
        <div class="space-y-3">
          @for (c of campaigns(); track c.campaignId) {
            <div class="bg-white border border-slate-200 rounded-xl p-4">
              <div class="flex flex-wrap items-center gap-2">
                <span class="font-semibold text-slate-900">{{ c.name }}</span>
                <span class="text-xs rounded px-2 py-0.5"
                      [class]="c.status === 'Sent' ? 'bg-emerald-50 text-emerald-700'
                             : c.status === 'Sending' ? 'bg-amber-50 text-amber-700'
                             : c.status === 'Failed' ? 'bg-red-50 text-red-700' : 'bg-slate-100 text-slate-500'">
                  {{ c.status }}
                </span>
                <span class="text-xs text-slate-400 ml-auto">{{ c.createdAt | date: 'dd MMM yyyy' }}</span>
              </div>
              <p class="text-sm text-slate-600 mt-1">{{ c.subject }}</p>

              @if (c.status !== 'Draft') {
                <div class="mt-2 text-xs text-slate-500">
                  {{ c.sentCount }} sent
                  @if (c.failedCount) { <span class="text-red-600">· {{ c.failedCount }} failed</span> }
                  of {{ c.totalRecipients }}
                </div>
                <div class="mt-1 h-1.5 bg-slate-100 rounded overflow-hidden">
                  <div class="h-full bg-emerald-500" [style.width.%]="pct(c)"></div>
                </div>
              }

              <div class="flex flex-wrap gap-2 mt-3 pt-3 border-t border-slate-100">
                @if (c.status === 'Draft') {
                  <button type="button" (click)="edit(c)" class="btn-ghost border border-slate-300 h-8 text-sm">Edit</button>
                  <button type="button" (click)="send(c)" [disabled]="sendingId() === c.campaignId"
                          class="btn-primary h-8 text-sm">
                    {{ sendingId() === c.campaignId ? 'Sending…' : 'Send now' }}
                  </button>
                } @else if (c.status === 'Sending') {
                  <button type="button" (click)="send(c)" [disabled]="sendingId() === c.campaignId"
                          class="btn-primary h-8 text-sm">
                    {{ sendingId() === c.campaignId ? 'Sending…' : 'Resume send' }}
                  </button>
                }
                @if (c.status !== 'Sending') {
                  <button type="button" (click)="remove(c)" class="h-8 text-sm px-3 rounded-lg text-red-600 hover:bg-red-50">Delete</button>
                }
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class AdminCampaignsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/campaigns`;

  readonly campaigns = signal<Campaign[]>([]);
  readonly audience = signal<AudienceCounts | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly sendingId = signal<number | null>(null);
  readonly editing = signal<Partial<Campaign> | null>(null);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
    this.http.get<ApiResponse<AudienceCounts>>(`${this.base}/audience`)
      .subscribe({ next: (r) => this.audience.set(r.data ?? null), error: () => {} });
  }

  pct(c: Campaign): number {
    return c.totalRecipients ? Math.round(((c.sentCount + c.failedCount) / c.totalRecipients) * 100) : 0;
  }

  load(): void {
    this.loading.set(true);
    this.http.get<ApiResponse<Campaign[]>>(this.base).subscribe({
      next: (r) => { this.campaigns.set(r.data ?? []); this.loading.set(false); },
      error: () => { this.error.set('Could not load campaigns.'); this.loading.set(false); },
    });
  }

  startNew(): void {
    this.editing.set({ name: '', subject: '', body: '', audience: 'Contacts' });
  }

  edit(c: Campaign): void { this.editing.set({ ...c }); }

  save(): void {
    const e = this.editing();
    if (!e) return;
    this.saving.set(true);
    this.error.set(null);
    const body = { name: e.name, subject: e.subject, body: e.body, audience: e.audience };
    const req = e.campaignId
      ? this.http.put<ApiResponse<Campaign>>(`${this.base}/${e.campaignId}`, body)
      : this.http.post<ApiResponse<Campaign>>(this.base, body);

    req.subscribe({
      next: () => { this.saving.set(false); this.editing.set(null); this.load(); },
      error: (err) => { this.saving.set(false); this.error.set(err?.error?.message ?? 'Could not save.'); },
    });
  }

  /**
   * Sends batch after batch until the server says none are left.
   *
   * Confirmed first, and only once: this is the one action in the admin that cannot be
   * undone — an email that has gone cannot be recalled.
   */
  send(c: Campaign): void {
    const key = c.audience === 'Customers' ? 'customers'
      : c.audience === 'Both' ? 'both'
      : c.audience === 'AllContacts' ? 'allContacts'
      : 'contacts';
    const size = c.totalRecipients || this.audience()?.[key] || 0;
    if (!confirm(`Send "${c.name}" to ${size} recipient(s)? This cannot be undone.`)) return;

    this.sendingId.set(c.campaignId);
    this.error.set(null);
    this.sendLoop(c.campaignId);
  }

  private sendLoop(id: number): void {
    this.http.post<ApiResponse<SendResult>>(`${this.base}/${id}/send?batchSize=25`, {}).subscribe({
      next: (r) => {
        const res = r.data;
        if (res && res.remaining > 0) {
          this.load();
          this.sendLoop(id);   // next batch
          return;
        }
        this.sendingId.set(null);
        this.message.set(r.message ?? 'Campaign sent.');
        setTimeout(() => this.message.set(null), 4000);
        this.load();
      },
      error: (e) => {
        this.sendingId.set(null);
        this.error.set(e?.error?.message ?? 'The send stopped. Use Resume send to continue where it left off.');
        this.load();
      },
    });
  }

  remove(c: Campaign): void {
    if (!confirm(`Delete "${c.name}"?`)) return;
    this.http.delete<ApiResponse<unknown>>(`${this.base}/${c.campaignId}`).subscribe({
      next: () => this.load(),
      error: (e) => this.error.set(e?.error?.message ?? 'Could not delete.'),
    });
  }
}
