export interface PlanOption {
  planId: number;
  name: string;
  slug: string;
  monthlyPrice: number;
  maxProducts: number | null;
  maxOrders: number | null;
  maxStorageMb: number | null;
  aiCredits: number;
  marketingEngineLevel: string | null;
  liveChatLevel: string | null;
  helpdeskLevel: string | null;
  introPriceInr: number | null;
  introMonths: number | null;
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
