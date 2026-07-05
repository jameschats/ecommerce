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
}

export interface Contact {
  userId: number;
  email: string | null;
  fullName: string | null;
  phoneNumber: string | null;
  roles: string;
  lastLoginAt: string | null;
}

export interface TenantDetail {
  summary: TenantSummary;
  contacts: Contact[];
  standingReason: string | null;
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

export interface ImpersonationResult { accessToken: string; storeUrl: string; mode: string; expiresAt: string; }
export interface BlocklistEntry { signupBlocklistId: number; type: string; value: string; reason: string | null; createdAt: string; }
export interface AuditEntry { platformAccessLogId: number; adminUserId: number; tenantId: number | null; action: string; detail: string | null; createdAt: string; }
