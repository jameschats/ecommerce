import { Directive, ElementRef, effect, inject, input } from '@angular/core';

/** Must match ImageVariantService.Widths on the API (ecomm.api/Features/Media/ImageVariantService.cs) —
 *  the deterministic naming scheme both sides rely on. */
const VARIANT_WIDTHS = [400, 800, 1600];
const UPLOAD_PATH_MARKER = '/uploads/';

/** Builds a WebP `srcset` for one of our own uploaded images from its deterministic `{stem}-{w}w.webp`
 *  siblings, or null for anything else (external/CDN URLs, e.g. the Unsplash placeholders theme demo
 *  content uses — those already carry their own `?w=` resizing and shouldn't get a fake srcset). */
export function buildWebpSrcset(url: string | null | undefined): string | null {
  if (!url || !url.includes(UPLOAD_PATH_MARKER)) return null;
  const dot = url.lastIndexOf('.');
  if (dot < 0) return null;
  const stem = url.slice(0, dot);
  return VARIANT_WIDTHS.map((w) => `${stem}-${w}w.webp ${w}w`).join(', ');
}

/**
 * Drop-in addition to an existing `<img [src]="url">` — adds a right-sized WebP `srcset` alongside it
 * with zero changes to `src`, `class`, or any other binding on the element. Usage:
 *   <img [src]="url" [appImgSrc]="url" class="..." loading="lazy" />
 *
 * Deliberately a directive on the existing tag, not a wrapping component: this codebase leans on
 * fine-grained Tailwind class toggles (`[class.group-hover:opacity-0]`, `[class]` ternaries) directly
 * on `<img>` elements throughout the storefront, which a wrapper component's host element can't forward
 * without its own bespoke plumbing per call site. A same-element directive has none of that risk.
 *
 * If a srcset candidate 404s (image uploaded before variants existed, and not yet backfilled via
 * `POST /api/admin/media/backfill-variants`), the browser silently ignores that candidate and falls
 * back to `src` — never worse than today's plain `<img>`.
 */
@Directive({ selector: 'img[appImgSrc]' })
export class ResponsiveImgDirective {
  private readonly el = inject(ElementRef<HTMLImageElement>);

  readonly appImgSrc = input<string | null | undefined>();
  readonly appImgSizes = input('100vw');

  constructor() {
    effect(() => {
      const srcset = buildWebpSrcset(this.appImgSrc());
      const img = this.el.nativeElement;
      if (srcset) {
        img.setAttribute('srcset', srcset);
        img.setAttribute('sizes', this.appImgSizes());
      } else {
        img.removeAttribute('srcset');
        img.removeAttribute('sizes');
      }
    });
  }
}
