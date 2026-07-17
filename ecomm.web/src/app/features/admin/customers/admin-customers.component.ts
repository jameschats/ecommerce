import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { CustomerAdminService } from '../../../core/services/customer-admin.service';
import { CustomerImportResult, CustomerListItem, CustomerSegment, TagCount } from '../../../core/models/customer.model';

@Component({
  selector: 'app-admin-customers',
  imports: [FormsModule, RouterLink, DecimalPipe, DatePipe],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <div class="flex items-center justify-between mb-1">
        <h1 class="text-xl font-bold text-slate-900">Customers</h1>
        <div class="flex items-center gap-2">
          <button type="button" (click)="showImport.set(!showImport())" class="px-3 py-2 rounded-lg border border-slate-300 text-sm hover:bg-slate-50">Import CSV</button>
          <a routerLink="/admin/customers/new" class="btn-primary">+ Add customer</a>
        </div>
      </div>
      <p class="text-sm text-slate-500 mb-4">Everyone who has an account or bought from your store.</p>

      <!-- Import panel -->
      @if (showImport()) {
        <div class="bg-white border border-slate-200 rounded-xl p-4 mb-4">
          <div class="flex items-center justify-between">
            <div>
              <div class="text-sm font-medium text-slate-800">Import customers from CSV</div>
              <p class="text-xs text-slate-500 mt-0.5">Columns: <code>name, email, phone, tags, notes</code> (+ optional <code>email marketing, sms marketing, whatsapp marketing</code>). Matches on email — existing customers are updated, tags are merged.</p>
            </div>
            <button type="button" (click)="downloadTemplate()" class="text-sm text-primary hover:underline whitespace-nowrap ml-3">Download template</button>
          </div>
          <div class="flex items-center gap-3 mt-3">
            <input #fileInput type="file" accept=".csv" (change)="onFile($event)" [disabled]="importing()"
              class="text-sm file:mr-3 file:px-3 file:py-1.5 file:rounded-lg file:border-0 file:bg-slate-100 file:text-slate-700 hover:file:bg-slate-200" />
            @if (importing()) { <span class="text-sm text-slate-400">Importing…</span> }
          </div>
          @if (importResult(); as r) {
            <div class="mt-3 text-sm rounded-lg bg-slate-50 border border-slate-200 p-3">
              <div class="text-slate-700"><span class="font-medium text-green-600">{{ r.created }} added</span> · <span class="font-medium text-blue-600">{{ r.updated }} updated</span>@if (r.skipped) { · <span class="font-medium text-amber-600">{{ r.skipped }} skipped</span> } <span class="text-slate-400">of {{ r.total }} rows</span></div>
              @if (r.errors.length) {
                <ul class="mt-2 text-xs text-amber-700 list-disc list-inside space-y-0.5 max-h-32 overflow-y-auto">
                  @for (e of r.errors; track e) { <li>{{ e }}</li> }
                </ul>
              }
            </div>
          }
        </div>
      }

      <!-- Segments -->
      <div class="flex flex-wrap gap-2 mb-3">
        @for (s of segments(); track s.key) {
          <button type="button" (click)="pickSegment(s.key)"
            class="text-sm px-3 py-1.5 rounded-full border transition"
            [class]="segment() === s.key ? 'border-primary bg-primary/5 text-primary font-medium' : 'border-slate-200 text-slate-600 hover:bg-slate-50'">
            {{ s.label }} <span class="text-slate-400">{{ s.count }}</span>
          </button>
        }
      </div>

      <!-- Tag filter -->
      @if (tags().length) {
        <div class="flex flex-wrap items-center gap-2 mb-4">
          <span class="text-xs text-slate-400">Tags:</span>
          @for (t of tags(); track t.tag) {
            <button type="button" (click)="pickTag(t.tag)"
              class="text-xs px-2.5 py-1 rounded-full border transition"
              [class]="tag() === t.tag ? 'border-primary bg-primary/5 text-primary font-medium' : 'border-slate-200 text-slate-600 hover:bg-slate-50'">
              {{ t.tag }} <span class="text-slate-400">{{ t.count }}</span>
            </button>
          }
          @if (tag()) { <button type="button" (click)="pickTag(tag())" class="text-xs text-slate-400 hover:text-slate-600 underline">clear</button> }
        </div>
      }

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
                  @if (c.tags.length) {
                    <div class="flex flex-wrap gap-1 mt-1">
                      @for (t of c.tags; track t) { <span class="text-[10px] bg-slate-100 text-slate-500 rounded px-1.5 py-0.5">{{ t }}</span> }
                    </div>
                  }
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
  readonly tags = signal<TagCount[]>([]);
  readonly loading = signal(true);
  readonly page = signal(1);
  readonly totalPages = signal(1);
  readonly segment = signal('all');
  readonly tag = signal('');
  readonly showImport = signal(false);
  readonly importing = signal(false);
  readonly importResult = signal<CustomerImportResult | null>(null);
  search = '';
  private readonly search$ = new Subject<string>();

  ngOnInit(): void {
    this.refreshFacets();
    this.search$.pipe(debounceTime(250), distinctUntilChanged()).subscribe(() => { this.page.set(1); this.load(); });
    this.load();
  }

  onSearch(v: string): void { this.search = v; this.search$.next(v); }
  pickSegment(key: string): void { this.segment.set(key); this.page.set(1); this.load(); }
  pickTag(t: string): void { this.tag.set(this.tag() === t ? '' : t); this.page.set(1); this.load(); }
  go(p: number): void { this.page.set(p); this.load(); }

  onFile(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.importing.set(true);
    this.importResult.set(null);
    this.api.import(file).subscribe({
      next: (r) => { this.importResult.set(r); this.importing.set(false); input.value = ''; this.page.set(1); this.load(); this.refreshFacets(); },
      error: () => { this.importing.set(false); input.value = ''; },
    });
  }

  downloadTemplate(): void {
    const csv = 'name,email,phone,tags,notes,email marketing,sms marketing,whatsapp marketing\n'
      + 'Asha Rao,asha@example.com,9876543210,"vip,wholesale",Prefers COD,yes,no,no\n';
    const blob = new Blob([csv], { type: 'text/csv' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'customers-template.csv';
    a.click();
    URL.revokeObjectURL(a.href);
  }

  private refreshFacets(): void {
    this.api.segments().subscribe((s) => this.segments.set(s));
    this.api.tags().subscribe((t) => this.tags.set(t));
  }

  private load(): void {
    this.loading.set(true);
    this.api.list(this.search.trim(), this.segment(), this.page(), 20, this.tag()).subscribe({
      next: (r) => { this.items.set(r.items); this.totalPages.set(r.totalPages); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
}
