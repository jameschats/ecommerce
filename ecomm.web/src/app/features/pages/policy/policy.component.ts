import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { CatalogService, StorePolicy } from '../../../core/services/catalog.service';
import { SeoService } from '../../../core/services/seo.service';
import { SITE_URL } from '../../../core/api.config';

@Component({
  selector: 'app-policy',
  template: `
    <div class="page-container py-10 max-w-3xl">
      @if (loading()) { <p class="text-slate-400">Loading…</p> }
      @else if (policy(); as p) {
        <h1 class="text-2xl font-bold text-slate-900 mb-5">{{ p.title }}</h1>
        <div class="prose max-w-none text-slate-700" [innerHTML]="p.bodyHtml"></div>
      } @else {
        <h1 class="text-2xl font-bold text-slate-900">Not found</h1>
        <p class="text-slate-500 mt-2">This policy hasn't been published.</p>
      }
    </div>
  `,
})
export class PolicyComponent implements OnInit {
  private readonly catalog = inject(CatalogService);
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);

  readonly policy = signal<StorePolicy | null>(null);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.route.paramMap.subscribe((pm) => {
      const handle = pm.get('handle') ?? '';
      this.loading.set(true);
      this.catalog.getPolicy(handle).subscribe((p) => {
        this.policy.set(p);
        this.loading.set(false);
        if (p) this.seo.setMeta({ title: `${p.title} — CalendarShop`, description: p.title, url: `${SITE_URL}/policies/${handle}` });
      });
    });
  }
}
