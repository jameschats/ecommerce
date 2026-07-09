import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminCollection, CollectionAdminService } from '../../../core/services/collection-admin.service';

@Component({
  selector: 'app-admin-collections',
  imports: [RouterLink],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <div class="flex items-center justify-between mb-1">
        <h1 class="text-xl font-bold text-slate-900">Collections</h1>
        <a routerLink="/admin/collections/new" class="btn-primary">+ Add collection</a>
      </div>
      <p class="text-sm text-slate-500 mb-4">Group products for merchandising — a fixed list, or automatically by rules. Show them on your storefront.</p>

      <div class="bg-white border border-slate-200 rounded-xl overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-slate-50 text-slate-500 text-left">
            <tr><th class="px-4 py-2 font-medium">Collection</th><th class="px-4 py-2 font-medium">Type</th><th class="px-4 py-2 font-medium text-right">Products</th><th class="px-4 py-2 font-medium">Status</th></tr>
          </thead>
          <tbody class="divide-y divide-slate-100">
            @for (c of collections(); track c.collectionId) {
              <tr class="hover:bg-slate-50">
                <td class="px-4 py-3"><a [routerLink]="['/admin/collections', c.collectionId]" class="font-medium text-blue-600 hover:underline">{{ c.name }}</a><div class="text-xs text-slate-400">/collection/{{ c.slug }}</div></td>
                <td class="px-4 py-3 text-slate-600">{{ c.collectionType }}</td>
                <td class="px-4 py-3 text-right text-slate-700">{{ c.productCount }}</td>
                <td class="px-4 py-3"><span class="text-xs px-1.5 py-0.5 rounded" [class]="c.isActive ? 'bg-green-50 text-green-700 border border-green-200' : 'bg-slate-100 text-slate-500'">{{ c.isActive ? 'Active' : 'Off' }}</span></td>
              </tr>
            }
            @if (!loading() && !collections().length) { <tr><td colspan="4" class="px-4 py-10 text-center text-slate-400">No collections yet.</td></tr> }
          </tbody>
        </table>
        @if (loading()) { <div class="p-6 text-center text-slate-400 text-sm">Loading…</div> }
      </div>
    </div>
  `,
})
export class AdminCollectionsComponent implements OnInit {
  private readonly api = inject(CollectionAdminService);
  readonly collections = signal<AdminCollection[]>([]);
  readonly loading = signal(true);
  ngOnInit(): void { this.api.list().subscribe({ next: (c) => { this.collections.set(c); this.loading.set(false); }, error: () => this.loading.set(false) }); }
}
