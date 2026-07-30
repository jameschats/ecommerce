/** A storefront hero banner (image URL already resolved by the service). */
export interface HomeBanner {
  homeBannerId: number;
  imageUrl: string | null;
  title: string | null;
  subtitle: string | null;
  cta: string | null;
  link: string | null;
}

/** Storefront pages that can have their own banners. */
export type BannerPage = 'home' | 'order' | 'finished-calendar' | 'about';

/** Full banner row for the admin editor. */
export interface AdminBanner {
  homeBannerId: number;
  page: BannerPage;
  title: string | null;
  subtitle: string | null;
  ctaText: string | null;
  linkUrl: string | null;
  imageUrl: string | null;
  hasUpload: boolean;
  displayOrder: number;
  isActive: boolean;
}

export interface SaveBannerRequest {
  page: BannerPage;
  title: string | null;
  subtitle: string | null;
  ctaText: string | null;
  linkUrl: string | null;
  imageUrl: string | null;
  displayOrder: number;
  isActive: boolean;
}
