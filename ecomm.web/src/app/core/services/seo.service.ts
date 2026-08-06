import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';

export interface SeoData {
  title: string;
  description?: string;
  image?: string;
  url?: string;
  type?: string;
  /** Comma-separated. Ignored by Google, still read by some AI crawlers and cheap to emit. */
  keywords?: string;
}

/**
 * Sets per-page SEO: title, meta description, Open Graph + Twitter tags,
 * canonical link, and JSON-LD structured data. SSR injects these into the
 * server-rendered HTML so crawlers and link previews see them.
 */
@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly doc = inject(DOCUMENT);

  setMeta(data: SeoData): void {
    const desc = data.description ?? '';
    this.title.setTitle(data.title);
    this.meta.updateTag({ name: 'description', content: desc });
    if (data.keywords) this.meta.updateTag({ name: 'keywords', content: data.keywords });
    this.meta.updateTag({ property: 'og:title', content: data.title });
    this.meta.updateTag({ property: 'og:description', content: desc });
    this.meta.updateTag({ property: 'og:type', content: data.type ?? 'website' });
    this.meta.updateTag({ name: 'twitter:card', content: data.image ? 'summary_large_image' : 'summary' });
    this.meta.updateTag({ name: 'twitter:title', content: data.title });
    this.meta.updateTag({ name: 'twitter:description', content: desc });

    if (data.image) {
      this.meta.updateTag({ property: 'og:image', content: data.image });
      this.meta.updateTag({ name: 'twitter:image', content: data.image });
    } else {
      this.meta.removeTag("property='og:image'");
      this.meta.removeTag("name='twitter:image'");
    }
    if (data.url) {
      this.meta.updateTag({ property: 'og:url', content: data.url });
      this.setCanonical(data.url);
    }
  }

  setCanonical(url: string): void {
    let link = this.doc.querySelector("link[rel='canonical']") as HTMLLinkElement | null;
    if (!link) {
      link = this.doc.createElement('link');
      link.setAttribute('rel', 'canonical');
      this.doc.head.appendChild(link);
    }
    link.setAttribute('href', url);
  }

  setJsonLd(data: unknown): void {
    const id = 'jsonld-structured-data';
    let script = this.doc.getElementById(id) as HTMLScriptElement | null;
    if (!script) {
      script = this.doc.createElement('script');
      script.id = id;
      script.setAttribute('type', 'application/ld+json');
      this.doc.head.appendChild(script);
    }
    script.textContent = JSON.stringify(data);
  }

  clearJsonLd(): void {
    const script = this.doc.getElementById('jsonld-structured-data');
    if (script) script.textContent = '';
  }
}
