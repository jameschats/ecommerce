import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PagedResult } from '../../../core/models/api-response.model';
import { InventoryRow, InventoryTransaction, VariantInventory } from '../../../core/models/admin-catalog.model';
import { AdminCatalogService } from '../../../core/services/admin-catalog.service';

@Component({
  selector: 'app-admin-inventory',
  imports: [FormsModule, DatePipe],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <div class="flex items-center justify-between mb-4">
        <h1 class="text-xl font-bold text-slate-900">Inventory</h1>
        <div class="flex gap-2">
          <button type="button" (click)="export()" class="btn-ghost border border-slate-300">Export</button>
          <button type="button" (click)="template()" class="btn-ghost border border-slate-300">Template</button>
          <label class="btn-primary cursor-pointer">
            Import<input type="file" accept=".xlsx" (change)="onFile($event)" class="hidden" />
          </label>
        </div>
      </div>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (importResult(); as r) {
        <div class="mb-4 rounded-lg border border-slate-200 bg-white p-3 text-sm">
          Imported {{ r.success }}/{{ r.total }} rows.
          @if (r.errors.length) { <ul class="mt-2 text-red-600">@for (e of r.errors; track e.rowNumber) { <li>Row {{ e.rowNumber }}: {{ e.message }}</li> }</ul> }
        </div>
      }

      <div class="flex items-center gap-3 mb-4">
        <input [(ngModel)]="search" (keyup.enter)="applySearch()" placeholder="Search products…" class="input max-w-sm" />
        <label class="flex items-center gap-2 text-sm text-slate-600"><input type="checkbox" [(ngModel)]="lowStockOnly" (ngModelChange)="applySearch()" /> Low stock only</label>
      </div>

      <div class="bg-white border border-slate-200 rounded-xl overflow-hidden">
        @if (loading()) { <div class="p-10 text-center text-slate-400">Loading…</div> }
        @else {
          <table class="w-full text-sm">
            <thead class="bg-slate-50 text-slate-500 text-left">
              <tr><th class="px-4 py-2">Product</th><th class="px-4 py-2 w-20">Reserved</th><th class="px-4 py-2 w-28">Available</th><th class="px-4 py-2 w-28">Reorder at</th><th class="px-4 py-2 w-44"></th></tr>
            </thead>
            <tbody>
              @for (row of rows(); track row.productId) {
                <tr class="border-t border-slate-100" [class.bg-orange-50]="row.isLowStock">
                  <td class="px-4 py-2">
                    <div class="text-slate-800">{{ row.name }}</div>
                    <div class="text-xs text-slate-400">{{ row.sku }} @if (row.isLowStock) { <span class="text-orange-600 font-medium">· low stock</span> }</div>
                  </td>
                  <td class="px-4 py-2 text-slate-500">{{ row.reservedQty }}</td>
                  <td class="px-4 py-2"><input type="number" [(ngModel)]="row.availableQty" [name]="'a'+row.productId" class="input w-24" /></td>
                  <td class="px-4 py-2"><input type="number" [(ngModel)]="row.reorderLevel" [name]="'r'+row.productId" class="input w-24" /></td>
                  <td class="px-4 py-2 text-right whitespace-nowrap">
                    <button type="button" (click)="save(row)" [disabled]="savingId() === row.productId" class="text-primary hover:underline mr-3">{{ savingId() === row.productId ? '…' : 'Save' }}</button>
                    @if (row.hasVariants) { <button type="button" (click)="toggleVariants(row)" class="text-slate-500 hover:text-slate-800 mr-3">Variants</button> }
                    <button type="button" (click)="viewHistory(row)" class="text-slate-500 hover:text-slate-800">History</button>
                  </td>
                </tr>
                @if (expandedId() === row.productId) {
                  <tr class="bg-slate-50/60 border-t border-slate-100">
                    <td colspan="5" class="px-4 py-3">
                      <div class="text-xs font-semibold text-slate-500 mb-2">Variant stock</div>
                      @if (variantsLoading()) { <div class="text-slate-400 text-sm">Loading…</div> }
                      @else {
                        <div class="space-y-2">
                          @for (v of variants(); track v.productVariantId) {
                            <div class="flex items-center gap-3 text-sm" [class.text-orange-600]="v.isLowStock">
                              <span class="w-48 text-slate-700">{{ v.name || v.sku }}</span>
                              <span class="text-slate-400 w-20">res {{ v.reservedQty }}</span>
                              <label class="text-slate-500">Avail <input type="number" [(ngModel)]="v.availableQty" [name]="'va'+v.productVariantId" class="input w-20 inline-block" /></label>
                              <label class="text-slate-500">Reorder <input type="number" [(ngModel)]="v.reorderLevel" [name]="'vr'+v.productVariantId" class="input w-20 inline-block" /></label>
                              <button type="button" (click)="saveVariant(row.productId, v)" [disabled]="savingVariantId() === v.productVariantId" class="text-primary hover:underline">{{ savingVariantId() === v.productVariantId ? '…' : 'Save' }}</button>
                            </div>
                          }
                          @if (variants().length === 0) { <div class="text-slate-400 text-sm">No variants.</div> }
                        </div>
                      }
                    </td>
                  </tr>
                }
              }
              @if (rows().length === 0) { <tr><td colspan="5" class="px-4 py-10 text-center text-slate-400">No products.</td></tr> }
            </tbody>
          </table>
        }
      </div>

      @if ((result()?.totalPages ?? 0) > 1) {
        <div class="flex justify-center gap-1 mt-6">
          @for (pg of pages; track pg) {
            <button type="button" (click)="goTo(pg)" class="w-9 h-9 rounded-lg text-sm border" [class]="pg === page ? 'bg-primary text-white border-primary' : 'bg-white text-slate-600 border-slate-300'">{{ pg }}</button>
          }
        </div>
      }
    </div>

    <!-- History modal -->
    @if (historyName(); as name) {
      <div class="fixed inset-0 bg-black/40 z-40 flex items-center justify-center p-4" (click)="closeHistory()">
        <div class="bg-white rounded-2xl w-full max-w-lg max-h-[80vh] overflow-auto p-5" (click)="$event.stopPropagation()">
          <div class="flex items-center justify-between mb-3">
            <h2 class="font-semibold text-slate-800">Stock history — {{ name }}</h2>
            <button type="button" (click)="closeHistory()" class="text-slate-400 hover:text-slate-700 text-xl">×</button>
          </div>
          @if (historyLoading()) { <div class="py-8 text-center text-slate-400">Loading…</div> }
          @else if (history().length === 0) { <div class="py-8 text-center text-slate-400">No transactions yet.</div> }
          @else {
            <table class="w-full text-sm">
              <thead class="text-slate-400 text-left"><tr><th class="py-1">Type</th><th class="py-1">Change</th><th class="py-1">Balance</th><th class="py-1">When</th></tr></thead>
              <tbody>
                @for (t of history(); track t.inventoryTransactionId) {
                  <tr class="border-t border-slate-100">
                    <td class="py-1.5 text-slate-700">{{ t.transactionType }}</td>
                    <td class="py-1.5" [class]="t.changeQty >= 0 ? 'text-green-600' : 'text-red-600'">{{ t.changeQty > 0 ? '+' : '' }}{{ t.changeQty }}</td>
                    <td class="py-1.5 text-slate-500">{{ t.balanceAfter }}</td>
                    <td class="py-1.5 text-slate-400">{{ t.createdAt | date:'dd MMM, HH:mm' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
    }
  `,
})
export class AdminInventoryComponent implements OnInit {
  private readonly api = inject(AdminCatalogService);

  readonly rows = signal<InventoryRow[]>([]);
  readonly result = signal<PagedResult<InventoryRow> | null>(null);
  readonly loading = signal(true);
  readonly savingId = signal<number | null>(null);
  readonly message = signal<string | null>(null);
  readonly importResult = signal<{ total: number; success: number; failed: number; errors: { rowNumber: number; message: string }[] } | null>(null);

  // variants
  readonly expandedId = signal<number | null>(null);
  readonly variants = signal<VariantInventory[]>([]);
  readonly variantsLoading = signal(false);
  readonly savingVariantId = signal<number | null>(null);

  // history
  readonly historyName = signal<string | null>(null);
  readonly history = signal<InventoryTransaction[]>([]);
  readonly historyLoading = signal(false);

  search = '';
  lowStockOnly = false;
  page = 1;

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.api.listInventory(this.search, this.lowStockOnly, this.page).subscribe({
      next: (r) => { this.result.set(r); this.rows.set(r.items.map((x) => ({ ...x }))); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  applySearch(): void { this.page = 1; this.expandedId.set(null); this.load(); }
  goTo(p: number): void { this.page = p; this.expandedId.set(null); this.load(); }

  get pages(): number[] {
    const r = this.result();
    return r ? Array.from({ length: r.totalPages }, (_, i) => i + 1) : [];
  }

  save(row: InventoryRow): void {
    this.savingId.set(row.productId);
    this.message.set(null);
    this.api.setStock(row.productId, row.availableQty, row.reorderLevel).subscribe({
      next: (updated) => { row.isLowStock = updated.isLowStock; this.savingId.set(null); this.message.set(`${row.name} updated.`); },
      error: () => this.savingId.set(null),
    });
  }

  toggleVariants(row: InventoryRow): void {
    if (this.expandedId() === row.productId) { this.expandedId.set(null); return; }
    this.expandedId.set(row.productId);
    this.variants.set([]);
    this.variantsLoading.set(true);
    this.api.variantInventory(row.productId).subscribe({
      next: (v) => { this.variants.set(v.map((x) => ({ ...x }))); this.variantsLoading.set(false); },
      error: () => this.variantsLoading.set(false),
    });
  }

  saveVariant(productId: number, v: VariantInventory): void {
    this.savingVariantId.set(v.productVariantId);
    this.api.setVariantStock(productId, v.productVariantId, v.availableQty, v.reorderLevel).subscribe({
      next: (updated) => { v.isLowStock = updated.isLowStock; this.savingVariantId.set(null); },
      error: () => this.savingVariantId.set(null),
    });
  }

  viewHistory(row: InventoryRow): void {
    this.historyName.set(row.name);
    this.history.set([]);
    this.historyLoading.set(true);
    this.api.inventoryTransactions(row.productId).subscribe({
      next: (t) => { this.history.set(t); this.historyLoading.set(false); },
      error: () => this.historyLoading.set(false),
    });
  }

  closeHistory(): void { this.historyName.set(null); }

  onFile(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.importResult.set(null);
    this.api.importInventory(file).subscribe({
      next: (r) => { this.importResult.set(r); this.load(); },
      error: () => this.message.set('Import failed.'),
    });
  }

  export(): void { this.api.exportInventory().subscribe((b) => this.download(b, 'inventory.xlsx')); }
  template(): void { this.api.inventoryTemplate().subscribe((b) => this.download(b, 'inventory-template.xlsx')); }

  private download(blob: Blob, name: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = name; a.click();
    URL.revokeObjectURL(url);
  }
}
