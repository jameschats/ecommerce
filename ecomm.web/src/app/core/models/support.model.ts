export interface Ticket {
  id: number;
  tenantId: number;
  subject: string;
  status: string;
  openedByPlatform: boolean;
  createdAt: string;
  lastMessageAt: string | null;
  storeName: string | null;
}

export interface TicketMessage {
  id: number;
  fromPlatform: boolean;
  isInternalNote: boolean;
  body: string;
  createdAt: string;
}

export interface TicketThread {
  ticket: Ticket;
  messages: TicketMessage[];
}
