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
  designNo: string | null;
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
}

export interface ProductDetail {
  productId: number;
  sku: string;
  /** Trade-facing design number. Distinct from SKU; blank on pre-import products. */
  designNo?: string | null;
  name: string;
  slug: string;
  shortDescription: string | null;
  description: string | null;
  /** Search and AI-crawler metadata. Null falls back to the name and short description. */
  metaTitle?: string | null;
  metaDescription?: string | null;
  metaKeywords?: string | null;
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
}

export interface ProductQuery {
  search?: string;
  categoryId?: number;
  brandId?: number;
  isFeatured?: boolean;
  sort?: string;
  page?: number;
  pageSize?: number;
}
