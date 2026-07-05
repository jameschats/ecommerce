import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Title, Meta } from '@angular/platform-browser';
import { CmsService, PageDetail } from '../../core/services/cms.service';
import { StorefrontSectionComponent } from '../storefront/storefront-section.component';

/** Data-driven custom page (/pages/:slug) — renders builder sections in order. */
@Component({
  selector: 'app-page',
  imports: [StorefrontSectionComponent],
  template: `
    @if (loading()) {
      <div class="p-16 text-center text-slate-400">Loading…</div>
    } @else if (page(); as p) {
      <div class="pb-10">
        @for (sec of p.sections; track sec.pageSectionId) {
          <app-storefront-section [section]="sec" />
        }
        @if (!p.sections.length) { <div class="p-16 text-center text-slate-400">This page has no content yet.</div> }
      </div>
    } @else {
      <div class="p-16 text-center text-slate-400">Page not found.</div>
    }
  `,
})
export class PageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly cms = inject(CmsService);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  readonly page = signal<PageDetail | null>(null);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.route.paramMap.subscribe((pm) => {
      const slug = pm.get('slug') ?? '';
      this.loading.set(true);
      this.cms.getPage(slug).subscribe((p) => {
        this.page.set(p);
        this.loading.set(false);
        if (p?.page.metaTitle) this.title.setTitle(p.page.metaTitle);
        if (p?.page.metaDescription) this.meta.updateTag({ name: 'description', content: p.page.metaDescription });
      });
    });
  }
}
