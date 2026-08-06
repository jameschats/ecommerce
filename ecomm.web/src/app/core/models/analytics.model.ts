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
  /** True ⇒ cost/profit/marginPct are unknown, not zero. Show a dash, never the number. */
  costMissing: boolean;
}

// ---------------- Traffic (first-party) ----------------

export interface TrafficSummary {
  sessions: number;
  uniqueVisitors: number;
  sessionsChangePct: number;
  visitorsChangePct: number;
}

export interface TrafficPoint { date: string; label: string; sessions: number; }
export interface DeviceBreakdown { device: string; sessions: number; pct: number; }
export interface SourceBreakdown { source: string; sessions: number; pct: number; }
export interface TopPage { path: string; views: number; }
export interface GeoBreakdown { country: string; city: string; sessions: number; }
export interface NewVsReturning { new: number; returning: number; }
