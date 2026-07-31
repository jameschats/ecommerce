export interface AdminCoupon {
  couponId: number;
  code: string;
  method: string;       // Code | Automatic
  description: string | null;
  discountType: string; // Flat | Percentage
  discountValue: number;
  freeShipping: boolean;
  appliesTo: string;    // Order | Products | Collections
  targetIds: number[];
  maxDiscountAmount: number | null;
  minOrderAmount: number | null;
  usageLimit: number | null;
  perUserLimit: number | null;
  usedCount: number;
  startsAt: string | null;
  endsAt: string | null;
  isActive: boolean;
  giftProductId: number | null;
  giftProductName: string | null;
}

export interface SaveCouponRequest {
  code: string;
  method: string;
  description: string | null;
  discountType: string;
  discountValue: number;
  freeShipping: boolean;
  appliesTo: string;
  targetIds: number[];
  maxDiscountAmount: number | null;
  minOrderAmount: number | null;
  usageLimit: number | null;
  perUserLimit: number | null;
  startsAt: string | null;
  endsAt: string | null;
  isActive: boolean;
  giftProductId: number | null;
}
