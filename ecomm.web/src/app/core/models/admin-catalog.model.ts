// Request payloads for the admin catalog endpoints.

export interface ProductImageInput {
  url: string;
  altText?: string | null;
  displayOrder: number;
  isPrimary: boolean;
  mediaFileId?: number | null;
}

export interface SaveCategoryRequest {
  name: string;
  parentCategoryId?: number | null;
  slug?: string | null;
  description?: string | null;
  imageUrl?: string | null;
  displayOrder: number;
  isActive: boolean;
}

export interface SaveBrandRequest {
  name: string;
  slug?: string | null;
  logoUrl?: string | null;
  description?: string | null;
  isActive: boolean;
}

export interface SaveProductRequest {
  sku: string;
  name: string;
  slug?: string | null;
  categoryId: number;
  brandId?: number | null;
  price: number;
  compareAtPrice?: number | null;
  costPrice?: number | null;
  shortDescription?: string | null;
  description?: string | null;
  hsnCode?: string | null;
  status: string;
  isFeatured: boolean;
  productType?: string | null;
  tags?: string | null;
  metaTitle?: string | null;
  metaDescription?: string | null;
  images?: ProductImageInput[];
}

export interface VariantOptionInput {
  optionName: string;
  optionValue: string;
}

export interface SaveVariantRequest {
  sku: string;
  name?: string | null;
  priceAdjustment: number;
  isActive: boolean;
  options?: VariantOptionInput[];
}

export interface AttributeValueDef {
  attributeValueId: number;
  value: string;
}

export interface AttributeDef {
  attributeId: number;
  name: string;
  code: string;
  dataType: string;
  isFilterable: boolean;
  isActive: boolean;
  values: AttributeValueDef[];
}

export interface SaveAttributeRequest {
  name: string;
  code?: string | null;
  dataType: string;
  isFilterable: boolean;
  isActive: boolean;
}

export interface ProductAttributeInput {
  attributeId: number;
  attributeValueId?: number | null;
  valueText?: string | null;
}

export interface ProductAttributeValue {
  productAttributeValueId: number;
  attributeId: number;
  attributeName: string;
  attributeValueId: number | null;
  value: string | null;
  valueText: string | null;
}

export interface InventoryRow {
  productId: number;
  sku: string;
  name: string;
  availableQty: number;
  reservedQty: number;
  reorderLevel: number;
  isLowStock: boolean;
  hasVariants: boolean;
}

export interface VariantInventory {
  productVariantId: number;
  sku: string;
  name: string | null;
  availableQty: number;
  reservedQty: number;
  reorderLevel: number;
  isLowStock: boolean;
}

export interface InventoryTransaction {
  inventoryTransactionId: number;
  changeQty: number;
  balanceAfter: number | null;
  transactionType: string;
  notes: string | null;
  createdAt: string;
}

export interface StoreSettings {
  taxMode: string;               // Exclusive | Inclusive | None
  storeState: string | null;
  storeGstin: string | null;
  storeLegalName: string | null;
  codEnabled: boolean;
  storeEmail: string | null;
  storePhone: string | null;
  storeAddress: string | null;
  timezone: string | null;
}

export interface DashboardSummary {
  ordersToday: number; ordersThisWeek: number;
  revenueToday: number; revenueThisWeek: number; aovThisWeek: number;
  newSignupsToday: number; newSignupsThisWeek: number;
  lowStockCount: number; pendingActionCount: number;
  topSearches: { term: string; count: number }[];
}
export interface ChecklistItem {
  key: string; label: string; description: string; done: boolean; actionLabel: string; actionLink: string;
  /** Optional live detail (connected domain, provider, product count) shown beside the row. */
  currentValue?: string | null;
}
export interface Dashboard {
  summary: DashboardSummary;
  checklist: ChecklistItem[];
  checklistDone: number;
  checklistTotal: number;
}
export interface TestOrderResult {
  orderId: number; orderNumber: string; productName: string; totalAmount: number;
}

export interface InventoryImportResult {
  total: number;
  success: number;
  failed: number;
  errors: { rowNumber: number; message: string }[];
}

export interface ImportJobResult {
  job: {
    importJobId: number;
    jobType: string;
    fileName: string | null;
    status: string;
    totalRows: number;
    successRows: number;
    failedRows: number;
    createdAt: string;
    completedAt: string | null;
  };
  failedRows: { rowNumber: number; status: string; errorMessage: string | null }[];
}
