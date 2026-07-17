export interface TopSearch { term: string; count: number; }

export interface AnalyticsSummary {
  ordersToday: number;
  ordersThisWeek: number;
  revenueToday: number;
  revenueThisWeek: number;
  aovThisWeek: number;
  newSignupsToday: number;
  newSignupsThisWeek: number;
  lowStockCount: number;
  pendingActionCount: number;
  topSearches: TopSearch[];
}

export interface ProductReportRow {
  productId: number;
  name: string;
  units: number;
  revenue: number;
  cost: number;
  profit: number;
  marginPct: number;
  costMissing: boolean;
}

export interface ReturnRateRow {
  productId: number;
  name: string;
  sold: number;
  returned: number;
  returnRatePct: number;
}

export interface GroupProfitRow {
  name: string;
  revenue: number;
  cost: number;
  profit: number;
  marginPct: number;
}

export interface SalesPoint { date: string; sales: number; orders: number; }
export interface SalesBreakdown { gross: number; discounts: number; returns: number; net: number; shipping: number; tax: number; total: number; }
export interface NewReturning { newCustomers: number; returningCustomers: number; newRevenue: number; returningRevenue: number; }
export interface SalesKpis { grossSales: number; netSales: number; orders: number; aov: number; returningRatePct: number; }
export interface SalesDashboard {
  kpis: SalesKpis;
  series: SalesPoint[];
  breakdown: SalesBreakdown;
  newVsReturning: NewReturning;
  topProducts: ProductReportRow[];
  byCategory: GroupProfitRow[];
}

export interface FunnelStage { stage: string; count: number; pctOfTop: number; stepPct: number; }
export interface Funnel { stages: FunnelStage[]; }
export interface AbandonedCartRow { cartId: number; customer: string; items: number; value: number; lastActivity: string; }
