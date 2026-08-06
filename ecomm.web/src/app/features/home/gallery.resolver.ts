import { inject } from '@angular/core';
import { ResolveFn } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { GalleryImage } from '../../core/models/gallery.model';
import { GalleryService } from '../../core/services/gallery.service';

export interface GalleryData {
  images: GalleryImage[];
}

/** Preloads the home gallery strip before route activation — same SSR-hydration rationale as homeResolver. */
export const galleryResolver: ResolveFn<GalleryData> = () => {
  const gallery = inject(GalleryService);
  return gallery.getImages().pipe(
    map((images) => ({ images })),
    catchError(() => of({ images: [] as GalleryImage[] })),
  );
};
