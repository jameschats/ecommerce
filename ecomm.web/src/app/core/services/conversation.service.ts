import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse, PagedResult } from '../models/api-response.model';

export interface Conversation {
  id: number;
  reference: string | null;
  subject: string;
  status: string;
  shopperEmail: string | null;
  orderId: number | null;
  productId: number | null;
  createdAt: string;
  lastMessageAt: string | null;
  firstResponseAt: string | null;
}

export interface ConversationMessage {
  id: number;
  authorType: 'Shopper' | 'Merchant' | 'Platform' | 'Bot';
  body: string;
  createdAt: string;
}

export interface ChatbotReply {
  reply: string;
  escalated: boolean;
  escalationReason: string | null;
}

export interface StartChatResponse {
  conversationId: number;
  reply: ChatbotReply;
}

export interface ConversationThread {
  conversation: Conversation;
  messages: ConversationMessage[];
  /** Present only when the thread was opened anonymously — the link back into it. */
  replyToken: string | null;
}

export interface SupportDraft {
  draft: string;
  /** What the suggestion was built from, so the merchant can judge it rather than trust it. */
  groundedOn: string[];
}

export interface StartConversationRequest {
  subject: string;
  message: string;
  email?: string | null;
  name?: string | null;
  orderId?: number | null;
  productId?: number | null;
}

@Injectable({ providedIn: 'root' })
export class ConversationService {
  private readonly http = inject(HttpClient);
  private readonly base = API_BASE_URL;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  // ----- Shopper -----
  start(body: StartConversationRequest): Observable<ConversationThread> {
    return this.unwrap(this.http.post<ApiResponse<ConversationThread>>(`${this.base}/conversations`, body));
  }
  mine(): Observable<Conversation[]> {
    return this.unwrap(this.http.get<ApiResponse<Conversation[]>>(`${this.base}/conversations`));
  }
  get(id: number): Observable<ConversationThread> {
    return this.unwrap(this.http.get<ApiResponse<ConversationThread>>(`${this.base}/conversations/${id}`));
  }
  reply(id: number, body: string): Observable<unknown> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/conversations/${id}/messages`, { body });
  }
  /** AI assistant (v4 Phase 2) — starts a brand-new livechat conversation with the first message. */
  startChat(body: string): Observable<StartChatResponse> {
    return this.unwrap(this.http.post<ApiResponse<StartChatResponse>>(`${this.base}/conversations/chat/start`, { body }));
  }
  /** AI assistant — sends a message on an existing conversation and gets the bot's reply (or an escalation signal). */
  chat(id: number, body: string): Observable<ChatbotReply> {
    return this.unwrap(this.http.post<ApiResponse<ChatbotReply>>(`${this.base}/conversations/${id}/chat`, { body }));
  }
  byToken(token: string): Observable<ConversationThread> {
    return this.unwrap(this.http.get<ApiResponse<ConversationThread>>(`${this.base}/conversations/thread/${token}`));
  }
  replyByToken(token: string, body: string): Observable<unknown> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/conversations/thread/${token}/messages`, { body });
  }

  // ----- Merchant inbox -----
  inbox(status?: string, page = 1, pageSize = 20): Observable<PagedResult<Conversation>> {
    const q = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (status) q.set('status', status);
    return this.unwrap(this.http.get<ApiResponse<PagedResult<Conversation>>>(`${this.base}/admin/inbox?${q}`));
  }
  adminThread(id: number): Observable<ConversationThread> {
    return this.unwrap(this.http.get<ApiResponse<ConversationThread>>(`${this.base}/admin/inbox/${id}`));
  }
  adminReply(id: number, body: string): Observable<unknown> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/admin/inbox/${id}/messages`, { body });
  }
  setStatus(id: number, status: string): Observable<unknown> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/admin/inbox/${id}/status`, { status });
  }
  /** Suggests a reply for the merchant to edit — never sends it. */
  draft(id: number): Observable<SupportDraft> {
    return this.unwrap(this.http.post<ApiResponse<SupportDraft>>(`${this.base}/admin/inbox/${id}/draft`, {}));
  }
}
