import { inject } from '@angular/core';
import { ResolveFn } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { GalleryImage, GallerySection } from '../../core/models/gallery.model';
import { GalleryService } from '../../core/services/gallery.service';

export interface GalleryData {
  images: GalleryImage[];
}

/** Preloads one home gallery section before route activation — same SSR-hydration rationale as homeResolver. */
export function galleryResolver(section: GallerySection): ResolveFn<GalleryData> {
  return () => {
    const gallery = inject(GalleryService);
    return gallery.getImages(section).pipe(
      map((images) => ({ images })),
      catchError(() => of({ images: [] as GalleryImage[] })),
    );
  };
}
