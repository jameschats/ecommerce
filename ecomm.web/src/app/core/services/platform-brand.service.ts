import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';

/**
 * The platform's own fixed marketing/auth brand fonts (Sora/Manrope/JetBrains Mono) — used by
 * pages that render the platform's own header (landing, login on the apex host, signup), which
 * are never Theme-Engine driven since a tenant's brand fonts don't apply on the apex. Mirrors
 * ThemeService.loadFonts' link-injection pattern, but for this one fixed font set rather than a
 * tenant-selectable allowlist.
 */
@Injectable({ providedIn: 'root' })
export class PlatformBrandService {
  private readonly document = inject(DOCUMENT);
  private loaded = false;

  loadFonts(): void {
    if (this.loaded) return;
    this.loaded = true;
    const href = 'https://fonts.googleapis.com/css2?family=Sora:wght@600;700;800&family=Manrope:wght@400;500;600;700;800&family=JetBrains+Mono:wght@500;700&display=swap';
    if (this.document.querySelector(`link[href="${href}"]`)) return;
    const link = this.document.createElement('link');
    link.rel = 'stylesheet';
    link.href = href;
    this.document.head.appendChild(link);
  }
}
