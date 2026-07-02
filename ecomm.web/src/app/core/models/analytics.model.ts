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
