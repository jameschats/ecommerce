import { HttpClient } from '@angular/common/http';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { API_BASE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';

interface Abandoned {
  cartId: number;
  userId: number | null;
  customerName: string | null;
  email: string | null;
  phone: string | null;
  itemCount: number;
  value: number;
  lastActivity: string;
  remindedAt: string | null;
}

/**
 * Baskets a signed-in shopper built and never ordered.
 *
 * Only signed-in shoppers appear, and that loses nothing actionable: recovering a basket
 * means emailing someone, and an anonymous visitor has left no address to email.
 */
@Component({
  selector: 'app-admin-abandoned',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Abandoned estimates</h1>
      <p class="text-sm text-slate-500 mb-5">
        Baskets left unfinished by signed-in customers. Anonymous visitors leave no address to contact.
      </p>

      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      <div class="flex items-center gap-2 mb-4 text-sm">
        <span class="text-slate-500">Idle for at least</span>
        <select [(ngModel)]="idleHours" (ngModelChange)="load()" class="input max-w-[140px]">
          <option [ngValue]="1">1 hour</option>
          <option [ngValue]="6">6 hours</option>
          <option [ngValue]="24">1 day</option>
          <option [ngValue]="72">3 days</option>
          <option [ngValue]="168">1 week</option>
        </select>
      </div>

      @if (loading()) {
        <div class="p-10 text-center text-slate-400">Loading…</div>
      } @else if (!rows().length) {
        <div class="bg-white border border-slate-200 rounded-xl p-12 text-center text-slate-500">
          Nothing left hanging. Baskets show up here once they have been idle for the period above.
        </div>
      } @else {
        <div class="bg-white border border-slate-200 rounded-xl overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="text-left text-slate-400 border-b border-slate-100">
              <tr>
                <th class="px-4 py-2">Customer</th>
                <th class="px-2 py-2 text-right">Items</th>
                <th class="px-2 py-2 text-right">Value</th>
                <th class="px-4 py-2">Last activity</th>
                <th class="px-4 py-2"></th>
              </tr>
            </thead>
            <tbody>
              @for (r of rows(); track r.cartId) {
                <tr class="border-b border-slate-50">
                  <td class="px-4 py-2">
                    <div class="text-slate-800">{{ r.customerName || 'Unnamed' }}</div>
                    <div class="text-xs text-slate-500">{{ r.email || r.phone || 'no contact details' }}</div>
                  </td>
                  <td class="px-2 py-2 text-right">{{ r.itemCount }}</td>
                  <td class="px-2 py-2 text-right font-medium">{{ r.value | currency: 'INR' : 'symbol' : '1.2-2' }}</td>
                  <td class="px-4 py-2 text-slate-500">{{ r.lastActivity | date: 'dd MMM, HH:mm' }}</td>
                  <td class="px-4 py-2 text-right">
                    @if (r.remindedAt) {
                      <span class="text-xs text-slate-400">reminded {{ r.remindedAt | date: 'dd MMM' }}</span>
                    } @else if (r.email) {
                      <button type="button" (click)="remind(r)" [disabled]="busy() === r.cartId"
                              class="text-sm px-3 py-1.5 rounded-lg border border-slate-300 hover:bg-slate-50 disabled:opacity-50">
                        {{ busy() === r.cartId ? 'Sending…' : 'Send reminder' }}
                      </button>
                    } @else {
                      <span class="text-xs text-slate-400">no email</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
})
export class AdminAbandonedComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/abandoned`;

  readonly rows = signal<Abandoned[]>([]);
  readonly loading = signal(true);
  readonly busy = signal<number | null>(null);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  idleHours = 24;

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.http.get<ApiResponse<Abandoned[]>>(`${this.base}?idleHours=${this.idleHours}`).subscribe({
      next: (r) => { this.rows.set(r.data ?? []); this.loading.set(false); },
      error: () => { this.error.set('Could not load.'); this.loading.set(false); },
    });
  }

  remind(r: Abandoned): void {
    this.busy.set(r.cartId);
    this.error.set(null);
    this.http.post<ApiResponse<unknown>>(`${this.base}/${r.cartId}/remind`, {}).subscribe({
      next: (res) => {
        this.busy.set(null);
        this.message.set(res.message ?? 'Reminder sent.');
        setTimeout(() => this.message.set(null), 3000);
        this.load();
      },
      error: (e) => {
        this.busy.set(null);
        this.error.set(e?.error?.message ?? 'Could not send the reminder.');
      },
    });
  }
}
