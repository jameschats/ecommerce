export interface Ticket {
  id: number;
  tenantId: number;
  subject: string;
  status: string;
  openedByPlatform: boolean;
  createdAt: string;
  lastMessageAt: string | null;
  storeName: string | null;
  // Helpdesk fields
  reference: string | null;
  priority: string;
  category: string | null;
  assignedToUserId: number | null;
  assignedToName: string | null;
  escalationTier: string;
  tags: string | null;
  firstResponseAt: string | null;
  resolvedAt: string | null;
}

export interface TicketMessage {
  id: number;
  fromPlatform: boolean;
  isInternalNote: boolean;
  body: string;
  createdAt: string;
}

export interface TicketActivity {
  id: number;
  type: string;
  detail: string;
  createdAt: string;
}

export interface TicketThread {
  ticket: Ticket;
  messages: TicketMessage[];
  activity: TicketActivity[];
}

export interface Agent {
  userId: number;
  name: string;
}

export interface QueueFilter {
  status?: string;
  priority?: string;
  tier?: string;
  mine?: boolean;
  unassigned?: boolean;
}
