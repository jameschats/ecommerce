import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  AttributeDef, ProductImageInput, SaveProductCustomFieldInput, SaveProductRequest, SaveVariantRequest,
} from '../../../core/models/admin-catalog.model';
import { Brand, Category, ProductDetail, ProductVariant } from '../../../core/models/catalog.model';
import { AdminCatalogService } from '../../../core/services/admin-catalog.service';
import { MediaService } from '../../../core/services/media.service';
import { ProductSupplierInput, Supplier } from '../../../core/models/supplier.model';
import { SupplierService } from '../../../core/services/supplier.service';

@Component({
  selector: 'app-admin-product-form',
  imports: [FormsModule, RouterLink],
  templateUrl: './admin-product-form.component.html',
})
export class AdminProductFormComponent implements OnInit {
  private readonly api = inject(AdminCatalogService);
  private readonly media = inject(MediaService);
  private readonly suppliers = inject(SupplierService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly uploading = signal(false);

  readonly productId = signal<number | null>(null);
  readonly isEdit = computed(() => this.productId() !== null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);

  readonly categories = signal<Category[]>([]);
  readonly brands = signal<Brand[]>([]);
  readonly attributeDefs = signal<AttributeDef[]>([]);
  readonly variants = signal<ProductVariant[]>([]);

  // --- Options -> variants (Wix-style generator) ---
  readonly optionGroups = signal<{ name: string; valuesText: string }[]>([{ name: '', valuesText: '' }]);
  readonly generatingVariants = signal(false);
  readonly savingVariantId = signal<number | null>(null);
  readonly deletingVariantId = signal<number | null>(null);

  // --- Custom text fields (Wix-style personalization) ---
  readonly customFields = signal<SaveProductCustomFieldInput[]>([]);
  readonly savingCustomFields = signal(false);

  readonly allSuppliers = signal<Supplier[]>([]);
  productSuppliers: ProductSupplierInput[] = [];

  form: SaveProductRequest = this.blank();
  attrValues: Record<number, string> = {};

  ngOnInit(): void {
    this.api.listCategories().subscribe((c) => this.categories.set(c));
    this.api.listBrands().subscribe((b) => this.brands.set(b));
    this.api.listAttributes().subscribe((a) => this.attributeDefs.set(a));

    this.suppliers.list().subscribe((s) => this.allSuppliers.set(s));

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.productId.set(+id);
      this.loadProduct(+id);
      this.loadProductSuppliers(+id);
    } else {
      this.loading.set(false);
    }
  }

  /**
   * Discount shown against the MRP. Derived, never stored — the storefront computes it the
   * same way, so the number here is the number a customer sees.
   */
  readonly discountPct = signal<number>(0);

  private recomputeDiscount(): void {
    const mrp = this.form.compareAtPrice ?? 0;
    const price = this.form.price ?? 0;
    this.discountPct.set(mrp > 0 && mrp > price ? Math.round(((mrp - price) / mrp) * 100) : 0);
  }

  /** Typing a discount rewrites the price; 0 restores full price. */
  onDiscountChange(value: number | string | null): void {
    const mrp = this.form.compareAtPrice ?? 0;
    if (mrp <= 0) return;

    const pct = Math.min(100, Math.max(0, Number(value) || 0));
    this.discountPct.set(pct);
    this.form.price = Math.round(mrp * (1 - pct / 100) * 100) / 100;
  }

  /** Changing the MRP leaves the price alone but changes what the discount works out to. */
  onMrpChange(): void {
    this.recomputeDiscount();
  }

  private blank(): SaveProductRequest {
    return {
      sku: '', designNo: '', name: '', categoryId: 0, brandId: null, price: 0, compareAtPrice: null, costPrice: null,
      shortDescription: '', description: '', hsnCode: '', status: 'Active', isFeatured: false, images: [],
      metaTitle: '', metaDescription: '', metaKeywords: '',
    };
  }

  private loadProduct(id: number): void {
    this.loading.set(true);
    this.api.getProduct(id).subscribe({
      next: (p: ProductDetail) => {
        this.form = {
          sku: p.sku, designNo: p.designNo ?? '', name: p.name, categoryId: p.categoryId, brandId: p.brandId,
          price: p.price, compareAtPrice: p.compareAtPrice, costPrice: p.costPrice,
          shortDescription: p.shortDescription, description: p.description, hsnCode: p.hsnCode,
          metaTitle: p.metaTitle ?? '', metaDescription: p.metaDescription ?? '', metaKeywords: p.metaKeywords ?? '',
          status: p.status, isFeatured: p.isFeatured,
          images: p.images.map((i) => ({ url: i.url, altText: i.altText, displayOrder: i.displayOrder, isPrimary: i.isPrimary })),
        };
        this.recomputeDiscount();
        this.variants.set(p.variants);
        this.customFields.set(p.customFields.map((f) => ({
          label: f.label, charLimit: f.charLimit, isMandatory: f.isMandatory, sortOrder: f.sortOrder,
        })));
        this.attrValues = {};
        for (const a of p.attributes) this.attrValues[a.attributeId] = a.value ?? a.valueText ?? '';
        this.loading.set(false);
      },
      error: () => { this.error.set('Could not load product.'); this.loading.set(false); },
    });
  }

  // --- Images ---
  addImage(): void {
    this.form.images!.push({ url: '', altText: '', displayOrder: this.form.images!.length, isPrimary: this.form.images!.length === 0 });
  }
  removeImage(i: number): void {
    this.form.images!.splice(i, 1);
  }
  setPrimary(i: number): void {
    this.form.images!.forEach((img, idx) => (img.isPrimary = idx === i));
  }
  uploadImage(img: ProductImageInput, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.uploading.set(true);
    this.error.set(null);
    this.media.upload(file, true).subscribe({
      next: (m) => { img.url = m.url; img.mediaFileId = m.mediaFileId; this.uploading.set(false); },
      error: () => { this.uploading.set(false); this.error.set('Image upload failed (max 5 MB; JPG/PNG/WebP/GIF only).'); },
    });
    input.value = '';
  }

  // --- Save product ---
  saveProduct(): void {
    if (!this.form.sku.trim() || !this.form.name.trim()) { this.error.set('SKU and Name are required.'); return; }
    if (!this.form.categoryId) { this.error.set('Please choose a category.'); return; }
    this.saving.set(true);
    this.error.set(null);
    this.message.set(null);

    const op = this.isEdit() ? this.api.updateProduct(this.productId()!, this.form) : this.api.createProduct(this.form);
    op.subscribe({
      next: (p) => {
        this.saving.set(false);
        this.message.set('Saved.');
        if (!this.isEdit()) this.router.navigate(['/admin/products', p.productId]);
      },
      error: (e) => { this.saving.set(false); this.error.set(e?.error?.message ?? 'Save failed.'); },
    });
  }

  // --- Options -> variants ---
  addOptionGroup(): void {
    this.optionGroups.update((g) => [...g, { name: '', valuesText: '' }]);
  }
  removeOptionGroup(i: number): void {
    this.optionGroups.update((g) => g.filter((_, idx) => idx !== i));
  }

  /**
   * Generates every combination across the option groups as a variant (Wix-style: define
   * options once, get a row per combination, then edit each row's SKU/price here — stock is
   * set afterwards on the Inventory page, which already supports per-variant stock).
   * Additive on the server — safe to run again after adding a value to an existing option;
   * only the new combinations are created, existing rows are untouched.
   */
  generateVariants(): void {
    const id = this.productId();
    if (!id) return;
    const groups = this.optionGroups()
      .map((g) => ({ name: g.name.trim(), values: g.valuesText.split(',').map((v) => v.trim()).filter((v) => v.length > 0) }))
      .filter((g) => g.name.length > 0 && g.values.length > 0);
    if (!groups.length) { this.error.set('Add at least one option with a name and at least one value.'); return; }

    this.generatingVariants.set(true);
    this.error.set(null);
    this.message.set(null);
    this.api.generateVariants(id, groups).subscribe({
      next: (v) => {
        this.generatingVariants.set(false);
        this.variants.set(v);
        this.message.set('Variants generated.');
      },
      error: (e) => { this.generatingVariants.set(false); this.error.set(e?.error?.message ?? 'Could not generate variants.'); },
    });
  }

  /** Persists SKU/price-difference/active edited in place on an existing variant row. */
  saveVariant(v: ProductVariant): void {
    const id = this.productId();
    if (!id) return;
    this.savingVariantId.set(v.productVariantId);
    this.error.set(null);
    const body: SaveVariantRequest = {
      sku: v.sku, name: v.name, priceAdjustment: v.priceAdjustment, isActive: v.isActive,
      options: v.options.map((o) => ({ optionName: o.optionName, optionValue: o.optionValue })),
    };
    this.api.updateVariant(id, v.productVariantId, body).subscribe({
      next: () => this.savingVariantId.set(null),
      error: (e) => { this.savingVariantId.set(null); this.error.set(e?.error?.message ?? 'Could not save variant.'); },
    });
  }

  deleteVariant(v: ProductVariant): void {
    if (!confirm(`Delete variant "${v.name || v.sku}"? This cannot be undone.`)) return;
    this.deletingVariantId.set(v.productVariantId);
    this.api.deleteVariant(this.productId()!, v.productVariantId).subscribe({
      next: () => { this.deletingVariantId.set(null); this.reloadVariants(); },
      error: (e) => { this.deletingVariantId.set(null); this.error.set(e?.error?.message ?? 'Delete failed.'); },
    });
  }
  private reloadVariants(): void {
    this.api.listVariants(this.productId()!).subscribe((v) => this.variants.set(v));
  }
  variantLabel(v: ProductVariant): string {
    return v.options.length ? v.options.map((o) => o.optionValue).join(' / ') : v.name || v.sku;
  }

  // --- Custom text fields ---
  addCustomField(): void {
    this.customFields.update((f) => [...f, { label: '', charLimit: 500, isMandatory: false, sortOrder: f.length }]);
  }
  removeCustomField(i: number): void {
    this.customFields.update((f) => f.filter((_, idx) => idx !== i));
  }
  saveCustomFields(): void {
    const id = this.productId();
    if (!id) return;
    const fields = this.customFields()
      .map((f, i) => ({ ...f, label: f.label.trim(), sortOrder: i }))
      .filter((f) => f.label.length > 0);
    this.savingCustomFields.set(true);
    this.error.set(null);
    this.message.set(null);
    this.api.setProductCustomFields(id, fields).subscribe({
      next: (saved) => {
        this.savingCustomFields.set(false);
        this.customFields.set(saved.map((f) => ({
          label: f.label, charLimit: f.charLimit, isMandatory: f.isMandatory, sortOrder: f.sortOrder,
        })));
        this.message.set('Custom text fields saved.');
      },
      error: (e) => { this.savingCustomFields.set(false); this.error.set(e?.error?.message ?? 'Could not save custom fields.'); },
    });
  }

  // --- Attributes ---
  saveAttributes(): void {
    const inputs = this.attributeDefs()
      .filter((a) => (this.attrValues[a.attributeId] ?? '').trim().length > 0)
      .map((a) => ({ attributeId: a.attributeId, valueText: this.attrValues[a.attributeId].trim() }));
    this.api.setProductAttributes(this.productId()!, inputs).subscribe({
      next: () => this.message.set('Specifications saved.'),
      error: (e) => this.error.set(e?.error?.message ?? 'Failed.'),
    });
  }

  // --- Suppliers ---
  private loadProductSuppliers(id: number): void {
    this.suppliers.forProduct(id).subscribe((ps) => {
      this.productSuppliers = ps.map((p) => ({
        supplierId: p.supplierId, supplierSku: p.supplierSku, costPrice: p.costPrice,
        leadTimeDays: p.leadTimeDays, isPrimary: p.isPrimary,
      }));
    });
  }
  addProductSupplier(): void {
    this.productSuppliers.push({
      supplierId: 0, supplierSku: null, costPrice: null, leadTimeDays: null,
      isPrimary: this.productSuppliers.length === 0,
    });
  }
  removeProductSupplier(i: number): void {
    this.productSuppliers.splice(i, 1);
  }
  setPrimarySupplier(i: number): void {
    this.productSuppliers.forEach((ps, idx) => (ps.isPrimary = idx === i));
  }
  saveProductSuppliers(): void {
    const rows = this.productSuppliers.filter((ps) => ps.supplierId);
    if (rows.length && !rows.some((ps) => ps.isPrimary)) rows[0].isPrimary = true;
    this.suppliers.setForProduct(this.productId()!, rows).subscribe({
      next: () => this.message.set('Suppliers saved.'),
      error: (e) => this.error.set(e?.error?.message ?? 'Failed.'),
    });
  }
}
