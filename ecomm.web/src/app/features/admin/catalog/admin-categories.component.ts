import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SaveCategoryRequest } from '../../../core/models/admin-catalog.model';
import { Category } from '../../../core/models/catalog.model';
import { AdminCatalogService } from '../../../core/services/admin-catalog.service';
import { API_BASE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';

/** What a cascade delete would destroy — every number the dialog has to say out loud. */
interface DeleteImpact {
  categoryId: number; name: string;
  liveProducts: number; deletedProducts: number;
  orders: number; invoices: number; payments: number; orderValue: number;
  otherCategoryProductsAffected: number; canDeleteOutright: boolean;
}

@Component({
  selector: 'app-admin-categories',
  imports: [FormsModule, DecimalPipe],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-4">Categories</h1>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      <!--
        Opened only when the safe delete is refused. It states what would be destroyed and
        makes the name be typed, because a Yes button beside a Delete link is one slip away
        from taking invoices and payments with it.
      -->
      @if (impact(); as i) {
        <div class="fixed inset-0 bg-black/40 flex items-start justify-center p-6 overflow-y-auto z-50"
             (click)="impact.set(null)">
          <div class="bg-white rounded-xl border border-slate-200 p-6 w-full max-w-lg mt-12" (click)="$event.stopPropagation()">
            <h2 class="text-lg font-bold text-slate-900">Delete “{{ i.name }}”?</h2>
            <p class="text-sm text-slate-600 mt-1">This category cannot be removed on its own. Deleting it takes:</p>

            <ul class="mt-4 space-y-1.5 text-sm">
              @if (i.liveProducts) { <li class="flex justify-between"><span class="text-slate-700">Products</span><span class="font-semibold text-slate-900">{{ i.liveProducts }}</span></li> }
              @if (i.deletedProducts) { <li class="flex justify-between"><span class="text-slate-700">Already-deleted products</span><span class="font-semibold text-slate-900">{{ i.deletedProducts }}</span></li> }
              @if (i.orders) { <li class="flex justify-between"><span class="text-slate-700">Orders</span><span class="font-semibold text-slate-900">{{ i.orders }}</span></li> }
              @if (i.invoices) { <li class="flex justify-between text-red-700"><span>Invoices</span><span class="font-semibold">{{ i.invoices }}</span></li> }
              @if (i.payments) { <li class="flex justify-between text-red-700"><span>Payment records</span><span class="font-semibold">{{ i.payments }}</span></li> }
              @if (i.orderValue) { <li class="flex justify-between border-t border-slate-100 pt-1.5"><span class="text-slate-700">Order value removed</span><span class="font-semibold text-slate-900">₹{{ i.orderValue | number: '1.2-2' }}</span></li> }
            </ul>

            @if (i.otherCategoryProductsAffected) {
              <p class="mt-3 text-[13px] text-amber-900 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
                Those orders also contain <strong>{{ i.otherCategoryProductsAffected }}</strong>
                product(s) from other categories. Their sales history goes too.
              </p>
            }

            @if (i.invoices) {
              <p class="mt-3 text-[13px] text-red-900 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
                This removes issued invoices. Fine while this is test data — but invoices are
                records you are expected to keep once you are trading for real.
              </p>
            }

            <label class="block mt-4 text-sm">
              <span class="text-slate-600">Type <strong class="text-slate-900">{{ i.name }}</strong> to confirm</span>
              <input [(ngModel)]="confirmName" class="input w-full mt-1" placeholder="Category name" />
            </label>

            @if (error()) { <p class="text-sm text-red-600 mt-2">{{ error() }}</p> }

            <div class="flex items-center gap-2 mt-4">
              <button type="button" (click)="cascadeDelete()" [disabled]="cascading() || confirmName.trim() !== i.name"
                      class="px-4 py-2 rounded-lg bg-red-600 text-white text-sm font-medium disabled:opacity-40">
                {{ cascading() ? 'Deleting…' : 'Delete everything listed' }}
              </button>
              <button type="button" (click)="impact.set(null)" class="px-4 py-2 rounded-lg border border-slate-300 text-sm">Cancel</button>
            </div>
          </div>
        </div>
      }

      <div class="grid md:grid-cols-3 gap-6">
        <!-- Form -->
        <div class="bg-white border border-slate-200 rounded-xl p-4 h-fit">
          <h2 class="font-medium text-slate-800 mb-3">{{ form.categoryId ? 'Edit' : 'New' }} category</h2>
          <div class="space-y-3">
            <input [(ngModel)]="form.name" placeholder="Name" class="input" />
            <select [(ngModel)]="form.parentCategoryId" class="input">
              <option [ngValue]="null">— No parent —</option>
              @for (c of categories(); track c.categoryId) {
                @if (c.categoryId !== form.categoryId) { <option [ngValue]="c.categoryId">{{ c.name }}</option> }
              }
            </select>
            <input [(ngModel)]="form.imageUrl" placeholder="Image URL (optional)" class="input" />
            <textarea [(ngModel)]="form.description" placeholder="Description" rows="2" class="input"></textarea>
            <div class="flex items-center gap-3">
              <input type="number" [(ngModel)]="form.displayOrder" placeholder="Order" class="input w-24" />
              <label class="flex items-center gap-2 text-sm text-slate-600"><input type="checkbox" [(ngModel)]="form.isActive" /> Active</label>
            </div>
            <div class="flex gap-2 pt-1">
              <button type="button" (click)="save()" [disabled]="saving()" class="btn-primary">{{ saving() ? 'Saving…' : 'Save' }}</button>
              @if (form.categoryId) { <button type="button" (click)="reset()" class="btn-ghost">Cancel</button> }
            </div>
          </div>
        </div>

        <!-- List -->
        <div class="md:col-span-2 bg-white border border-slate-200 rounded-xl overflow-hidden">
          @if (loading()) { <div class="p-8 text-center text-slate-400">Loading…</div> }
          @else {
            <table class="w-full text-sm">
              <thead class="bg-slate-50 text-slate-500 text-left">
                <tr><th class="px-4 py-2">Name</th><th class="px-4 py-2">Slug</th><th class="px-4 py-2">Active</th><th class="px-4 py-2"></th></tr>
              </thead>
              <tbody>
                @for (c of categories(); track c.categoryId) {
                  <tr class="border-t border-slate-100">
                    <td class="px-4 py-2 text-slate-800">{{ c.name }}</td>
                    <td class="px-4 py-2 text-slate-400">{{ c.slug }}</td>
                    <td class="px-4 py-2">{{ c.isActive ? '✓' : '—' }}</td>
                    <td class="px-4 py-2 text-right whitespace-nowrap">
                      <button type="button" (click)="edit(c)" class="text-blue-600 hover:underline mr-3">Edit</button>
                      <button type="button" (click)="remove(c)" class="text-red-600 hover:underline">Delete</button>
                    </td>
                  </tr>
                }
                @if (categories().length === 0) { <tr><td colspan="4" class="px-4 py-8 text-center text-slate-400">No categories yet.</td></tr> }
              </tbody>
            </table>
          }
        </div>
      </div>
    </div>
  `,
})
export class AdminCategoriesComponent implements OnInit {
  private readonly api = inject(AdminCatalogService);
  private readonly http = inject(HttpClient);

  readonly categories = signal<Category[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);
  readonly impact = signal<DeleteImpact | null>(null);
  readonly cascading = signal(false);
  confirmName = '';

  form: SaveCategoryRequest & { categoryId?: number } = this.blank();

  ngOnInit(): void {
    this.load();
  }

  private blank(): SaveCategoryRequest & { categoryId?: number } {
    return { name: '', parentCategoryId: null, imageUrl: '', description: '', displayOrder: 0, isActive: true };
  }

  load(): void {
    this.loading.set(true);
    this.api.listCategories().subscribe({
      next: (c) => { this.categories.set(c); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  edit(c: Category): void {
    this.form = {
      categoryId: c.categoryId, name: c.name, parentCategoryId: c.parentCategoryId,
      imageUrl: c.imageUrl, description: c.description, displayOrder: c.displayOrder, isActive: c.isActive,
    };
  }

  reset(): void {
    this.form = this.blank();
  }

  save(): void {
    this.saving.set(true);
    this.error.set(null);
    const { categoryId, ...body } = this.form;
    const op = categoryId ? this.api.updateCategory(categoryId, body) : this.api.createCategory(body);
    op.subscribe({
      next: () => { this.saving.set(false); this.reset(); this.load(); },
      error: (e) => { this.saving.set(false); this.error.set(e?.error?.message ?? 'Save failed.'); },
    });
  }

  /**
   * Tries the safe delete first. It succeeds outright for a category nothing depends on, which
   * is the common case and should not need a dialog. Only when the server refuses does the
   * impact panel open, showing what is holding it and offering the cascade.
   */
  remove(c: Category): void {
    if (!confirm(`Delete category "${c.name}"?`)) return;
    this.error.set(null);

    this.api.deleteCategory(c.categoryId).subscribe({
      next: () => this.load(),
      error: () => this.openImpact(c),
    });
  }

  private openImpact(c: Category): void {
    this.http.get<ApiResponse<DeleteImpact>>(
      `${API_BASE_URL}/admin/categories/${c.categoryId}/delete-impact`).subscribe({
        next: (r) => { this.impact.set(r.data ?? null); this.confirmName = ''; },
        error: (e) => this.error.set(e?.error?.message ?? 'Delete failed.'),
      });
  }

  cascadeDelete(): void {
    const i = this.impact();
    if (!i) return;

    this.cascading.set(true);
    this.error.set(null);
    this.http.delete<ApiResponse<DeleteImpact>>(
      `${API_BASE_URL}/admin/categories/${i.categoryId}/cascade?confirm=${encodeURIComponent(this.confirmName)}`)
      .subscribe({
        next: (r) => {
          this.cascading.set(false);
          this.impact.set(null);
          this.message.set(r.message ?? 'Deleted.');
          setTimeout(() => this.message.set(null), 6000);
          this.load();
        },
        error: (e) => {
          this.cascading.set(false);
          // 403 here means a Catalogue manager token: this page is catalog.manage but the
          // cascade deliberately demands settings.manage. Say that rather than 'Could not delete'.
          this.error.set(e?.status === 403
            ? 'Only an administrator can delete a category along with its orders and invoices.'
            : e?.error?.message ?? 'Could not delete.');
        },
      });
  }
}
