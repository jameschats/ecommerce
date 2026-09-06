import { inject } from '@angular/core';
import { ResolveFn } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { GalleryImage, GallerySection } from '../../core/models/gallery.model';
import { GalleryService } from '../../core/services/gallery.service';

export interface GalleryData {
  images: GalleryImage[];
  title: string;
}

/** Preloads one home gallery section (photos + its admin-editable title) before route
 *  activation — same SSR-hydration rationale as homeResolver. */
export function galleryResolver(section: GallerySection, fallbackTitle: string): ResolveFn<GalleryData> {
  return () => {
    const gallery = inject(GalleryService);
    return forkJoin({
      images: gallery.getImages(section),
      title: gallery.getSectionTitle(section, fallbackTitle),
    }).pipe(
      catchError(() => of({ images: [] as GalleryImage[], title: fallbackTitle })),
    );
  };
}
