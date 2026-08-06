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

/** One period of trading. Revenue is the order total billed, including packing and rounding. */
export interface SalesPeriodRow {
  period: string;
  label: string;
  orders: number;
  units: number;
  revenue: number;
  cost: number;
  profit: number;
  marginPct: number;
  costMissing: boolean;
}

export interface GroupProfitRow {
  name: string;
  revenue: number;
  cost: number;
  profit: number;
  marginPct: number;
}
