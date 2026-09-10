import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Catalogue } from '../../../core/models/catalogue.model';
import { CatalogueService } from '../../../core/services/catalogue.service';

@Component({
  selector: 'app-admin-catalogues',
  imports: [FormsModule],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Catalogues</h1>
      <p class="text-sm text-slate-500 mb-4">
        The downloadable PDF catalogues shown on the storefront's Catalogues page. Hidden ones aren't listed there.
      </p>

      <div class="flex items-center justify-between mb-1">
        <h2 class="text-sm font-semibold text-slate-700">Files</h2>
        <label class="btn-primary shrink-0 ml-3 cursor-pointer" [class.opacity-60]="uploading()">
          {{ uploading() ? 'Uploading…' : '+ Upload PDF' }}
          <input type="file" accept="application/pdf" class="hidden" [disabled]="uploading()" (change)="onUpload($event)" />
        </label>
      </div>
      @if (message()) { <div class="mt-4 mb-2 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mt-4 mb-2 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      @if (loading()) { <div class="p-8 text-center text-slate-400">Loading…</div> }
      @else if (!items().length) { <div class="p-8 text-center text-slate-400">No catalogues yet. Click "Upload PDF" to add one.</div> }
      @else {
        <div class="space-y-3 mt-4">
          @for (c of items(); track c.catalogueId; let i = $index) {
            <div class="bg-white border border-slate-200 rounded-xl p-3 flex gap-3 items-center" [class.opacity-60]="!c.isActive">
              <div class="flex flex-col justify-center">
                <button type="button" (click)="move(i, -1)" [disabled]="i === 0" class="text-slate-400 hover:text-slate-700 disabled:opacity-30 leading-none">▲</button>
                <button type="button" (click)="move(i, 1)" [disabled]="i === items().length - 1" class="text-slate-400 hover:text-slate-700 disabled:opacity-30 leading-none">▼</button>
              </div>

              <div class="w-10 h-10 shrink-0 rounded-lg bg-red-50 text-red-600 grid place-items-center text-xs font-bold">PDF</div>

              <div class="flex-1 min-w-0 space-y-1.5">
                <input [(ngModel)]="c.title" [name]="'ti' + c.catalogueId" placeholder="Title" class="input w-full" />
                <div class="flex items-center justify-between">
                  <p class="text-xs text-slate-400 truncate" [title]="c.fileName">{{ c.fileName }} · {{ formatSize(c.fileSizeBytes) }}</p>
                  <a [href]="c.fileUrl" target="_blank" rel="noopener" class="text-xs text-blue-600 hover:underline shrink-0 ml-2">View</a>
                </div>
                <div class="flex items-center justify-between pt-0.5">
                  <label class="flex items-center gap-2 text-sm text-slate-600">
                    <input type="checkbox" [(ngModel)]="c.isActive" [name]="'a' + c.catalogueId" /> Visible
                  </label>
                  <button type="button" (click)="remove(c)" class="text-sm text-red-500 hover:text-red-700">Delete</button>
                </div>
              </div>
            </div>
          }
        </div>
        <button type="button" (click)="save()" [disabled]="busy()" class="btn-primary mt-4">{{ busy() ? 'Saving…' : 'Save changes' }}</button>
      }
    </div>
  `,
})
export class AdminCataloguesComponent implements OnInit {
  private readonly svc = inject(CatalogueService);

  readonly items = signal<Catalogue[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly uploading = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  ngOnInit(): void { this.reload(); }

  private reload(): void {
    this.loading.set(true);
    this.svc.listAdmin().subscribe({
      next: (c) => { this.items.set(c); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  formatSize(bytes: number): string {
    return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.round(bytes / 1024)} KB`;
  }

  onUpload(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.uploading.set(true);
    this.error.set(null);
    this.svc.upload(file).subscribe({
      next: () => { this.uploading.set(false); this.flash('Catalogue uploaded.'); this.reload(); },
      error: (e) => { this.uploading.set(false); this.error.set(e?.error?.message ?? 'Upload failed (max 25 MB, PDF only).'); },
    });
    input.value = '';
  }

  move(index: number, delta: number): void {
    const arr = [...this.items()];
    const target = index + delta;
    if (target < 0 || target >= arr.length) return;
    [arr[index], arr[target]] = [arr[target], arr[index]];
    this.items.set(arr);
  }

  remove(c: Catalogue): void {
    if (!confirm(`Delete "${c.title}"?`)) return;
    this.busy.set(true);
    this.svc.remove(c.catalogueId).subscribe({
      next: () => { this.busy.set(false); this.flash('Catalogue deleted.'); this.reload(); },
      error: () => { this.busy.set(false); this.error.set('Delete failed.'); },
    });
  }

  save(): void {
    this.busy.set(true);
    this.message.set(null);
    this.error.set(null);
    const updates = this.items().map((c, i) =>
      this.svc.update(c.catalogueId, { title: c.title, displayOrder: i + 1, isActive: c.isActive }),
    );
    let done = 0, failed = false;
    updates.forEach((o) => o.subscribe({
      next: () => { if (++done === updates.length) this.finishSave(failed); },
      error: () => { failed = true; if (++done === updates.length) this.finishSave(failed); },
    }));
    if (!updates.length) this.finishSave(false);
  }

  private finishSave(failed: boolean): void {
    this.busy.set(false);
    if (failed) this.error.set('Some catalogues failed to save.');
    else this.flash('Saved.');
    this.reload();
  }

  private flash(msg: string): void {
    this.message.set(msg);
  }
}
