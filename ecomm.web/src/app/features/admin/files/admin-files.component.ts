import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MediaFile, MediaService } from '../../../core/services/media.service';

@Component({
  selector: 'app-admin-files',
  imports: [DatePipe],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <div class="flex items-center justify-between mb-1">
        <h1 class="text-xl font-bold text-slate-900">Files</h1>
        <label class="btn-primary cursor-pointer">
          {{ uploading() ? 'Uploading…' : 'Upload files' }}
          <input type="file" accept="image/*" class="hidden" (change)="upload($event)" multiple />
        </label>
      </div>
      <p class="text-sm text-slate-500 mb-4">Images you've uploaded to your store. Reuse a URL anywhere.</p>
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }
      @if (copied()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">URL copied.</div> }

      @if (loading()) { <p class="text-slate-400 text-sm">Loading…</p> }
      @else if (!files().length) { <div class="bg-white border border-slate-200 rounded-xl p-10 text-center text-slate-400">No files yet.</div> }
      @else {
        <div class="grid grid-cols-2 sm:grid-cols-4 gap-4">
          @for (f of files(); track f.mediaFileId) {
            <div class="bg-white border border-slate-200 rounded-xl overflow-hidden group">
              <div class="aspect-square bg-slate-50 grid place-items-center overflow-hidden">
                <img [src]="f.url" [alt]="f.originalName || ''" class="w-full h-full object-cover" />
              </div>
              <div class="p-2 text-xs">
                <div class="font-medium text-slate-700 truncate">{{ f.originalName || 'image' }}</div>
                <div class="text-slate-400">{{ size(f.sizeBytes) }} · {{ f.references }} use{{ f.references === 1 ? '' : 's' }} · {{ f.createdAt | date:'shortDate' }}</div>
                <div class="flex gap-2 mt-1">
                  <button type="button" (click)="copy(f)" class="text-blue-600 hover:underline">Copy URL</button>
                  <button type="button" (click)="remove(f)" [disabled]="f.references > 0" class="text-red-500 hover:underline disabled:text-slate-300" [title]="f.references > 0 ? 'In use by a product' : ''">Delete</button>
                </div>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class AdminFilesComponent implements OnInit {
  private readonly api = inject(MediaService);
  readonly files = signal<MediaFile[]>([]);
  readonly loading = signal(true);
  readonly uploading = signal(false);
  readonly error = signal<string | null>(null);
  readonly copied = signal(false);

  ngOnInit(): void { this.load(); }
  private load(): void { this.loading.set(true); this.api.list().subscribe({ next: (r) => { this.files.set(r.items); this.loading.set(false); }, error: () => this.loading.set(false) }); }

  size(bytes: number | null): string { if (!bytes) return '—'; return bytes > 1024 * 1024 ? (bytes / 1024 / 1024).toFixed(1) + ' MB' : Math.round(bytes / 1024) + ' KB'; }

  upload(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    if (!files.length) return;
    this.uploading.set(true); this.error.set(null);
    let remaining = files.length;
    for (const file of files) {
      this.api.upload(file).subscribe({
        next: () => { if (--remaining === 0) { this.uploading.set(false); this.load(); } },
        error: (e: unknown) => { this.uploading.set(false); this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Upload failed.'); },
      });
    }
    input.value = '';
  }

  copy(f: MediaFile): void {
    navigator.clipboard?.writeText(f.url).then(() => { this.copied.set(true); setTimeout(() => this.copied.set(false), 2000); });
  }
  remove(f: MediaFile): void {
    if (f.references > 0 || !confirm('Delete this file?')) return;
    this.api.remove(f.mediaFileId).subscribe({ next: () => this.files.set(this.files().filter((x) => x.mediaFileId !== f.mediaFileId)) });
  }
}
