import { Component, EventEmitter, OnInit, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MediaFile, MediaService } from '../../core/services/media.service';

/**
 * E4: image field picker modal — Library (existing uploads, paginated), Upload (new file), and a URL
 * fallback tab for external images. Both endpoints it calls already existed (admin media
 * list/upload); this is pure frontend wiring, no backend change.
 */
@Component({
  selector: 'app-media-picker',
  imports: [FormsModule],
  template: `
    <div class="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" (click)="close.emit()">
      <div class="bg-white rounded-xl shadow-xl w-full max-w-2xl max-h-[80vh] flex flex-col" (click)="$event.stopPropagation()">
        <div class="flex items-center justify-between px-4 py-3 border-b border-slate-200 shrink-0">
          <div class="flex gap-1">
            <button type="button" (click)="tab.set('library')" class="text-sm px-3 py-1.5 rounded-lg" [class]="tab() === 'library' ? 'bg-slate-800 text-white' : 'text-slate-600 hover:bg-slate-50'">Library</button>
            <button type="button" (click)="tab.set('upload')" class="text-sm px-3 py-1.5 rounded-lg" [class]="tab() === 'upload' ? 'bg-slate-800 text-white' : 'text-slate-600 hover:bg-slate-50'">Upload</button>
            <button type="button" (click)="tab.set('url')" class="text-sm px-3 py-1.5 rounded-lg" [class]="tab() === 'url' ? 'bg-slate-800 text-white' : 'text-slate-600 hover:bg-slate-50'">URL</button>
          </div>
          <button type="button" (click)="close.emit()" aria-label="Close" class="text-slate-400 hover:text-slate-700 text-xl leading-none w-7 h-7 grid place-items-center">×</button>
        </div>
        <div class="flex-1 overflow-auto p-4">
          @switch (tab()) {
            @case ('library') {
              @if (loading()) {
                <p class="text-sm text-slate-400 text-center py-10">Loading…</p>
              } @else if (!files().length) {
                <p class="text-sm text-slate-400 text-center py-10">No images uploaded yet — try the Upload tab.</p>
              } @else {
                <div class="grid grid-cols-4 gap-2">
                  @for (f of files(); track f.mediaFileId) {
                    <button type="button" (click)="picked.emit(f.url)" [title]="f.originalName || ''"
                      class="aspect-square rounded-lg overflow-hidden border border-slate-200 hover:border-primary hover:ring-1 hover:ring-primary transition">
                      <img [src]="f.url" [alt]="f.originalName || ''" class="w-full h-full object-cover" loading="lazy" />
                    </button>
                  }
                </div>
                @if (hasMore()) {
                  <button type="button" (click)="loadMore()" [disabled]="loadingMore()" class="mt-4 text-sm text-primary hover:underline mx-auto block">
                    {{ loadingMore() ? 'Loading…' : 'Load more' }}
                  </button>
                }
              }
            }
            @case ('upload') {
              <label class="block border-2 border-dashed border-slate-300 rounded-xl p-10 text-center cursor-pointer hover:border-primary transition">
                <input type="file" accept="image/*" class="hidden" (change)="onFileSelected($event)" [disabled]="uploading()" />
                @if (uploading()) { <p class="text-sm text-slate-500">Uploading…</p> } @else { <p class="text-sm text-slate-500">Click to choose an image (JPEG, PNG, WebP or GIF)</p> }
              </label>
              @if (uploadError()) { <p class="text-sm text-red-600 mt-2 text-center">{{ uploadError() }}</p> }
            }
            @case ('url') {
              <label class="block">
                <span class="text-sm text-slate-600 mb-1 block">Image URL</span>
                <input [(ngModel)]="urlInput" placeholder="https://…/image.jpg" class="input w-full" />
              </label>
              <button type="button" (click)="picked.emit(urlInput.trim())" [disabled]="!urlInput.trim()" class="btn-primary mt-3 w-full disabled:opacity-50">Use this URL</button>
            }
          }
        </div>
      </div>
    </div>
  `,
})
export class MediaPickerComponent implements OnInit {
  private readonly media = inject(MediaService);

  @Output() picked = new EventEmitter<string>();
  @Output() close = new EventEmitter<void>();

  readonly tab = signal<'library' | 'upload' | 'url'>('library');
  readonly files = signal<MediaFile[]>([]);
  readonly loading = signal(true);
  readonly loadingMore = signal(false);
  readonly hasMore = signal(false);
  readonly uploading = signal(false);
  readonly uploadError = signal<string | null>(null);
  urlInput = '';

  private page = 1;
  private readonly pageSize = 24;

  ngOnInit(): void { this.loadPage(1); }

  private loadPage(page: number): void {
    if (page === 1) this.loading.set(true); else this.loadingMore.set(true);
    this.media.list(page, this.pageSize).subscribe({
      next: (r) => {
        this.files.set(page === 1 ? r.items : [...this.files(), ...r.items]);
        this.page = page;
        this.hasMore.set(page * this.pageSize < r.totalCount);
        this.loading.set(false);
        this.loadingMore.set(false);
      },
      error: () => { this.loading.set(false); this.loadingMore.set(false); },
    });
  }
  loadMore(): void { this.loadPage(this.page + 1); }

  onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.uploading.set(true);
    this.uploadError.set(null);
    this.media.upload(file).subscribe({
      next: (m) => { this.uploading.set(false); this.picked.emit(m.url); },
      error: (e) => { this.uploading.set(false); this.uploadError.set((e as { error?: { message?: string } })?.error?.message ?? 'Upload failed.'); },
    });
  }
}
