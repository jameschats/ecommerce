export interface CustomerListItem {
  userId: number;
  fullName: string | null;
  email: string | null;
  phoneNumber: string | null;
  orderCount: number;
  totalSpent: number;
  lastOrderAt: string | null;
  acceptsEmailMarketing: boolean;
  tags: string[];
  createdAt: string;
}

export interface TagCount {
  tag: string;
  count: number;
}

export interface CustomerImportResult {
  total: number;
  created: number;
  updated: number;
  skipped: number;
  errors: string[];
}

export interface CustomerAddress {
  customerAddressId: number;
  label: string | null;
  recipientName: string | null;
  phone: string | null;
  line1: string;
  line2: string | null;
  city: string;
  state: string;
  pincode: string;
  country: string;
  isDefault: boolean;
}

export interface CustomerOrder {
  orderId: number;
  orderNumber: string;
  status: string;
  totalAmount: number;
  placedAt: string | null;
}

export interface CustomerDetail {
  userId: number;
  fullName: string | null;
  email: string | null;
  phoneNumber: string | null;
  isActive: boolean;
  isEmailVerified: boolean;
  createdAt: string;
  acceptsEmailMarketing: boolean;
  acceptsSmsMarketing: boolean;
  acceptsWhatsappMarketing: boolean;
  notes: string | null;
  tags: string[];
  orderCount: number;
  totalSpent: number;
  lastOrderAt: string | null;
  addresses: CustomerAddress[];
  recentOrders: CustomerOrder[];
}

export interface CustomerSegment {
  key: string;
  label: string;
  count: number;
}

export interface CreateCustomerRequest {
  fullName: string | null;
  email: string | null;
  phoneNumber: string | null;
  acceptsEmailMarketing: boolean;
  acceptsSmsMarketing: boolean;
  acceptsWhatsappMarketing: boolean;
  notes: string | null;
  tags: string | null;   // comma-separated
}

export interface UpdateCustomerRequest {
  fullName: string | null;
  phoneNumber: string | null;
  acceptsEmailMarketing: boolean;
  acceptsSmsMarketing: boolean;
  acceptsWhatsappMarketing: boolean;
  notes: string | null;
  tags: string | null;   // comma-separated
}
