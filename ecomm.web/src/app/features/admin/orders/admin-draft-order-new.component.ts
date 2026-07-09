import { DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { DraftOrderService } from '../../../core/services/draft-order.service';
import { CustomerAdminService } from '../../../core/services/customer-admin.service';
import { CatalogService } from '../../../core/services/catalog.service';
import { CustomerListItem } from '../../../core/models/customer.model';
import { ProductListItem } from '../../../core/models/catalog.model';

interface Line { productId: number; name: string; unitPrice: number; quantity: number; }

@Component({
  selector: 'app-admin-draft-order-new',
  imports: [FormsModule, RouterLink, DecimalPipe],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <a routerLink="/admin/draft-orders" class="text-sm text-slate-500 hover:text-slate-800">← Draft orders</a>
      <h1 class="text-xl font-bold text-slate-900 mt-2 mb-5">Create order</h1>
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      <div class="grid lg:grid-cols-3 gap-6">
        <div class="lg:col-span-2 space-y-5">
          <!-- Products -->
          <div class="bg-white border border-slate-200 rounded-xl p-5">
            <h2 class="font-semibold text-slate-800 mb-2">Products</h2>
            <div class="relative">
              <input [(ngModel)]="productQuery" (ngModelChange)="productSearch$.next($event)" placeholder="Search products to add…" class="input w-full" />
              @if (productResults().length) {
                <div class="absolute z-10 left-0 right-0 mt-1 bg-white border border-slate-200 rounded-lg shadow-lg max-h-64 overflow-auto">
                  @for (p of productResults(); track p.productId) {
                    <button type="button" (click)="addProduct(p)" class="w-full text-left px-3 py-2 hover:bg-slate-50 flex justify-between text-sm">
                      <span class="text-slate-700">{{ p.name }}</span><span class="text-slate-500">₹{{ p.price | number:'1.0-2' }}</span>
                    </button>
                  }
                </div>
              }
            </div>

            <div class="divide-y divide-slate-100 mt-3">
              @for (l of lines(); track l.productId) {
                <div class="flex items-center gap-3 py-2 text-sm">
                  <span class="flex-1 text-slate-700">{{ l.name }}</span>
                  <span class="text-slate-400">₹{{ l.unitPrice | number:'1.0-2' }}</span>
                  <input type="number" [(ngModel)]="l.quantity" min="1" class="input w-16 py-1 text-center" />
                  <span class="w-20 text-right font-medium text-slate-800">₹{{ (l.unitPrice * l.quantity) | number:'1.0-2' }}</span>
                  <button type="button" (click)="removeLine(l)" class="text-red-500">×</button>
                </div>
              }
              @if (!lines().length) { <p class="text-sm text-slate-400 py-2">No products added yet.</p> }
            </div>
          </div>

          <!-- Discount + notes -->
          <div class="bg-white border border-slate-200 rounded-xl p-5 grid sm:grid-cols-2 gap-4">
            <label class="block"><span class="lbl">Discount code (optional)</span><input [(ngModel)]="couponCode" class="input w-full uppercase" placeholder="SAVE10" /></label>
            <label class="block sm:col-span-2"><span class="lbl">Notes (optional)</span><textarea [(ngModel)]="notes" rows="2" class="input w-full"></textarea></label>
          </div>
        </div>

        <!-- Customer -->
        <div class="space-y-5">
          <div class="bg-white border border-slate-200 rounded-xl p-5">
            <h2 class="font-semibold text-slate-800 mb-2">Customer</h2>
            @if (customer(); as c) {
              <div class="flex items-center justify-between">
                <div><div class="font-medium text-slate-800">{{ c.fullName || 'No name' }}</div><div class="text-xs text-slate-400">{{ c.email || c.phoneNumber }}</div></div>
                <button type="button" (click)="customer.set(null)" class="text-xs text-slate-500 hover:underline">Change</button>
              </div>
            } @else {
              <div class="relative">
                <input [(ngModel)]="customerQuery" (ngModelChange)="customerSearch$.next($event)" placeholder="Search customers…" class="input w-full" />
                @if (customerResults().length) {
                  <div class="absolute z-10 left-0 right-0 mt-1 bg-white border border-slate-200 rounded-lg shadow-lg max-h-64 overflow-auto">
                    @for (c of customerResults(); track c.userId) {
                      <button type="button" (click)="pickCustomer(c)" class="w-full text-left px-3 py-2 hover:bg-slate-50 text-sm">
                        <div class="text-slate-700">{{ c.fullName || 'No name' }}</div><div class="text-xs text-slate-400">{{ c.email || c.phoneNumber }}</div>
                      </button>
                    }
                  </div>
                }
              </div>
              <p class="text-xs text-slate-400 mt-2">No account yet? Add them in <a routerLink="/admin/customers/new" class="text-blue-600 hover:underline">Customers</a> first.</p>
            }
          </div>

          <button type="button" (click)="create()" [disabled]="saving() || !customer() || !lines().length" class="btn-primary w-full py-2.5">{{ saving() ? 'Creating…' : 'Create draft' }}</button>
          <p class="text-xs text-slate-400 text-center">Tax and shipping are calculated from the customer's saved address when you create the draft.</p>
        </div>
      </div>
    </div>
  `,
})
export class AdminDraftOrderNewComponent {
  private readonly api = inject(DraftOrderService);
  private readonly customers = inject(CustomerAdminService);
  private readonly catalog = inject(CatalogService);
  private readonly router = inject(Router);

  readonly customer = signal<CustomerListItem | null>(null);
  readonly customerResults = signal<CustomerListItem[]>([]);
  readonly productResults = signal<ProductListItem[]>([]);
  readonly lines = signal<Line[]>([]);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  customerQuery = '';
  productQuery = '';
  couponCode = '';
  notes = '';

  readonly customerSearch$ = new Subject<string>();
  readonly productSearch$ = new Subject<string>();

  constructor() {
    this.customerSearch$.pipe(debounceTime(250), distinctUntilChanged(),
      switchMap((q) => this.customers.list(q.trim(), 'all', 1, 6)))
      .subscribe((r) => this.customerResults.set(r.items));
    this.productSearch$.pipe(debounceTime(250), distinctUntilChanged(),
      switchMap((q) => this.catalog.getProducts({ search: q.trim(), pageSize: 8 })))
      .subscribe((r) => this.productResults.set(r.items));
  }

  pickCustomer(c: CustomerListItem): void { this.customer.set(c); this.customerResults.set([]); this.customerQuery = ''; }

  addProduct(p: ProductListItem): void {
    const existing = this.lines().find((l) => l.productId === p.productId);
    if (existing) { existing.quantity++; this.lines.set([...this.lines()]); }
    else this.lines.set([...this.lines(), { productId: p.productId, name: p.name, unitPrice: p.price, quantity: 1 }]);
    this.productResults.set([]); this.productQuery = '';
  }
  removeLine(l: Line): void { this.lines.set(this.lines().filter((x) => x !== l)); }

  create(): void {
    const c = this.customer(); if (!c || !this.lines().length) return;
    this.saving.set(true); this.error.set(null);
    this.api.create({
      customerUserId: c.userId,
      lines: this.lines().map((l) => ({ productId: l.productId, variantId: null, quantity: Number(l.quantity) || 1 })),
      couponCode: this.couponCode.trim() || null,
      notes: this.notes.trim() || null,
    }).subscribe({
      next: (d) => this.router.navigate(['/admin/draft-orders', d.orderId]),
      error: (e: unknown) => { this.saving.set(false); this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Could not create the draft.'); },
    });
  }
}
