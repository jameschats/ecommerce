import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AiAssistService } from '../../../core/services/ai-assist.service';
import { Conversation, ConversationService, ConversationThread } from '../../../core/services/conversation.service';
import { PagedResult } from '../../../core/models/api-response.model';

@Component({
  selector: 'app-admin-inbox',
  imports: [DatePipe, FormsModule, RouterLink],
  template: `
    <div class="max-w-6xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900">Inbox</h1>
      <p class="text-sm text-slate-500 mb-6">Conversations with your customers.</p>

      <div class="flex gap-2 mb-4 text-sm">
        @for (f of filters; track f.label) {
          <button type="button" (click)="setFilter(f.value)"
                  class="px-3 py-1.5 rounded-lg border transition"
                  [class]="filter() === f.value ? 'border-primary text-primary bg-blue-50' : 'border-slate-200 text-slate-600 hover:bg-slate-50'">
            {{ f.label }}
          </button>
        }
      </div>

      @if (loading()) {
        <p class="text-slate-400 text-sm">Loading…</p>
      } @else if ((result()?.items ?? []).length === 0) {
        <div class="bg-white border border-slate-200 rounded-xl p-8 text-center">
          <div class="text-3xl">💬</div>
          <p class="text-slate-600 font-medium mt-2">No conversations here</p>
          <p class="text-slate-400 text-sm mt-1">Messages customers send you will appear here.</p>
        </div>
      } @else {
        <div class="grid lg:grid-cols-5 gap-6">
          <!-- List -->
          <div class="lg:col-span-2 space-y-2">
            @for (c of result()?.items ?? []; track c.id) {
              <button type="button" (click)="open(c)"
                      class="w-full text-left bg-white border rounded-xl p-3 transition hover:border-slate-300"
                      [class]="selected()?.conversation?.id === c.id ? 'border-primary ring-1 ring-primary/20' : 'border-slate-200'">
                <div class="flex items-center justify-between gap-2">
                  <span class="text-sm font-medium text-slate-800 truncate">{{ c.subject }}</span>
                  <span class="text-[10px] px-1.5 py-0.5 rounded-full shrink-0" [class]="badge(c.status)">{{ c.status }}</span>
                </div>
                <div class="text-xs text-slate-500 truncate mt-0.5">{{ c.shopperEmail }}</div>
                <div class="text-xs text-slate-400 mt-0.5">
                  {{ c.reference }} · {{ (c.lastMessageAt || c.createdAt) | date:'dd MMM, HH:mm' }}
                </div>
              </button>
            }
          </div>

          <!-- Thread -->
          <div class="lg:col-span-3">
            @if (selected(); as t) {
              <div class="bg-white border border-slate-200 rounded-xl p-5">
                <div class="flex items-start justify-between gap-3 pb-3 border-b border-slate-100">
                  <div class="min-w-0">
                    <h2 class="font-semibold text-slate-800">{{ t.conversation.subject }}</h2>
                    <div class="text-xs text-slate-500 mt-0.5">
                      {{ t.conversation.shopperEmail }} · {{ t.conversation.reference }}
                      @if (t.conversation.orderId) {
                        · <a [routerLink]="['/admin/orders', t.conversation.orderId]" class="text-primary hover:underline">view order</a>
                      }
                    </div>
                  </div>
                  <select [ngModel]="t.conversation.status" (ngModelChange)="changeStatus(t, $event)"
                          class="input text-sm py-1 w-auto shrink-0">
                    @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
                  </select>
                </div>

                <div class="space-y-3 py-4 max-h-[45vh] overflow-auto">
                  @for (m of t.messages; track m.id) {
                    <div class="flex" [class]="m.authorType === 'Shopper' ? 'justify-start' : 'justify-end'">
                      <div class="max-w-[80%] rounded-2xl px-3.5 py-2"
                           [class]="m.authorType === 'Shopper' ? 'bg-slate-100 text-slate-800' : 'bg-blue-50 text-slate-800'">
                        <div class="text-sm whitespace-pre-line">{{ m.body }}</div>
                        <div class="text-[10px] text-slate-400 mt-1">
                          {{ m.authorType === 'Shopper' ? 'Customer' : 'You' }} · {{ m.createdAt | date:'dd MMM, HH:mm' }}
                        </div>
                      </div>
                    </div>
                  }
                </div>

                <div class="pt-3 border-t border-slate-100">
                  <textarea [(ngModel)]="draft" name="draft" rows="3" class="input" placeholder="Write a reply…"></textarea>

                  @if (grounding().length) {
                    <p class="text-xs text-slate-500 mt-1.5">
                      ✨ Suggested from {{ grounding().join(' · ') }} — <span class="text-slate-400">check it before sending.</span>
                    </p>
                  }
                  @if (error()) { <p class="text-sm text-red-600 mt-1">{{ error() }}</p> }

                  <div class="flex items-center gap-2 mt-2">
                    <button type="button" (click)="send(t)" [disabled]="sending() || !draft.trim()"
                            class="btn-primary disabled:opacity-60">
                      {{ sending() ? 'Sending…' : 'Send reply' }}
                    </button>
                    @if (ai.enabled()) {
                      <button type="button" (click)="suggest(t)" [disabled]="drafting()"
                              class="btn-ghost border border-violet-200 text-violet-700 disabled:opacity-60">
                        {{ drafting() ? 'Drafting…' : '✨ Suggest a reply' }}
                      </button>
                    }
                  </div>
                </div>
              </div>
            } @else {
              <div class="bg-white border border-slate-200 rounded-xl p-8 text-center text-slate-400 text-sm">
                Pick a conversation to read it.
              </div>
            }
          </div>
        </div>
      }
    </div>
  `,
})
export class AdminInboxComponent implements OnInit {
  private readonly api = inject(ConversationService);
  readonly ai = inject(AiAssistService);

  readonly loading = signal(true);
  readonly sending = signal(false);
  readonly drafting = signal(false);
  readonly grounding = signal<string[]>([]);
  readonly error = signal<string | null>(null);
  readonly result = signal<PagedResult<Conversation> | null>(null);
  readonly selected = signal<ConversationThread | null>(null);
  readonly filter = signal<string | undefined>('Open');
  draft = '';

  readonly statuses = ['Open', 'Pending', 'Closed'];
  readonly filters = [
    { label: 'Open', value: 'Open' },
    { label: 'Pending', value: 'Pending' },
    { label: 'Closed', value: 'Closed' },
    { label: 'All', value: undefined as string | undefined },
  ];

  ngOnInit(): void {
    this.ai.ensureStatus();
    this.load();
  }

  setFilter(value: string | undefined): void {
    this.filter.set(value);
    this.selected.set(null);
    this.load();
  }

  open(c: Conversation): void {
    this.error.set(null);
    this.grounding.set([]);
    this.api.adminThread(c.id).subscribe((t) => this.selected.set(t));
  }

  /**
   * Puts a suggestion in the reply box. Deliberately never sends: the merchant edits and ships it,
   * so a wrong draft costs a few seconds rather than their credibility with a customer.
   */
  suggest(t: ConversationThread): void {
    this.drafting.set(true);
    this.error.set(null);
    this.api.draft(t.conversation.id).subscribe({
      next: (d) => {
        this.draft = d.draft;
        this.grounding.set(d.groundedOn ?? []);
        this.drafting.set(false);
      },
      error: (e) => {
        this.error.set(e?.status === 402
          ? "You're out of AI credits — top up under AI credits to keep using suggestions."
          : e?.error?.message ?? 'Could not draft a reply just now.');
        this.drafting.set(false);
      },
    });
  }

  send(t: ConversationThread): void {
    const body = this.draft.trim();
    if (!body) return;
    this.sending.set(true);
    this.error.set(null);
    this.api.adminReply(t.conversation.id, body).subscribe({
      next: () => { this.draft = ''; this.sending.set(false); this.open(t.conversation); this.load(); },
      error: (e) => { this.error.set(e?.error?.message ?? 'Could not send that reply.'); this.sending.set(false); },
    });
  }

  changeStatus(t: ConversationThread, status: string): void {
    this.api.setStatus(t.conversation.id, status).subscribe(() => { this.open(t.conversation); this.load(); });
  }

  badge(status: string): string {
    return status === 'Open' ? 'bg-amber-100 text-amber-700'
      : status === 'Pending' ? 'bg-blue-100 text-blue-700'
      : 'bg-slate-100 text-slate-500';
  }

  private load(): void {
    this.loading.set(true);
    this.api.inbox(this.filter()).subscribe({
      next: (r) => { this.result.set(r); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
}
