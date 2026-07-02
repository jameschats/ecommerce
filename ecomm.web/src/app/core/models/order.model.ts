export interface QuoteLine {
  productId: number;
  productVariantId: number | null;
  name: string;
  variantLabel: string | null;
  quantity: number;
  unitPrice: number;
  lineSubtotal: number;
  taxRate: number;
  taxAmount: number;
  availableQty: number;
  inStock: boolean;
}

export interface CheckoutQuote {
  serviceable: boolean;
  message: string | null;
  lines: QuoteLine[];
  subtotal: number;
  taxAmount: number;
  cgst: number;
  sgst: number;
  igst: number;
  interState: boolean;
  shippingCharge: number;
  shippingMethod: string;
  estimatedDays: number | null;
  total: number;
  shippingAddressId: number | null;
  taxMode: string;
  discountAmount: number;
  couponCode: string | null;
  couponMessage: string | null;
  couponApplied: boolean;
  codEnabled: boolean;
}

export interface PaymentInit {
  gateway: string;
  publicKey: string | null;
  gatewayOrderId: string;
  paymentId: number;
  amount: number;
  currency: string;
}

export interface PlaceOrderResult {
  orderId: number;
  orderNumber: string;
  amount: number;
  currency: string;
  payment: PaymentInit | null;
  codOrder: boolean;
}

export interface OrderItem {
  orderItemId: number;
  productId: number;
  productName: string;
  sku: string | null;
  slug: string | null;
  variantLabel: string | null;
  hsnCode: string | null;
  quantity: number;
  unitPrice: number;
  taxRate: number;
  taxAmount: number;
  lineTotal: number;
}

export interface OrderAddress {
  recipientName: string | null;
  phone: string | null;
  line1: string;
  line2: string | null;
  city: string;
  state: string;
  pincode: string;
  country: string;
}

export interface Order {
  orderId: number;
  orderNumber: string;
  status: string;
  currency: string;
  subtotal: number;
  discountAmount: number;
  taxAmount: number;
  shippingAmount: number;
  totalAmount: number;
  placedAt: string | null;
  createdAt: string;
  items: OrderItem[];
  shippingAddress: OrderAddress | null;
  billingAddress: OrderAddress | null;
  paymentMethod: string | null;
  paymentStatus: string | null;
  invoiceId: number | null;
  invoiceNumber: string | null;
  canCancel: boolean;
  shipment: Shipment | null;
}

export interface Shipment {
  shipmentId: number;
  courier: string | null;
  trackingNumber: string | null;
  status: string;
  estimatedDeliveryDate: string | null;
  shippedAt: string | null;
  deliveredAt: string | null;
}

export interface OrderListItem {
  orderId: number;
  orderNumber: string;
  status: string;
  totalAmount: number;
  itemCount: number;
  firstItemName: string | null;
  firstItemImage: string | null;
  placedAt: string | null;
  createdAt: string;
}
