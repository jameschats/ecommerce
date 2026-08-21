import { isPlatformBrowser } from '@angular/common';
import { Component, OnDestroy, PLATFORM_ID, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { ChatbotReply, ConversationMessage, ConversationService } from '../../core/services/conversation.service';
import { NotificationService } from '../../core/services/notification.service';

/**
 * Site-wide AI livechat bubble (v4 Phase 2) — bot-first, reusing the existing shopper-merchant
 * conversation model and the same NotificationHub/ConversationRealtime SignalR transport the
 * merchant inbox and account/conversations page already push over. Signed-in shoppers only — the
 * bot's order-lookup grounding needs a real customer identity, same v1 limit as the backend.
 *
 * The widget renders its own Shopper/Bot messages optimistically from each HTTP response (the
 * live feed can't be joined until the very first response tells us the conversation id, so it
 * would miss the opening exchange) — the SignalR push is only used for genuinely new inbound
 * messages a human agent sends after escalation, never re-rendering what this widget already sent.
 *
 * Conversation state lives only in this component's signals — resets on a full page reload. The
 * conversation itself is safely saved server-side either way; this only affects whether the widget
 * auto-reopens it on revisit, a known v1 simplification rather than a persistence gap.
 */
@Component({
  selector: 'app-live-chat-widget',
  imports: [FormsModule],
  template: `
    @if (auth.isAuthenticated()) {
      @if (open()) {
        <div class="fixed bottom-4 right-4 z-50 w-[22rem] max-w-[calc(100vw-2rem)] h-[28rem] max-h-[calc(100vh-2rem)] sf-card flex flex-col overflow-hidden">
          <div class="flex items-center justify-between px-4 py-3 bg-primary text-white shrink-0">
            <span class="font-semibold text-sm">Chat with us</span>
            <button type="button" (click)="open.set(false)" aria-label="Close chat" class="text-white/80 hover:text-white text-lg leading-none">×</button>
          </div>

          <div class="flex-1 overflow-y-auto px-3 py-3 space-y-2.5 bg-slate-50">
            @if (messages().length === 0 && !sending()) {
              <p class="text-sm text-slate-500 text-center mt-6">Ask a question about your order, our products, or anything else — we're here to help.</p>
            }
            @for (m of messages(); track m.id) {
              <div class="flex" [class]="m.authorType === 'Shopper' ? 'justify-end' : 'justify-start'">
                <div class="max-w-[85%] rounded-2xl px-3 py-2"
                     [class]="m.authorType === 'Shopper' ? 'bg-primary text-white' : 'bg-white border border-slate-200 text-slate-800'">
                  <div class="text-sm whitespace-pre-line">{{ m.body }}</div>
                  @if (m.authorType !== 'Shopper') {
                    <div class="text-[10px] text-slate-400 mt-1">{{ m.authorType === 'Bot' ? 'AI Assistant' : 'Store' }}</div>
                  }
                </div>
              </div>
            }
            @if (sending()) {
              <div class="flex justify-start">
                <div class="bg-white border border-slate-200 rounded-2xl px-3 py-2 text-sm text-slate-400">Typing…</div>
              </div>
            }
            @if (escalated()) {
              <p class="text-xs text-center text-slate-400 py-1">A team member will reply here as soon as they can.</p>
            }
          </div>

          @if (errorMsg(); as err) {
            <div class="px-3 py-1.5 bg-red-50 border-t border-red-200 text-red-700 text-xs shrink-0">{{ err }}</div>
          }

          <div class="p-2.5 border-t border-slate-200 bg-white shrink-0 flex gap-2">
            <input [(ngModel)]="draft" name="draft" (keyup.enter)="send()" [disabled]="sending()"
                   class="input flex-1" placeholder="Type a message…" />
            <button type="button" (click)="send()" [disabled]="sending() || !draft.trim()" class="btn-primary shrink-0 disabled:opacity-60">
              Send
            </button>
          </div>
        </div>
      } @else {
        <button type="button" (click)="open.set(true)" aria-label="Open chat"
                class="fixed bottom-4 right-4 z-50 w-14 h-14 rounded-full bg-primary text-white shadow-lg flex items-center justify-center hover:bg-primary-dark">
          <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/>
          </svg>
        </button>
      }
    }
  `,
})
export class LiveChatWidgetComponent implements OnDestroy {
  readonly auth = inject(AuthService);
  private readonly api = inject(ConversationService);
  private readonly notify = inject(NotificationService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly open = signal(false);
  readonly sending = signal(false);
  readonly escalated = signal(false);
  readonly messages = signal<ConversationMessage[]>([]);
  readonly errorMsg = signal<string | null>(null);
  draft = '';

  private conversationId: number | null = null;
  private nextLocalId = -1;   // negative ids for optimistically-rendered messages, never collide with real server ids

  constructor() {
    effect(() => {
      const m = this.notify.liveMessage();
      if (!m || m.conversationId !== this.conversationId) return;
      if (m.authorType !== 'Merchant') return;   // Shopper/Bot turns are already rendered by this widget's own send/reply flow
      this.messages.update((list) => [...list, { id: m.messageId, authorType: m.authorType, body: m.body, createdAt: m.createdAt }]);
    });
  }

  send(): void {
    const body = this.draft.trim();
    if (!body || this.sending()) return;
    this.errorMsg.set(null);
    this.draft = '';
    this.messages.update((list) => [...list, { id: this.nextLocalId--, authorType: 'Shopper', body, createdAt: new Date().toISOString() }]);
    this.sending.set(true);

    if (this.conversationId === null) {
      this.api.startChat(body).subscribe({
        next: (r) => {
          this.conversationId = r.conversationId;
          if (this.isBrowser) this.notify.joinConversation(r.conversationId);
          this.applyReply(r.reply);
        },
        error: () => this.onSendError(),
      });
    } else {
      this.api.chat(this.conversationId, body).subscribe({
        next: (r) => this.applyReply(r),
        error: () => this.onSendError(),
      });
    }
  }

  private applyReply(reply: ChatbotReply): void {
    this.sending.set(false);
    if (reply.reply) {
      this.messages.update((list) => [...list, { id: this.nextLocalId--, authorType: 'Bot', body: reply.reply, createdAt: new Date().toISOString() }]);
    }
    if (reply.escalated) this.escalated.set(true);
  }

  private onSendError(): void {
    this.sending.set(false);
    this.errorMsg.set('Message not sent — please try again.');
  }

  ngOnDestroy(): void {
    if (this.conversationId !== null && this.isBrowser) this.notify.leaveConversation(this.conversationId);
  }
}
