export interface Category {
  categoryId: number;
  parentCategoryId: number | null;
  name: string;
  slug: string;
  description: string | null;
  imageUrl: string | null;
  displayOrder: number;
  isActive: boolean;
}

export interface Brand {
  brandId: number;
  name: string;
  slug: string;
  logoUrl: string | null;
  description: string | null;
  isActive: boolean;
}

export interface ProductImage {
  productImageId: number;
  url: string;
  altText: string | null;
  displayOrder: number;
  isPrimary: boolean;
}

export interface VariantOption {
  optionName: string;
  optionValue: string;
}

export interface ProductVariant {
  productVariantId: number;
  sku: string;
  name: string | null;
  priceAdjustment: number;
  isActive: boolean;
  options: VariantOption[];
}

export interface ProductAttributeValue {
  productAttributeValueId: number;
  attributeId: number;
  attributeName: string;
  attributeValueId: number | null;
  value: string | null;
  valueText: string | null;
}

export interface ProductListItem {
  productId: number;
  sku: string;
  name: string;
  slug: string;
  price: number;
  compareAtPrice: number | null;
  status: string;
  isFeatured: boolean;
  primaryImageUrl: string | null;
  categoryName: string;
  brandName: string | null;
  inStock: boolean;
  availableQty: number;
  isLowStock: boolean;
  colorOptions: string[];
  createdAt: string;
  secondaryImageUrl: string | null;
  rating?: number;
  reviewCount?: number;
}

export interface ProductDetail {
  productId: number;
  sku: string;
  name: string;
  slug: string;
  shortDescription: string | null;
  description: string | null;
  price: number;
  compareAtPrice: number | null;
  costPrice: number | null;
  hsnCode: string | null;
  status: string;
  isFeatured: boolean;
  isActive: boolean;
  categoryId: number;
  categoryName: string;
  brandId: number | null;
  brandName: string | null;
  availableQty: number;
  inStock: boolean;
  images: ProductImage[];
  variants: ProductVariant[];
  attributes: ProductAttributeValue[];
  productType: string | null;
  tags: string | null;
  metaTitle: string | null;
  metaDescription: string | null;
  isBundle: boolean;
}

export interface BundleComponent {
  componentProductId: number;
  componentVariantId: number | null;
  name: string;
  slug: string;
  variantLabel: string | null;
  imageUrl: string | null;
  quantity: number;
  availableQty: number;
}

export interface ProductQuery {
  search?: string;
  categoryId?: number;
  brandId?: number;
  isFeatured?: boolean;
  sort?: string;
  page?: number;
  pageSize?: number;
  ids?: number[];
  minPrice?: number;
  maxPrice?: number;
  // Faceted filters (multi-value)
  brandIds?: number[];
  color?: string[];
  size?: string[];
  attr?: string[];        // "code:value"
  inStock?: boolean;
  onSale?: boolean;
  minRating?: number;
}

// ---- Facets ----
export interface BrandFacet { brandId: number; name: string; count: number; }
export interface ValueFacet { value: string; count: number; hex: string | null; }
export interface AttributeFacet { code: string; name: string; values: ValueFacet[]; }
export interface Facets {
  total: number;
  brands: BrandFacet[];
  colors: ValueFacet[];
  sizes: ValueFacet[];
  attributes: AttributeFacet[];
  priceMin: number;
  priceMax: number;
  ratingCounts: number[];   // index 0 = 1★+, … index 4 = 5★
  inStockCount: number;
  onSaleCount: number;
}
