import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ImportJobResult } from '../../../core/models/admin-catalog.model';
import { AdminCatalogService } from '../../../core/services/admin-catalog.service';

@Component({
  selector: 'app-admin-import',
  imports: [RouterLink],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Import / Export products</h1>
      <p class="text-sm text-slate-500 mb-6">Bulk-manage your catalog with Excel. Upsert by SKU; unknown columns become specifications.</p>
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      <div class="grid sm:grid-cols-2 gap-4 mb-6">
        <a routerLink="/admin/ai/catalog" class="block rounded-xl border border-violet-200 bg-violet-50 p-5 hover:bg-violet-100 transition">
          <div class="font-medium text-violet-800">✨ Generate a starter catalog with AI</div>
          <p class="text-sm text-violet-700/80 mt-1">No spreadsheet yet? Pick a store type and let AI create products you can edit.</p>
        </a>
        <a routerLink="/admin/ai/import" class="block rounded-xl border border-violet-200 bg-violet-50 p-5 hover:bg-violet-100 transition">
          <div class="font-medium text-violet-800">✨ Import from any spreadsheet</div>
          <p class="text-sm text-violet-700/80 mt-1">Already have a product file in any layout? AI matches your columns to ours.</p>
        </a>
      </div>

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
    </div>
  `,
})
export class AdminImportComponent {
  private readonly api = inject(AdminCatalogService);

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
