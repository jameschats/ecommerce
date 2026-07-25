import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { API_BASE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';
import { ImportJobResult } from '../../../core/models/admin-catalog.model';
import { AdminCatalogService } from '../../../core/services/admin-catalog.service';

interface ZipImageResult {
  filesInZip: number;
  matched: number;
  productsUpdated: number;
  unmatched: string[];
  skipped: string[];
  productsWithoutImages: string[];
}

@Component({
  selector: 'app-admin-import',
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Import / Export products</h1>
      <p class="text-sm text-slate-500 mb-6">Bulk-manage your catalog with Excel. Upsert by SKU; unknown columns become specifications.</p>
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      <div class="grid sm:grid-cols-2 gap-4 mb-6">
        <div class="bg-white border border-slate-200 rounded-xl p-5">
          <h2 class="font-medium text-slate-800 mb-2">Export</h2>
          <p class="text-sm text-slate-500 mb-3">Download all products, edit in Excel, re-upload.</p>
          <button type="button" (click)="exportProducts()" class="btn-primary">Download products.xlsx</button>
        </div>
        <div class="bg-white border border-slate-200 rounded-xl p-5">
          <h2 class="font-medium text-slate-800 mb-2">Template</h2>
          <p class="text-sm text-slate-500 mb-3">Get a blank template with the expected columns.</p>
          <button type="button" (click)="downloadTemplate()" class="btn-ghost border border-slate-300">Download template</button>
        </div>
      </div>

      <div class="bg-white border border-slate-200 rounded-xl p-5">
        <h2 class="font-medium text-slate-800 mb-2">Import</h2>
        <input type="file" accept=".xlsx" (change)="onFile($event)" class="block text-sm mb-3" />
        <button type="button" (click)="upload()" [disabled]="!file || uploading()" class="btn-primary">
          {{ uploading() ? 'Importing…' : 'Upload & import' }}
        </button>

        @if (result(); as r) {
          <div class="mt-5 border-t border-slate-100 pt-4">
            <p class="text-sm">
              <span class="font-medium">{{ r.job.status }}</span> —
              {{ r.job.successRows }} imported, {{ r.job.failedRows }} failed of {{ r.job.totalRows }}.
            </p>
            @if (r.failedRows.length) {
              <table class="w-full text-sm mt-3">
                <thead class="text-slate-400 text-left"><tr><th class="py-1">Row</th><th class="py-1">Error</th></tr></thead>
                <tbody>
                  @for (f of r.failedRows; track f.rowNumber) {
                    <tr class="border-t border-slate-100"><td class="py-1 w-16">{{ f.rowNumber }}</td><td class="py-1 text-red-600">{{ f.errorMessage }}</td></tr>
                  }
                </tbody>
              </table>
            }
          </div>
        }
      </div>

      <!-- Bulk product images, matched by Design No (design.md §10.2) -->
      <div class="bg-white border border-slate-200 rounded-xl p-5 mt-6">
        <h2 class="font-medium text-slate-800 mb-2">Product images (ZIP)</h2>
        <p class="text-sm text-slate-500">
          Name each file after its <strong>Design No</strong>. For several images of one design,
          add <code>-1</code>, <code>-2</code>, <code>-3</code> — the lowest number becomes the main image.
        </p>
        <pre class="mt-2 mb-3 text-xs bg-slate-50 border border-slate-200 rounded p-3 text-slate-600">DESK-1.jpg          one image for design DESK-1
DESK-1-1.jpg        first image  (main)
DESK-1-2.jpg        second image
DESK-1-3.jpg        third image</pre>
        <p class="text-xs text-slate-500 mb-3">
          Accepts .jpg, .jpeg, .png and .webp. Matching ignores case. Files that match no design
          number are listed back to you, never discarded silently.
        </p>

        <input type="file" accept=".zip" (change)="onZip($event)" class="block text-sm mb-3" />

        <label class="flex items-center gap-2 text-sm text-slate-700 mb-3">
          <input type="checkbox" [checked]="replaceExisting()" (change)="replaceExisting.set($any($event.target).checked)" class="w-4 h-4" />
          Replace existing images on the designs in this ZIP
        </label>

        <button type="button" (click)="uploadZip()" [disabled]="!zipFile || zipUploading()" class="btn-primary">
          {{ zipUploading() ? 'Uploading…' : 'Upload images' }}
        </button>

        @if (zipResult(); as z) {
          <div class="mt-5 border-t border-slate-100 pt-4 text-sm space-y-2">
            <p>
              <span class="font-medium text-emerald-700">{{ z.matched }}</span> image(s) attached to
              <span class="font-medium">{{ z.productsUpdated }}</span> design(s), from {{ z.filesInZip }} file(s).
            </p>

            @if (z.unmatched.length) {
              <details class="rounded border border-amber-200 bg-amber-50 px-3 py-2">
                <summary class="cursor-pointer text-amber-900 font-medium">
                  {{ z.unmatched.length }} file(s) matched no design number
                </summary>
                <ul class="mt-2 text-amber-900 font-mono text-xs space-y-0.5">
                  @for (u of z.unmatched; track u) { <li>{{ u }}</li> }
                </ul>
              </details>
            }

            @if (z.skipped.length) {
              <details class="rounded border border-slate-200 px-3 py-2">
                <summary class="cursor-pointer text-slate-700 font-medium">{{ z.skipped.length }} file(s) skipped</summary>
                <ul class="mt-2 text-slate-600 text-xs space-y-0.5">
                  @for (s of z.skipped; track s) { <li>{{ s }}</li> }
                </ul>
              </details>
            }

            @if (z.productsWithoutImages.length) {
              <details class="rounded border border-slate-200 px-3 py-2">
                <summary class="cursor-pointer text-slate-700 font-medium">
                  {{ z.productsWithoutImages.length }} design(s) still have no image
                </summary>
                <ul class="mt-2 text-slate-600 font-mono text-xs space-y-0.5">
                  @for (p of z.productsWithoutImages; track p) { <li>{{ p }}</li> }
                </ul>
              </details>
            }
          </div>
        }
      </div>
    </div>
  `,
})
export class AdminImportComponent {
  private readonly api = inject(AdminCatalogService);
  private readonly http = inject(HttpClient);

  readonly zipUploading = signal(false);
  readonly zipResult = signal<ZipImageResult | null>(null);
  readonly replaceExisting = signal(true);
  zipFile: File | null = null;

  readonly uploading = signal(false);
  readonly result = signal<ImportJobResult | null>(null);
  readonly error = signal<string | null>(null);
  file: File | null = null;

  onFile(event: Event): void {
    this.file = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  upload(): void {
    if (!this.file) return;
    this.uploading.set(true);
    this.error.set(null);
    this.result.set(null);
    this.api.importProducts(this.file).subscribe({
      next: (r) => { this.result.set(r); this.uploading.set(false); },
      error: (e) => { this.error.set(e?.error?.message ?? 'Import failed.'); this.uploading.set(false); },
    });
  }

  // --- bulk product images by Design No ---

  onZip(event: Event): void {
    this.zipFile = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.zipResult.set(null);
  }

  uploadZip(): void {
    if (!this.zipFile || this.zipUploading()) return;
    this.zipUploading.set(true);
    this.error.set(null);
    this.zipResult.set(null);

    const form = new FormData();
    form.append('file', this.zipFile);

    this.http
      .post<ApiResponse<ZipImageResult>>(
        `${API_BASE_URL}/admin/products/images/zip?replaceExisting=${this.replaceExisting()}`,
        form,
      )
      .subscribe({
        next: (r) => {
          this.zipResult.set(r.data ?? null);
          this.zipUploading.set(false);
        },
        error: (e) => {
          this.error.set(e?.error?.message ?? 'Image upload failed.');
          this.zipUploading.set(false);
        },
      });
  }

  exportProducts(): void {
    this.api.exportProducts().subscribe((blob) => this.download(blob, 'products.xlsx'));
  }

  downloadTemplate(): void {
    this.api.downloadTemplate().subscribe((blob) => this.download(blob, 'products-template.xlsx'));
  }

  private download(blob: Blob, name: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = name;
    a.click();
    URL.revokeObjectURL(url);
  }
}
