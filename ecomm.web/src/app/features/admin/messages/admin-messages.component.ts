import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ContactMessage, ContactService } from '../../../core/services/contact.service';
import { PagedResult } from '../../../core/models/api-response.model';

@Component({
  selector: 'app-admin-messages',
  imports: [DatePipe],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900">Messages</h1>
      <p class="text-sm text-slate-500 mb-6">Enquiries sent from your storefront contact form.</p>

      <div class="flex gap-2 mb-4 text-sm">
        @for (f of filters; track f.value) {
          <button type="button" (click)="setFilter(f.value)"
                  class="px-3 py-1.5 rounded-lg border transition"
                  [class]="filter() === f.value ? 'border-primary text-primary bg-blue-50' : 'border-slate-200 text-slate-600 hover:bg-slate-50'">
            {{ f.label }}
          </button>
        }
      </div>

      @if (loading()) {
        <p class="text-slate-400 text-sm">Loading…</p>
      } @else if ((result()?.items ?? []).length === 0) {
        <div class="bg-white border border-slate-200 rounded-xl p-8 text-center">
          <div class="text-3xl">📭</div>
          <p class="text-slate-600 font-medium mt-2">No messages here</p>
          <p class="text-slate-400 text-sm mt-1">Enquiries from your contact page will appear here.</p>
        </div>
      } @else {
        <div class="space-y-3">
          @for (m of result()?.items ?? []; track m.contactMessageId) {
            <div class="bg-white border rounded-xl p-4"
                 [class]="m.status === 'New' ? 'border-amber-200' : 'border-slate-200'">
              <div class="flex items-start justify-between gap-4">
                <div class="min-w-0">
                  <div class="font-medium text-slate-800">
                    {{ m.name }}
                    @if (m.status === 'New') {
                      <span class="ml-1.5 text-[10px] px-1.5 py-0.5 rounded-full bg-amber-100 text-amber-700 align-middle">NEW</span>
                    }
                  </div>
                  <div class="text-xs text-slate-500">
                    <a [href]="'mailto:' + m.email" class="hover:underline">{{ m.email }}</a>
                    @if (m.phone) { <span> · {{ m.phone }}</span> }
                    <span> · {{ m.createdAt | date:'dd MMM yy, HH:mm' }}</span>
                  </div>
                </div>
                <button type="button" (click)="toggle(m)"
                        class="text-sm shrink-0 px-3 py-1.5 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50">
                  {{ m.status === 'New' ? 'Mark handled' : 'Reopen' }}
                </button>
              </div>
              @if (m.subject) { <div class="text-sm font-medium text-slate-700 mt-3">{{ m.subject }}</div> }
              <p class="text-sm text-slate-600 mt-1 whitespace-pre-line">{{ m.body }}</p>
              @if (m.sourceUrl) {
                <div class="text-xs text-slate-400 mt-2 truncate">Sent from {{ m.sourceUrl }}</div>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class AdminMessagesComponent implements OnInit {
  private readonly api = inject(ContactService);

  readonly loading = signal(true);
  readonly result = signal<PagedResult<ContactMessage> | null>(null);
  readonly filter = signal<string | undefined>('New');

  readonly filters = [
    { label: 'New', value: 'New' },
    { label: 'Handled', value: 'Handled' },
    { label: 'All', value: undefined as string | undefined },
  ];

  ngOnInit(): void { this.load(); }

  setFilter(value: string | undefined): void {
    this.filter.set(value);
    this.load();
  }

  toggle(m: ContactMessage): void {
    this.api.setStatus(m.contactMessageId, m.status === 'New' ? 'Handled' : 'New').subscribe(() => this.load());
  }

  private load(): void {
    this.loading.set(true);
    this.api.list(this.filter()).subscribe({
      next: (r) => { this.result.set(r); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
}
