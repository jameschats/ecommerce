export interface PlanOption {
  planId: number;
  name: string;
  slug: string;
  monthlyPrice: number;
  maxProducts: number | null;
  maxOrders: number | null;
  aiCredits: number;
}

export interface SignupRequest {
  storeName: string;
  slug: string;
  ownerName: string;
  ownerEmail: string;
  password: string;
  planSlug?: string | null;
}

export interface OnboardingResult {
  tenantId: number;
  slug: string;
  storeUrl: string;
  accessToken: string;
  trialEndsAt: string;
}
