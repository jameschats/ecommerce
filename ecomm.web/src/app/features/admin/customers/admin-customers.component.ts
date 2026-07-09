import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { CustomerAdminService } from '../../../core/services/customer-admin.service';
import { CustomerListItem, CustomerSegment } from '../../../core/models/customer.model';

@Component({
  selector: 'app-admin-customers',
  imports: [FormsModule, RouterLink, DecimalPipe, DatePipe],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <div class="flex items-center justify-between mb-1">
        <h1 class="text-xl font-bold text-slate-900">Customers</h1>
        <a routerLink="/admin/customers/new" class="btn-primary">+ Add customer</a>
      </div>
      <p class="text-sm text-slate-500 mb-4">Everyone who has an account or bought from your store.</p>

      <!-- Segments -->
      <div class="flex flex-wrap gap-2 mb-4">
        @for (s of segments(); track s.key) {
          <button type="button" (click)="pickSegment(s.key)"
            class="text-sm px-3 py-1.5 rounded-full border transition"
            [class]="segment() === s.key ? 'border-primary bg-primary/5 text-primary font-medium' : 'border-slate-200 text-slate-600 hover:bg-slate-50'">
            {{ s.label }} <span class="text-slate-400">{{ s.count }}</span>
          </button>
        }
      </div>

      <input [(ngModel)]="search" (ngModelChange)="onSearch($event)" placeholder="Search by name, email or phone…" class="input w-full mb-4" />

      <div class="bg-white border border-slate-200 rounded-xl overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-slate-50 text-slate-500 text-left">
            <tr>
              <th class="px-4 py-2 font-medium">Customer</th>
              <th class="px-4 py-2 font-medium text-right">Orders</th>
              <th class="px-4 py-2 font-medium text-right">Spent</th>
              <th class="px-4 py-2 font-medium">Last order</th>
              <th class="px-4 py-2 font-medium">Subscribed</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-100">
            @for (c of items(); track c.userId) {
              <tr class="hover:bg-slate-50">
                <td class="px-4 py-3">
                  <a [routerLink]="['/admin/customers', c.userId]" class="block">
                    <div class="font-medium text-slate-800">{{ c.fullName || 'No name' }}</div>
                    <div class="text-xs text-slate-400">{{ c.email || c.phoneNumber || '—' }}</div>
                  </a>
                </td>
                <td class="px-4 py-3 text-right text-slate-600">{{ c.orderCount }}</td>
                <td class="px-4 py-3 text-right font-medium text-slate-800">₹{{ c.totalSpent | number:'1.0-0' }}</td>
                <td class="px-4 py-3 text-slate-500">{{ c.lastOrderAt ? (c.lastOrderAt | date:'mediumDate') : '—' }}</td>
                <td class="px-4 py-3">
                  @if (c.acceptsEmailMarketing) { <span class="text-xs text-green-600">✓ Email</span> } @else { <span class="text-xs text-slate-300">—</span> }
                </td>
              </tr>
            }
            @if (!loading() && !items().length) {
              <tr><td colspan="5" class="px-4 py-10 text-center text-slate-400">No customers found.</td></tr>
            }
          </tbody>
        </table>
        @if (loading()) { <div class="p-6 text-center text-slate-400 text-sm">Loading…</div> }
      </div>

      @if (totalPages() > 1) {
        <div class="flex items-center justify-center gap-3 mt-4 text-sm">
          <button type="button" (click)="go(page() - 1)" [disabled]="page() <= 1" class="px-3 py-1.5 rounded-lg border border-slate-200 disabled:opacity-40">Prev</button>
          <span class="text-slate-500">Page {{ page() }} of {{ totalPages() }}</span>
          <button type="button" (click)="go(page() + 1)" [disabled]="page() >= totalPages()" class="px-3 py-1.5 rounded-lg border border-slate-200 disabled:opacity-40">Next</button>
        </div>
      }
    </div>
  `,
})
export class AdminCustomersComponent implements OnInit {
  private readonly api = inject(CustomerAdminService);

  readonly items = signal<CustomerListItem[]>([]);
  readonly segments = signal<CustomerSegment[]>([]);
  readonly loading = signal(true);
  readonly page = signal(1);
  readonly totalPages = signal(1);
  readonly segment = signal('all');
  search = '';
  private readonly search$ = new Subject<string>();

  ngOnInit(): void {
    this.api.segments().subscribe((s) => this.segments.set(s));
    this.search$.pipe(debounceTime(250), distinctUntilChanged()).subscribe(() => { this.page.set(1); this.load(); });
    this.load();
  }

  onSearch(v: string): void { this.search = v; this.search$.next(v); }
  pickSegment(key: string): void { this.segment.set(key); this.page.set(1); this.load(); }
  go(p: number): void { this.page.set(p); this.load(); }

  private load(): void {
    this.loading.set(true);
    this.api.list(this.search.trim(), this.segment(), this.page()).subscribe({
      next: (r) => { this.items.set(r.items); this.totalPages.set(r.totalPages); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
}
