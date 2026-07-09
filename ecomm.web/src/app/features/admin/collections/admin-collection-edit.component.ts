import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { CollectionAdminService, CollectionProduct, CollectionRule, SaveCollection } from '../../../core/services/collection-admin.service';
import { CatalogService } from '../../../core/services/catalog.service';
import { ProductListItem } from '../../../core/models/catalog.model';

interface Member { productId: number; name: string; price: number; }

@Component({
  selector: 'app-admin-collection-edit',
  imports: [FormsModule, RouterLink, DecimalPipe],
  template: `
    <div class="max-w-2xl mx-auto p-6">
      <a routerLink="/admin/collections" class="text-sm text-slate-500 hover:text-slate-800">← Collections</a>
      <div class="flex items-center justify-between mt-2 mb-5">
        <h1 class="text-xl font-bold text-slate-900">{{ id ? 'Edit collection' : 'New collection' }}</h1>
        @if (id) { <button type="button" (click)="remove()" class="text-sm text-red-500 hover:underline">Delete</button> }
      </div>
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      <div class="space-y-5">
        <div class="bg-white border border-slate-200 rounded-xl p-5 grid sm:grid-cols-2 gap-4">
          <label class="block sm:col-span-2"><span class="lbl">Name</span><input class="input" [(ngModel)]="form.name" placeholder="Summer sale" /></label>
          <label class="block sm:col-span-2"><span class="lbl">Description</span><textarea class="input" rows="2" [(ngModel)]="form.description"></textarea></label>
          <label class="block"><span class="lbl">Type</span>
            <select class="input" [(ngModel)]="form.collectionType">
              <option value="Manual">Manual (pick products)</option>
              <option value="Automated">Automated (by rules)</option>
            </select></label>
          <label class="flex items-center gap-2 mt-6"><input type="checkbox" [(ngModel)]="form.isActive" /> <span class="text-sm text-slate-700">Active</span></label>
        </div>

        <!-- Manual members -->
        @if (form.collectionType === 'Manual') {
          <div class="bg-white border border-slate-200 rounded-xl p-5">
            <h2 class="font-semibold text-slate-800 mb-2">Products</h2>
            <div class="relative">
              <input [(ngModel)]="productQuery" (ngModelChange)="productSearch$.next($event)" placeholder="Search products to add…" class="input w-full" />
              @if (productResults().length) {
                <div class="absolute z-10 left-0 right-0 mt-1 bg-white border border-slate-200 rounded-lg shadow-lg max-h-56 overflow-auto">
                  @for (p of productResults(); track p.productId) {
                    <button type="button" (click)="addMember(p)" class="w-full text-left px-3 py-2 hover:bg-slate-50 flex justify-between text-sm"><span>{{ p.name }}</span><span class="text-slate-400">₹{{ p.price | number:'1.0-2' }}</span></button>
                  }
                </div>
              }
            </div>
            <div class="divide-y divide-slate-100 mt-3">
              @for (m of members(); track m.productId) {
                <div class="flex items-center justify-between py-2 text-sm"><span class="text-slate-700">{{ m.name }}</span><button type="button" (click)="removeMember(m)" class="text-red-500">×</button></div>
              }
              @if (!members().length) { <p class="text-sm text-slate-400 py-2">No products yet.</p> }
            </div>
          </div>
        } @else {
          <!-- Automated rules -->
          <div class="bg-white border border-slate-200 rounded-xl p-5">
            <div class="flex items-center justify-between mb-2">
              <h2 class="font-semibold text-slate-800">Rules</h2>
              <select class="input py-1 text-sm w-auto" [(ngModel)]="form.matchType">
                <option value="All">Match all</option>
                <option value="Any">Match any</option>
              </select>
            </div>
            @for (r of rules(); track $index) {
              <div class="flex gap-2 mb-2">
                <select class="input py-1 text-sm" [(ngModel)]="r.field">
                  <option value="tag">Tag</option><option value="type">Product type</option><option value="title">Title contains</option>
                  <option value="price">Price</option><option value="category">Category id</option><option value="featured">Is featured</option>
                </select>
                @if (r.field === 'price') {
                  <select class="input py-1 text-sm w-24" [(ngModel)]="r.op"><option value="gte">≥</option><option value="lte">≤</option><option value="eq">=</option></select>
                }
                @if (r.field !== 'featured') { <input class="input py-1 text-sm flex-1" [(ngModel)]="r.value" placeholder="value" /> }
                <button type="button" (click)="removeRule($index)" class="text-red-500 px-2">×</button>
              </div>
            }
            <button type="button" (click)="addRule()" class="text-sm text-blue-600 hover:underline">+ Add rule</button>
            <p class="text-xs text-slate-400 mt-2">Products matching these rules are included automatically. Tag rules need product tags (set on the product).</p>
          </div>
        }

        <!-- SEO -->
        <div class="bg-white border border-slate-200 rounded-xl p-5">
          <label class="lbl">Search engine listing (SEO)</label>
          <input class="input mb-2" [(ngModel)]="form.metaTitle" placeholder="Meta title" />
          <textarea class="input" rows="2" [(ngModel)]="form.metaDescription" placeholder="Meta description"></textarea>
        </div>

        <button type="button" (click)="save()" [disabled]="saving()" class="btn-primary px-5 py-2.5">{{ saving() ? 'Saving…' : 'Save collection' }}</button>
      </div>
    </div>
  `,
})
export class AdminCollectionEditComponent implements OnInit {
  private readonly api = inject(CollectionAdminService);
  private readonly catalog = inject(CatalogService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  id = 0;
  form: SaveCollection = { name: '', slug: null, description: null, imageUrl: null, collectionType: 'Manual', matchType: 'All', rules: [], metaTitle: null, metaDescription: null, isActive: true };
  readonly rules = signal<CollectionRule[]>([]);
  readonly members = signal<Member[]>([]);
  readonly productResults = signal<ProductListItem[]>([]);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  productQuery = '';
  readonly productSearch$ = new Subject<string>();

  constructor() {
    this.productSearch$.pipe(debounceTime(250), distinctUntilChanged(),
      switchMap((q) => this.catalog.getProducts({ search: q.trim(), pageSize: 8 })))
      .subscribe((r) => this.productResults.set(r.items));
  }

  ngOnInit(): void {
    const p = this.route.snapshot.paramMap.get('id');
    if (p && p !== 'new') {
      this.id = Number(p);
      this.api.get(this.id).subscribe((c) => {
        this.form = { name: c.name, slug: c.slug, description: c.description, imageUrl: c.imageUrl, collectionType: c.collectionType, matchType: c.matchType, rules: c.rules, metaTitle: c.metaTitle, metaDescription: c.metaDescription, isActive: c.isActive };
        this.rules.set([...c.rules]);
      });
      this.api.members(this.id).subscribe((m) => this.members.set(m.map((x: CollectionProduct) => ({ productId: x.productId, name: x.name, price: x.price }))));
    }
  }

  addRule(): void { this.rules.set([...this.rules(), { field: 'tag', op: 'eq', value: '' }]); }
  removeRule(i: number): void { this.rules.set(this.rules().filter((_, idx) => idx !== i)); }

  addMember(p: ProductListItem): void {
    if (!this.members().some((m) => m.productId === p.productId)) this.members.set([...this.members(), { productId: p.productId, name: p.name, price: p.price }]);
    this.productResults.set([]); this.productQuery = '';
  }
  removeMember(m: Member): void { this.members.set(this.members().filter((x) => x.productId !== m.productId)); }

  save(): void {
    this.saving.set(true); this.error.set(null);
    const body: SaveCollection = { ...this.form, rules: this.form.collectionType === 'Automated' ? this.rules() : null };
    const obs = this.id ? this.api.update(this.id, body) : this.api.create(body);
    obs.subscribe({
      next: (c) => {
        const id = c.collectionId;
        if (body.collectionType === 'Manual') {
          this.api.setMembers(id, this.members().map((m) => m.productId)).subscribe({ next: () => this.router.navigateByUrl('/admin/collections'), error: (e: unknown) => this.fail(e) });
        } else { this.router.navigateByUrl('/admin/collections'); }
      },
      error: (e: unknown) => this.fail(e),
    });
  }
  private fail(e: unknown): void { this.saving.set(false); this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Could not save.'); }

  remove(): void {
    if (!confirm('Delete this collection?')) return;
    this.api.remove(this.id).subscribe({ next: () => this.router.navigateByUrl('/admin/collections') });
  }
}
