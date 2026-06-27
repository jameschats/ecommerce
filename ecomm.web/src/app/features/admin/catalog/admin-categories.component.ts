import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SaveCategoryRequest } from '../../../core/models/admin-catalog.model';
import { Category } from '../../../core/models/catalog.model';
import { AdminCatalogService } from '../../../core/services/admin-catalog.service';

@Component({
  selector: 'app-admin-categories',
  imports: [FormsModule],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-4">Categories</h1>
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

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

  readonly categories = signal<Category[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

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

  remove(c: Category): void {
    if (!confirm(`Delete category "${c.name}"?`)) return;
    this.api.deleteCategory(c.categoryId).subscribe({
      next: () => this.load(),
      error: (e) => this.error.set(e?.error?.message ?? 'Delete failed.'),
    });
  }
}
