import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminGalleryImage, GallerySection } from '../../../core/models/gallery.model';
import { GalleryService } from '../../../core/services/gallery.service';

interface SectionTab { key: GallerySection; label: string; hint: string; }

const TABS: SectionTab[] = [
  { key: 'new-designs', label: 'New designs', hint: 'Shown at the top of the home page, right below the banner.' },
  { key: 'featured', label: 'Our Work', hint: 'Shown lower on the home page, below the price list.' },
];

@Component({
  selector: 'app-admin-gallery',
  imports: [FormsModule],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Home gallery</h1>
      <p class="text-sm text-slate-500 mb-4">Each tab is its own photo strip on the home page. Hidden photos aren't displayed.</p>

      <div class="flex gap-1 border-b border-slate-200 mb-6">
        @for (tab of tabs; track tab.key) {
          <button type="button" (click)="selectTab(tab.key)"
            class="px-4 py-2 text-sm font-medium border-b-2 -mb-px transition"
            [class]="activeTab() === tab.key ? 'border-primary text-primary' : 'border-transparent text-slate-500 hover:text-slate-800'">
            {{ tab.label }}
          </button>
        }
      </div>

      <div class="rounded-xl border border-slate-200 bg-white p-4 mb-4">
        <label class="text-xs font-semibold text-slate-500 uppercase">Section title <span class="normal-case font-normal">(shown on the home page)</span></label>
        <div class="flex gap-2 mt-1.5">
          <input [ngModel]="sectionTitle()" (ngModelChange)="sectionTitle.set($event)" name="sectionTitle" class="input flex-1" />
          <button type="button" (click)="saveTitle()" [disabled]="titleBusy() || titleLoading() || !sectionTitle().trim()" class="btn-primary shrink-0">
            {{ titleBusy() ? 'Saving…' : 'Save title' }}
          </button>
        </div>
        <p class="text-xs text-slate-500 mt-1.5">{{ activeTabHint() }}</p>
      </div>

      <div class="flex items-center justify-between mb-1">
        <h2 class="text-sm font-semibold text-slate-700">Photos</h2>
        <button type="button" (click)="add()" [disabled]="busy()" class="btn-primary shrink-0 ml-3">+ Add photo</button>
      </div>
      @if (message()) { <div class="mt-4 mb-2 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

      @if (loading()) { <div class="p-8 text-center text-slate-400">Loading…</div> }
      @else if (!images().length) { <div class="p-8 text-center text-slate-400">No photos yet. Click "Add photo" to create one.</div> }
      @else {
        <div class="space-y-3 mt-4">
          @for (g of images(); track g.galleryImageId; let i = $index) {
            <div class="bg-white border border-slate-200 rounded-xl p-3 flex gap-3" [class.opacity-60]="!g.isActive">
              <!-- reorder -->
              <div class="flex flex-col justify-center">
                <button type="button" (click)="move(i, -1)" [disabled]="i === 0" class="text-slate-400 hover:text-slate-700 disabled:opacity-30 leading-none">▲</button>
                <button type="button" (click)="move(i, 1)" [disabled]="i === images().length - 1" class="text-slate-400 hover:text-slate-700 disabled:opacity-30 leading-none">▼</button>
              </div>

              <!-- image -->
              <div class="shrink-0">
                <div class="w-24 h-24 rounded-lg overflow-hidden bg-slate-100 grid place-items-center">
                  @if (g.imageUrl) { <img [src]="g.imageUrl" alt="" class="w-full h-full object-cover" /> }
                  @else { <span class="text-xs text-slate-400">No image</span> }
                </div>
                <label class="mt-1.5 block text-center text-xs text-blue-600 hover:underline cursor-pointer">
                  {{ g.hasUpload ? 'Replace' : 'Upload' }}
                  <input type="file" accept="image/*" class="hidden" (change)="onFile(g, $event)" />
                </label>
              </div>

              <!-- fields -->
              <div class="flex-1 space-y-1.5">
                <input [(ngModel)]="g.title" [name]="'ti' + g.galleryImageId" placeholder="Title / alt text (optional)" class="input w-full" />
                <input [(ngModel)]="g.linkUrl" [name]="'l' + g.galleryImageId" placeholder="Link when clicked (optional, e.g. /order)" class="input w-full" />
                <input [(ngModel)]="g.imageUrl" [name]="'im' + g.galleryImageId" placeholder="…or paste an image URL"
                  [disabled]="g.hasUpload" class="input w-full text-xs" />
                <div class="flex items-center justify-between pt-0.5">
                  <label class="flex items-center gap-2 text-sm text-slate-600">
                    <input type="checkbox" [(ngModel)]="g.isActive" [name]="'a' + g.galleryImageId" /> Visible
                  </label>
                  <button type="button" (click)="remove(g)" class="text-sm text-red-500 hover:text-red-700">Delete</button>
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
export class AdminGalleryComponent implements OnInit {
  private readonly svc = inject(GalleryService);

  readonly tabs = TABS;
  readonly activeTab = signal<GallerySection>('new-designs');
  readonly images = signal<AdminGalleryImage[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);

  readonly sectionTitle = signal('');
  readonly titleLoading = signal(true);
  readonly titleBusy = signal(false);

  activeTabHint(): string {
    return this.tabs.find((t) => t.key === this.activeTab())?.hint ?? '';
  }

  ngOnInit(): void { this.reload(); this.reloadTitle(); }

  selectTab(section: GallerySection): void {
    if (section === this.activeTab()) return;
    this.activeTab.set(section);
    this.message.set(null);
    this.reload();
    this.reloadTitle();
  }

  private reload(): void {
    this.loading.set(true);
    this.svc.listAdmin(this.activeTab()).subscribe({
      next: (g) => { this.images.set(g); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  private reloadTitle(): void {
    this.titleLoading.set(true);
    this.svc.getSectionTitleAdmin(this.activeTab()).subscribe({
      next: (t) => { this.sectionTitle.set(t); this.titleLoading.set(false); },
      error: () => this.titleLoading.set(false),
    });
  }

  saveTitle(): void {
    const title = this.sectionTitle().trim();
    if (!title) return;
    this.titleBusy.set(true);
    this.svc.setSectionTitle(this.activeTab(), title).subscribe({
      next: () => { this.titleBusy.set(false); this.flash('Title saved.'); },
      error: () => { this.titleBusy.set(false); this.flash('Could not save the title.'); },
    });
  }

  add(): void {
    this.busy.set(true);
    this.svc.create({
      section: this.activeTab(), title: '', linkUrl: null, imageUrl: null,
      displayOrder: this.images().length + 1, isActive: true,
    }).subscribe({
      next: () => { this.busy.set(false); this.flash('Photo added.'); this.reload(); },
      error: () => { this.busy.set(false); this.flash('Add failed.'); },
    });
  }

  move(index: number, delta: number): void {
    const arr = [...this.images()];
    const target = index + delta;
    if (target < 0 || target >= arr.length) return;
    [arr[index], arr[target]] = [arr[target], arr[index]];
    this.images.set(arr);
  }

  onFile(g: AdminGalleryImage, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.busy.set(true);
    this.svc.uploadImage(g.galleryImageId, file).subscribe({
      next: () => { this.busy.set(false); this.flash('Image uploaded.'); this.reload(); },
      error: () => { this.busy.set(false); this.flash('Upload failed (max 5 MB, image files only).'); },
    });
    input.value = '';
  }

  remove(g: AdminGalleryImage): void {
    if (!confirm('Delete this photo?')) return;
    this.busy.set(true);
    this.svc.remove(g.galleryImageId).subscribe({
      next: () => { this.busy.set(false); this.flash('Photo deleted.'); this.reload(); },
      error: () => { this.busy.set(false); this.flash('Delete failed.'); },
    });
  }

  save(): void {
    this.busy.set(true);
    this.message.set(null);
    const updates = this.images().map((g, i) =>
      this.svc.update(g.galleryImageId, {
        section: g.section, title: g.title, linkUrl: g.linkUrl,
        // Only send imageUrl when it's an external URL the admin typed — never the resolved
        // /api/... upload URL (uploads are managed via the upload button).
        imageUrl: g.hasUpload ? null : g.imageUrl,
        displayOrder: i + 1, isActive: g.isActive,
      }),
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
    this.flash(failed ? 'Some photos failed to save.' : 'Gallery saved.');
    this.reload();
  }

  private flash(msg: string): void {
    this.message.set(msg);
  }
}
