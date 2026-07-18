export interface TenantSummary {
  tenantId: number;
  name: string;
  slug: string | null;
  standing: string;
  isActive: boolean;
  suspended: boolean;
  planName: string | null;
  subStatus: string | null;
  trialEndsAt: string | null;
  createdAt: string;
  userCount: number;
  orderCount: number;
  healthScore: number;
  healthBand: string;
}

export interface TenantHealth { score: number; band: string; signals: string[]; suggestedStanding: string | null; }

export interface Contact {
  userId: number;
  email: string | null;
  fullName: string | null;
  phoneNumber: string | null;
  roles: string;
  lastLoginAt: string | null;
}

export interface TenantSubscriptionInfo {
  planId: number | null;
  planName: string | null;
  status: string | null;
  trialEndsAt: string | null;
  currentPeriodEnd: string | null;
  razorpaySubscriptionId: string | null;
}
export interface TenantUsage {
  products: number;
  orders: number;
  gmv: number;
  aiCreditBalance: number;
}

export interface PlanOption {
  planId: number;
  name: string;
  slug: string;
  monthlyPrice: number;
  maxProducts: number | null;
  maxOrders: number | null;
  aiCredits: number;
  features: string | null;
  isActive: boolean;
  displayOrder: number;
}
export interface PlanUpsert {
  name: string; slug: string | null; monthlyPrice: number;
  maxProducts: number | null; maxOrders: number | null; aiCredits: number;
  features: string | null; isActive: boolean; displayOrder: number;
}
export interface CreditPack { aiCreditPackId: number; name: string; credits: number; priceInr: number; isActive: boolean; displayOrder: number; }
export interface PackUpsert { name: string; credits: number; priceInr: number; isActive: boolean; displayOrder: number; }
export interface TenantNote { tenantNoteId: number; adminUserId: number; note: string; createdAt: string; }

export interface TenantDetail {
  summary: TenantSummary;
  contacts: Contact[];
  standingReason: string | null;
  subscription: TenantSubscriptionInfo;
  usage: TenantUsage;
  customDomain: string | null;
  customDomainVerified: boolean;
  recentActivity: AuditEntry[];
  tags: string[];
  notes: TenantNote[];
  offboardedAt: string | null;
  billing: BillingCharge[];
  health: TenantHealth;
}

export interface BillingCharge {
  id: number;
  tenantId: number;
  amount: number;
  status: string;
  billedAt: string;
  periodStart: string | null;
  periodEnd: string | null;
  razorpayPaymentId: string | null;
}
export interface SubStatusRow {
  tenantId: number;
  name: string;
  slug: string | null;
  planName: string | null;
  status: string;
  currentPeriodEnd: string | null;
  graceEndsAt: string | null;
}

export interface PlanRevenueRow { plan: string; activeCount: number; mrr: number; }
export interface PlatformRevenue {
  mrr: number;
  totalTenants: number;
  active: number;
  trial: number;
  pastDue: number;
  suspended: number;
  cancelled: number;
  byPlan: PlanRevenueRow[];
}

export interface FailedNotification { id: number; channel: string; recipient: string; subject: string | null; error: string | null; createdAt: string; }
export interface TenantDiagnostics {
  failedNotifications: number;
  ordersNeedingAction: number;
  lowStock: number;
  recentFailures: FailedNotification[];
}

export interface StoreLeader { tenantId: number; name: string; slug: string | null; gmv: number; orders: number; }
export interface PlatformGmvPoint { date: string; gmv: number; }
export interface PlatformAnalytics {
  gmv: number;
  orders: number;
  aov: number;
  activeStores: number;
  newStores: number;
  collectedRevenue: number;
  series: PlatformGmvPoint[];
  topStores: StoreLeader[];
}

export interface PlatformStaff { userId: number; email: string | null; fullName: string | null; isActive: boolean; lastLoginAt: string | null; createdAt: string; }

export interface Announcement { id: number; title: string; body: string; level: string; isActive: boolean; startsAt: string | null; endsAt: string | null; createdAt: string; }
export interface AnnouncementUpsert { title: string; body: string; level: string; startsAt: string | null; endsAt: string | null; }

export interface ImpersonationResult { accessToken: string; storeUrl: string; mode: string; expiresAt: string; }
export interface BlocklistEntry { signupBlocklistId: number; type: string; value: string; reason: string | null; createdAt: string; }
export interface AuditEntry { platformAccessLogId: number; adminUserId: number; tenantId: number | null; action: string; detail: string | null; createdAt: string; }
