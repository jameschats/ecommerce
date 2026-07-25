import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AttributeDef, ProductImageInput, SaveProductRequest, SaveVariantRequest } from '../../../core/models/admin-catalog.model';
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

  readonly allSuppliers = signal<Supplier[]>([]);
  productSuppliers: ProductSupplierInput[] = [];

  form: SaveProductRequest = this.blank();
  newVariant: SaveVariantRequest = this.blankVariant();
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

  private blank(): SaveProductRequest {
    return {
      sku: '', designNo: '', name: '', categoryId: 0, brandId: null, price: 0, compareAtPrice: null, costPrice: null,
      shortDescription: '', description: '', hsnCode: '', status: 'Active', isFeatured: false, images: [],
    };
  }

  private blankVariant(): SaveVariantRequest {
    return { sku: '', name: '', priceAdjustment: 0, isActive: true, options: [] };
  }

  private loadProduct(id: number): void {
    this.loading.set(true);
    this.api.getProduct(id).subscribe({
      next: (p: ProductDetail) => {
        this.form = {
          sku: p.sku, designNo: p.designNo ?? '', name: p.name, categoryId: p.categoryId, brandId: p.brandId,
          price: p.price, compareAtPrice: p.compareAtPrice, costPrice: p.costPrice,
          shortDescription: p.shortDescription, description: p.description, hsnCode: p.hsnCode,
          status: p.status, isFeatured: p.isFeatured,
          images: p.images.map((i) => ({ url: i.url, altText: i.altText, displayOrder: i.displayOrder, isPrimary: i.isPrimary })),
        };
        this.variants.set(p.variants);
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
    this.media.upload(file).subscribe({
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

  // --- Variants ---
  addVariantOption(): void {
    this.newVariant.options!.push({ optionName: '', optionValue: '' });
  }
  addVariant(): void {
    if (!this.productId() || !this.newVariant.sku.trim()) return;
    this.api.createVariant(this.productId()!, this.newVariant).subscribe({
      next: () => { this.newVariant = this.blankVariant(); this.reloadVariants(); },
      error: (e) => this.error.set(e?.error?.message ?? 'Variant failed.'),
    });
  }
  deleteVariant(v: ProductVariant): void {
    this.api.deleteVariant(this.productId()!, v.productVariantId).subscribe({
      next: () => this.reloadVariants(),
      error: (e) => this.error.set(e?.error?.message ?? 'Delete failed.'),
    });
  }
  private reloadVariants(): void {
    this.api.listVariants(this.productId()!).subscribe((v) => this.variants.set(v));
  }
  variantLabel(v: ProductVariant): string {
    return v.options.length ? v.options.map((o) => `${o.optionName}: ${o.optionValue}`).join(', ') : v.sku;
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
