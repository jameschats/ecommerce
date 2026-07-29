import { Injectable, signal } from '@angular/core';

/** Which product (by slug) the global Quick View modal should show, if any. Opened from any
 *  product card without needing to thread state through the page component tree. */
@Injectable({ providedIn: 'root' })
export class QuickViewService {
  readonly slug = signal<string | null>(null);

  open(slug: string): void { this.slug.set(slug); }
  close(): void { this.slug.set(null); }
}
