export interface AppNotification {
  id: number;
  type: string;               // OrderUpdate | NewOrder | PendingReview | LowStock
  title: string;
  message: string | null;
  linkUrl: string | null;
  isRead: boolean;
  createdAt: string;
}
