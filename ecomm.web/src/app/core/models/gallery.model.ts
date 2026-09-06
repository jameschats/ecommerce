/** Home-page galleries this photo can belong to. */
export type GallerySection = 'new-designs' | 'featured';

/** A storefront gallery photo (image URL already resolved by the service). */
export interface GalleryImage {
  galleryImageId: number;
  imageUrl: string | null;
  title: string | null;
  link: string | null;
}

/** Full gallery row for the admin editor. */
export interface AdminGalleryImage {
  galleryImageId: number;
  section: GallerySection;
  title: string | null;
  linkUrl: string | null;
  imageUrl: string | null;
  hasUpload: boolean;
  displayOrder: number;
  isActive: boolean;
}

export interface SaveGalleryImageRequest {
  section: GallerySection;
  title: string | null;
  linkUrl: string | null;
  imageUrl: string | null;
  displayOrder: number;
  isActive: boolean;
}
