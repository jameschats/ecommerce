import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AiAssistService } from '../../../core/services/ai-assist.service';
import { AiImportAnalysis, AiImportService } from '../../../core/services/ai-import.service';
import { ImportJobResult } from '../../../core/models/admin-catalog.model';

/**
 * AI-3 smart import. Upload any product spreadsheet (.xlsx/.csv) → AI matches your columns to our schema →
 * review/correct the mapping → import. Missing categories are created automatically. Nothing is written
 * until you click Import.
 */
@Component({
  selector: 'app-admin-ai-import',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <div class="flex items-center justify-between gap-3 mb-1">
        <h1 class="text-xl font-bold text-slate-900">Import from any spreadsheet</h1>
        <a routerLink="/admin/ai" class="text-sm text-slate-500 hover:underline">AI credits ↗</a>
      </div>
      <p class="text-sm text-slate-500 mb-5">Migrating from Shopify, WooCommerce or Wix — or any spreadsheet in any layout (.xlsx or .csv)? We recognise the big platforms automatically and AI maps the rest. Review the mapping, then import; new categories are created for you.</p>

      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      @if (!ai.enabled()) {
        <div class="rounded-lg bg-amber-50 border border-amber-200 text-amber-800 text-sm px-3 py-2">
          AI features aren't switched on for this platform yet, so smart import is unavailable. You can still use the standard <a routerLink="/admin/import" class="underline">Import / Export</a>.
        </div>
      } @else {
        <div class="bg-white border border-slate-200 rounded-xl p-5 mb-6">
          <label class="lbl">Your product file</label>
          <input type="file" accept=".xlsx,.csv" (change)="onFile($event)" class="block text-sm" />
          <p class="text-xs text-slate-400 mt-2">Analyzing a file costs 2 AI credits.</p>
          @if (analyzing()) { <p class="text-sm text-violet-600 mt-3">Reading your columns…</p> }
        </div>

        @if (analysis(); as a) {
          <h2 class="font-semibold text-slate-800 mb-1">Review the column mapping</h2>
          <p class="text-sm text-slate-500 mb-3">We matched your columns to ours. Change any that look wrong before importing.</p>
          <div class="flex flex-wrap items-center gap-2 mb-3 text-sm">
            @if (a.detectedFormat) { <span class="px-2 py-0.5 rounded-full bg-green-50 text-green-700 text-xs">Detected: {{ a.detectedFormat }}</span> }
            <label class="text-slate-500">Treat as</label>
            <select [(ngModel)]="format" name="fmt" (change)="reanalyze()" [disabled]="analyzing()" class="input py-1 w-auto">
              <option value="">Auto-detect</option>
              @for (f of a.formats; track f.value) { <option [value]="f.value">{{ f.label }}</option> }
            </select>
            @if (analyzing()) { <span class="text-violet-600">Re-checking…</span> }
          </div>
          <div class="bg-white border border-slate-200 rounded-xl overflow-hidden mb-4">
            <table class="w-full text-sm">
              <thead class="bg-slate-50 text-slate-500 text-left">
                <tr><th class="px-4 py-2 font-medium">Your column</th><th class="px-4 py-2 font-medium">Sample</th><th class="px-4 py-2 font-medium">Maps to</th></tr>
              </thead>
              <tbody>
                @for (h of a.headers; track h; let i = $index) {
                  <tr class="border-t border-slate-100">
                    <td class="px-4 py-2 font-medium text-slate-700">{{ h }}</td>
                    <td class="px-4 py-2 text-slate-400 max-w-[16rem] truncate">{{ sampleOf(a, i) }}</td>
                    <td class="px-4 py-2">
                      <select [(ngModel)]="mapping[h]" name="map-{{ i }}" class="input py-1.5">
                        @for (f of a.fields; track f.value) { <option [value]="f.value">{{ f.label }}</option> }
                      </select>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <button type="button" (click)="apply()" [disabled]="importing()" class="btn-primary px-5 py-2.5">
            {{ importing() ? 'Importing…' : 'Import products' }}
          </button>
        }

        @if (result(); as r) {
          <div class="mt-6 bg-white border border-slate-200 rounded-xl p-5">
            <p class="text-sm"><span class="font-medium">{{ r.job.status }}</span> — {{ r.job.successRows }} imported, {{ r.job.failedRows }} failed of {{ r.job.totalRows }}.</p>
            @if (r.failedRows.length) {
              <table class="w-full text-sm mt-3">
                <thead class="text-slate-400 text-left"><tr><th class="py-1 w-16">Row</th><th class="py-1">Error</th></tr></thead>
                <tbody>
                  @for (f of r.failedRows; track f.rowNumber) {
                    <tr class="border-t border-slate-100"><td class="py-1">{{ f.rowNumber }}</td><td class="py-1 text-red-600">{{ f.errorMessage }}</td></tr>
                  }
                </tbody>
              </table>
            }
            <a routerLink="/admin/products" class="inline-block mt-3 text-sm text-blue-600 hover:underline">View products →</a>
          </div>
        }
      }
    </div>
  `,
})
export class AdminAiImportComponent implements OnInit {
  readonly ai = inject(AiAssistService);
  private readonly api = inject(AiImportService);

  readonly analyzing = signal(false);
  readonly importing = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly analysis = signal<AiImportAnalysis | null>(null);
  readonly result = signal<ImportJobResult | null>(null);

  file: File | null = null;
  mapping: Record<string, string> = {};
  format = '';

  ngOnInit(): void { this.ai.ensureStatus(); }

  onFile(event: Event): void {
    this.file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.format = '';
    this.analysis.set(null); this.result.set(null); this.error.set(null); this.message.set(null);
    if (this.file) this.runAnalyze();
  }

  /** Re-run mapping when the merchant forces a platform format ("Treat as…"). */
  reanalyze(): void { this.runAnalyze(); }

  private runAnalyze(): void {
    if (!this.file) return;
    this.analyzing.set(true); this.error.set(null);
    this.api.analyze(this.file, this.format || undefined).subscribe({
      next: (a) => { this.analysis.set(a); this.mapping = { ...a.mapping }; this.analyzing.set(false); },
      error: (e: unknown) => { this.analyzing.set(false); this.error.set(this.msg(e, true)); },
    });
  }

  sampleOf(a: AiImportAnalysis, col: number): string {
    for (const row of a.sampleRows) { const v = row[col]?.trim(); if (v) return v; }
    return '—';
  }

  apply(): void {
    if (!this.file) return;
    this.importing.set(true); this.error.set(null); this.result.set(null);
    this.api.apply(this.file, this.mapping).subscribe({
      next: (r) => { this.importing.set(false); this.result.set(r); this.analysis.set(null); this.message.set('Import complete.'); },
      error: (e: unknown) => { this.importing.set(false); this.error.set(this.msg(e)); },
    });
  }

  private msg(e: unknown, credits = false): string {
    const err = e as { status?: number; error?: { message?: string } };
    if (credits && err?.status === 402) return err.error?.message ?? 'Not enough AI credits — top up on the AI credits page.';
    return err?.error?.message ?? 'Something went wrong. Please try again.';
  }
}
