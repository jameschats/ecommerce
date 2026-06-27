import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CmsService, HomeSection } from '../../../core/services/cms.service';

@Component({
  selector: 'app-admin-cms',
  imports: [FormsModule],
  template: `
    <div class="max-w-2xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Home page</h1>
      <p class="text-sm text-slate-500 mb-6">Reorder, rename, or hide the sections shown on the storefront home page.</p>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

      @if (loading()) { <div class="p-8 text-center text-slate-400">Loading…</div> }
      @else {
        <div class="space-y-2">
          @for (s of sections(); track s.pageSectionId; let i = $index) {
            <div class="bg-white border border-slate-200 rounded-xl p-3 flex items-center gap-3"
                 [class.opacity-60]="!s.isVisible">
              <div class="flex flex-col">
                <button type="button" (click)="move(i, -1)" [disabled]="i === 0" class="text-slate-400 hover:text-slate-700 disabled:opacity-30 leading-none">▲</button>
                <button type="button" (click)="move(i, 1)" [disabled]="i === sections().length - 1" class="text-slate-400 hover:text-slate-700 disabled:opacity-30 leading-none">▼</button>
              </div>
              <div class="flex-1">
                <div class="text-xs text-slate-400">{{ label(s.sectionType) }}</div>
                <input [(ngModel)]="s.title" [name]="'t' + s.pageSectionId" placeholder="Section title"
                  class="w-full text-sm border-0 border-b border-transparent focus:border-slate-300 focus:outline-none px-0 py-1" />
              </div>
              <label class="flex items-center gap-2 text-sm text-slate-600">
                <input type="checkbox" [(ngModel)]="s.isVisible" [name]="'v' + s.pageSectionId" /> Visible
              </label>
            </div>
          }
        </div>
        <button type="button" (click)="save()" [disabled]="saving()" class="btn-primary mt-4">{{ saving() ? 'Saving…' : 'Save layout' }}</button>
      }
    </div>
  `,
})
export class AdminCmsComponent implements OnInit {
  private readonly cms = inject(CmsService);

  readonly sections = signal<HomeSection[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly message = signal<string | null>(null);

  private readonly labels: Record<string, string> = {
    Banner: 'Hero banner', Categories: 'Category strip', FeaturedProducts: 'Featured products',
    NewArrivals: 'New arrivals', BestSellers: 'Best sellers', CustomHtml: 'Custom HTML',
  };

  ngOnInit(): void {
    this.cms.getAdminHomeSections().subscribe({
      next: (s) => { this.sections.set(s); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  label(type: string): string {
    return this.labels[type] ?? type;
  }

  move(index: number, delta: number): void {
    const arr = [...this.sections()];
    const target = index + delta;
    if (target < 0 || target >= arr.length) return;
    [arr[index], arr[target]] = [arr[target], arr[index]];
    this.sections.set(arr);
  }

  save(): void {
    this.saving.set(true);
    this.message.set(null);
    const items = this.sections().map((s, i) => ({
      pageSectionId: s.pageSectionId,
      displayOrder: i + 1,
      isVisible: s.isVisible,
      title: s.title,
    }));
    this.cms.updateHomeSections(items).subscribe({
      next: () => { this.saving.set(false); this.message.set('Home layout saved.'); },
      error: () => { this.saving.set(false); this.message.set('Save failed.'); },
    });
  }
}
