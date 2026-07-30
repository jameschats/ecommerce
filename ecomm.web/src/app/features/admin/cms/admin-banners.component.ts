import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminBanner, BannerPage } from '../../../core/models/banner.model';
import { BannerService } from '../../../core/services/banner.service';

interface PageTab { key: BannerPage; label: string; }

const TABS: PageTab[] = [
  { key: 'home', label: 'Home' },
  { key: 'order', label: 'Order Now' },
  { key: 'finished-calendar', label: 'Finished Calendar' },
  { key: 'about', label: 'About Us' },
];

@Component({
  selector: 'app-admin-banners',
  imports: [FormsModule],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Page banners</h1>
      <p class="text-sm text-slate-500 mb-4">Each tab is a page's own banner carousel. Upload an image (or paste a URL), set the text, link and order. Hidden banners aren't displayed.</p>

      <div class="flex gap-1 border-b border-slate-200 mb-6">
        @for (tab of tabs; track tab.key) {
          <button type="button" (click)="selectTab(tab.key)"
            class="px-4 py-2 text-sm font-medium border-b-2 -mb-px transition"
            [class]="activeTab() === tab.key ? 'border-primary text-primary' : 'border-transparent text-slate-500 hover:text-slate-800'">
            {{ tab.label }}
          </button>
        }
      </div>

      <div class="flex items-center justify-between mb-4">
        <p class="text-sm text-slate-500">Banners shown on the {{ activeTabLabel() }} page.</p>
        <button type="button" (click)="add()" [disabled]="busy()" class="btn-primary">+ Add banner</button>
      </div>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

      @if (loading()) { <div class="p-8 text-center text-slate-400">Loading…</div> }
      @else if (!banners().length) { <div class="p-8 text-center text-slate-400">No banners yet. Click "Add banner" to create one.</div> }
      @else {
        <div class="space-y-3">
          @for (b of banners(); track b.homeBannerId; let i = $index) {
            <div class="bg-white border border-slate-200 rounded-xl p-3 flex gap-3" [class.opacity-60]="!b.isActive">
              <!-- reorder -->
              <div class="flex flex-col justify-center">
                <button type="button" (click)="move(i, -1)" [disabled]="i === 0" class="text-slate-400 hover:text-slate-700 disabled:opacity-30 leading-none">▲</button>
                <button type="button" (click)="move(i, 1)" [disabled]="i === banners().length - 1" class="text-slate-400 hover:text-slate-700 disabled:opacity-30 leading-none">▼</button>
              </div>

              <!-- image -->
              <div class="shrink-0">
                <div class="w-40 h-24 rounded-lg overflow-hidden bg-slate-100 grid place-items-center">
                  @if (b.imageUrl) { <img [src]="b.imageUrl" alt="" class="w-full h-full object-cover" /> }
                  @else { <span class="text-xs text-slate-400">No image</span> }
                </div>
                <label class="mt-1.5 block text-center text-xs text-blue-600 hover:underline cursor-pointer">
                  {{ b.hasUpload ? 'Replace image' : 'Upload image' }}
                  <input type="file" accept="image/*" class="hidden" (change)="onFile(b, $event)" />
                </label>
              </div>

              <!-- fields -->
              <div class="flex-1 space-y-1.5">
                <input [(ngModel)]="b.title" [name]="'ti' + b.homeBannerId" placeholder="Title" class="input w-full" />
                <input [(ngModel)]="b.subtitle" [name]="'su' + b.homeBannerId" placeholder="Subtitle" class="input w-full" />
                <div class="flex gap-2">
                  <input [(ngModel)]="b.ctaText" [name]="'c' + b.homeBannerId" placeholder="Button text" class="input w-1/3" />
                  <input [(ngModel)]="b.linkUrl" [name]="'l' + b.homeBannerId" placeholder="Link (e.g. /products)" class="input flex-1" />
                </div>
                <input [(ngModel)]="b.imageUrl" [name]="'im' + b.homeBannerId" placeholder="…or paste an image URL"
                  [disabled]="b.hasUpload" class="input w-full text-xs" />
                <div class="flex items-center justify-between pt-0.5">
                  <label class="flex items-center gap-2 text-sm text-slate-600">
                    <input type="checkbox" [(ngModel)]="b.isActive" [name]="'a' + b.homeBannerId" /> Visible
                  </label>
                  <button type="button" (click)="remove(b)" class="text-sm text-red-500 hover:text-red-700">Delete</button>
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
export class AdminBannersComponent implements OnInit {
  private readonly svc = inject(BannerService);

  readonly tabs = TABS;
  readonly activeTab = signal<BannerPage>('home');
  readonly banners = signal<AdminBanner[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);

  activeTabLabel(): string {
    return this.tabs.find((t) => t.key === this.activeTab())?.label ?? '';
  }

  ngOnInit(): void { this.reload(); }

  selectTab(page: BannerPage): void {
    if (page === this.activeTab()) return;
    this.activeTab.set(page);
    this.message.set(null);
    this.reload();
  }

  private reload(): void {
    this.loading.set(true);
    this.svc.listAdmin(this.activeTab()).subscribe({
      next: (b) => { this.banners.set(b); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  add(): void {
    this.busy.set(true);
    this.svc.create({
      page: this.activeTab(), title: 'New banner', subtitle: '', ctaText: 'Shop now', linkUrl: '/products',
      imageUrl: null, displayOrder: this.banners().length + 1, isActive: true,
    }).subscribe({
      next: () => { this.busy.set(false); this.flash('Banner added.'); this.reload(); },
      error: () => { this.busy.set(false); this.flash('Add failed.'); },
    });
  }

  move(index: number, delta: number): void {
    const arr = [...this.banners()];
    const target = index + delta;
    if (target < 0 || target >= arr.length) return;
    [arr[index], arr[target]] = [arr[target], arr[index]];
    this.banners.set(arr);
  }

  onFile(b: AdminBanner, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.busy.set(true);
    this.svc.uploadImage(b.homeBannerId, file).subscribe({
      next: () => { this.busy.set(false); this.flash('Image uploaded.'); this.reload(); },
      error: () => { this.busy.set(false); this.flash('Upload failed (max 5 MB, image files only).'); },
    });
    input.value = '';
  }

  remove(b: AdminBanner): void {
    if (!confirm('Delete this banner?')) return;
    this.busy.set(true);
    this.svc.remove(b.homeBannerId).subscribe({
      next: () => { this.busy.set(false); this.flash('Banner deleted.'); this.reload(); },
      error: () => { this.busy.set(false); this.flash('Delete failed.'); },
    });
  }

  save(): void {
    this.busy.set(true);
    this.message.set(null);
    const updates = this.banners().map((b, i) =>
      this.svc.update(b.homeBannerId, {
        page: b.page, title: b.title, subtitle: b.subtitle, ctaText: b.ctaText, linkUrl: b.linkUrl,
        // Only send imageUrl when it's an external URL the admin typed — never the resolved
        // /api/... upload URL (uploads are managed via the upload button).
        imageUrl: b.hasUpload ? null : b.imageUrl,
        displayOrder: i + 1, isActive: b.isActive,
      }),
    );
    // Fire sequentially-safe: use forkJoin-like Promise.all via subscriptions.
    let done = 0, failed = false;
    updates.forEach((o) => o.subscribe({
      next: () => { if (++done === updates.length) this.finishSave(failed); },
      error: () => { failed = true; if (++done === updates.length) this.finishSave(failed); },
    }));
    if (!updates.length) this.finishSave(false);
  }

  private finishSave(failed: boolean): void {
    this.busy.set(false);
    this.flash(failed ? 'Some banners failed to save.' : 'Banners saved.');
    this.reload();
  }

  private flash(msg: string): void {
    this.message.set(msg);
  }
}
