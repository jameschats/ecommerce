export interface Supplier {
  supplierId: number;
  name: string;
  code: string | null;
  contactName: string | null;
  email: string | null;
  phone: string | null;
  gstin: string | null;
  city: string | null;
  state: string | null;
  leadTimeDays: number | null;
  isActive: boolean;
}

export interface SaveSupplierRequest {
  name: string;
  code: string | null;
  contactName: string | null;
  email: string | null;
  phone: string | null;
  gstin: string | null;
  addressLine1: string | null;
  city: string | null;
  state: string | null;
  pincode: string | null;
  paymentTerms: string | null;
  leadTimeDays: number | null;
  notes: string | null;
  isActive: boolean;
}

export interface ProductSupplier {
  productSupplierId: number;
  supplierId: number;
  supplierName: string;
  supplierSku: string | null;
  costPrice: number | null;
  leadTimeDays: number | null;
  isPrimary: boolean;
}

export interface ProductSupplierInput {
  supplierId: number;
  supplierSku: string | null;
  costPrice: number | null;
  leadTimeDays: number | null;
  isPrimary: boolean;
}
