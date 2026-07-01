/** A storefront hero banner (image URL already resolved by the service). */
export interface HomeBanner {
  homeBannerId: number;
  imageUrl: string | null;
  title: string | null;
  subtitle: string | null;
  cta: string | null;
  link: string | null;
}

/** Full banner row for the admin editor. */
export interface AdminBanner {
  homeBannerId: number;
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
  title: string | null;
  subtitle: string | null;
  ctaText: string | null;
  linkUrl: string | null;
  imageUrl: string | null;
  displayOrder: number;
  isActive: boolean;
}
