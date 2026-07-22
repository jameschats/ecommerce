import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { API_BASE_URL, SITE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';
import { SeoService } from '../../../core/services/seo.service';

interface TimelineEntry { status: string; note: string | null; location: string | null; at: string; source: string; }
interface OrderLookup {
  orderNumber: string; status: string; placedAt: string | null;
  courier: string | null; trackingNumber: string | null; estimatedDeliveryDate: string | null;
  shippedAt: string | null; deliveredAt: string | null;
  timeline: TimelineEntry[];
}

/** Public "where is my order" page — no sign-in, so a shopper doesn't have to remember a password to ask. */
@Component({
  selector: 'app-track-order',
  imports: [FormsModule, DatePipe],
  template: `
    <section class="page-container py-12">
      <div class="max-w-xl mx-auto">
        <h1 class="text-3xl font-bold text-slate-900">Track your order</h1>
        <p class="mt-2 text-slate-600 mb-8">Enter your order number and the email you ordered with.</p>

        <form (ngSubmit)="search()" class="bg-white border border-slate-200 rounded-2xl p-6 space-y-4">
          <div>
            <label class="lbl">Order number</label>
            <input [(ngModel)]="orderNumber" name="orderNumber" required class="input" placeholder="ORD20260722-00001" />
          </div>
          <div>
            <label class="lbl">Email</label>
            <input [(ngModel)]="email" name="email" type="email" required class="input" />
          </div>
          @if (error()) { <p class="text-sm text-red-600">{{ error() }}</p> }
          <button type="submit" class="btn-primary" [disabled]="busy() || !orderNumber.trim() || !email.trim()">
            {{ busy() ? 'Looking…' : 'Track order' }}
          </button>
        </form>

        @if (result(); as r) {
          <div class="mt-6 bg-white border border-slate-200 rounded-2xl p-6">
            <div class="flex items-center justify-between gap-3">
              <div>
                <div class="font-semibold text-slate-800">{{ r.orderNumber }}</div>
                @if (r.placedAt) { <div class="text-xs text-slate-500">Placed {{ r.placedAt | date:'dd MMM yyyy' }}</div> }
              </div>
              <span class="text-xs px-2.5 py-1 rounded-full bg-blue-50 text-blue-700 font-medium">{{ r.status }}</span>
            </div>

            @if (r.trackingNumber) {
              <div class="mt-4 text-sm text-slate-600">
                <div>Courier: <span class="text-slate-800">{{ r.courier }}</span></div>
                <div>Tracking: <span class="text-slate-800">{{ r.trackingNumber }}</span></div>
                @if (r.estimatedDeliveryDate) {
                  <div>Expected by <span class="text-slate-800">{{ r.estimatedDeliveryDate | date:'dd MMM yyyy' }}</span></div>
                }
              </div>
            }

            @if (r.timeline.length) {
              <ol class="mt-5 border-l border-slate-200 pl-4 space-y-4">
                @for (e of reversed(r.timeline); track $index) {
                  <li class="relative">
                    <span class="absolute -left-[21px] top-1.5 w-2.5 h-2.5 rounded-full"
                          [class]="$index === 0 ? 'bg-primary' : 'bg-slate-300'"></span>
                    <div class="text-sm text-slate-800">{{ e.status }}</div>
                    <div class="text-xs text-slate-500">
                      {{ e.at | date:'dd MMM, HH:mm' }}@if (e.location) { <span> · {{ e.location }}</span> }
                    </div>
                  </li>
                }
              </ol>
            }
          </div>
        }
      </div>
    </section>
  `,
})
export class TrackOrderComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly seo = inject(SeoService);

  orderNumber = '';
  email = '';
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly result = signal<OrderLookup | null>(null);

  ngOnInit(): void {
    this.seo.setMeta({
      title: 'Track your order',
      description: 'Check the status of your order using your order number and email.',
      url: `${SITE_URL}/track`,
    });
  }

  /** Newest first reads better for a delivery journey. */
  reversed(entries: TimelineEntry[]): TimelineEntry[] { return [...entries].reverse(); }

  search(): void {
    this.busy.set(true);
    this.error.set(null);
    this.result.set(null);

    const q = new URLSearchParams({ orderNumber: this.orderNumber.trim(), email: this.email.trim() });
    this.http.get<ApiResponse<OrderLookup>>(`${API_BASE_URL}/orders/lookup?${q}`).subscribe({
      next: (r) => { this.result.set(r.data ?? null); this.busy.set(false); },
      error: (e) => {
        this.error.set(e?.status === 429
          ? 'Too many attempts — please wait a few minutes and try again.'
          : "We couldn't find an order with those details. Check the order number and email and try again.");
        this.busy.set(false);
      },
    });
  }
}
