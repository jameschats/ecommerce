import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Conversation, ConversationService, ConversationThread } from '../../core/services/conversation.service';

@Component({
  selector: 'app-account-conversations',
  imports: [DatePipe, FormsModule],
  template: `
    <h1 class="text-lg font-semibold text-slate-900 mb-1">Messages</h1>
    <p class="text-sm text-slate-500 mb-5">Your conversations with the store.</p>

    @if (loading()) {
      <p class="text-slate-400 text-sm">Loading…</p>
    } @else if (selected(); as t) {
      <button type="button" (click)="selected.set(null)" class="text-sm text-primary hover:underline mb-3">← All messages</button>
      <div class="bg-white border border-slate-200 rounded-2xl p-5">
        <h2 class="font-semibold text-slate-800">{{ t.conversation.subject }}</h2>
        <div class="text-xs text-slate-500 mt-0.5">{{ t.conversation.reference }} · {{ t.conversation.status }}</div>

        <div class="space-y-3 py-4">
          @for (m of t.messages; track m.id) {
            <div class="flex" [class]="m.authorType === 'Shopper' ? 'justify-end' : 'justify-start'">
              <div class="max-w-[80%] rounded-2xl px-3.5 py-2"
                   [class]="m.authorType === 'Shopper' ? 'bg-blue-50 text-slate-800' : 'bg-slate-100 text-slate-800'">
                <div class="text-sm whitespace-pre-line">{{ m.body }}</div>
                <div class="text-[10px] text-slate-400 mt-1">
                  {{ m.authorType === 'Shopper' ? 'You' : 'Store' }} · {{ m.createdAt | date:'dd MMM, HH:mm' }}
                </div>
              </div>
            </div>
          }
        </div>

        <div class="pt-3 border-t border-slate-100">
          <textarea [(ngModel)]="draft" name="draft" rows="3" class="input" placeholder="Write a reply…"></textarea>
          <button type="button" (click)="send(t)" [disabled]="sending() || !draft.trim()" class="btn-primary mt-2 disabled:opacity-60">
            {{ sending() ? 'Sending…' : 'Send' }}
          </button>
        </div>
      </div>
    } @else if (items().length === 0) {
      <div class="bg-white border border-slate-200 rounded-2xl p-8 text-center">
        <div class="text-3xl">💬</div>
        <p class="text-slate-600 font-medium mt-2">No messages yet</p>
        <p class="text-slate-400 text-sm mt-1">If you contact the store, the conversation appears here.</p>
      </div>
    } @else {
      <div class="space-y-2">
        @for (c of items(); track c.id) {
          <button type="button" (click)="open(c)"
                  class="w-full text-left bg-white border border-slate-200 rounded-xl p-4 hover:border-slate-300 transition">
            <div class="flex items-center justify-between gap-2">
              <span class="text-sm font-medium text-slate-800 truncate">{{ c.subject }}</span>
              <span class="text-[10px] px-1.5 py-0.5 rounded-full shrink-0"
                    [class]="c.status === 'Closed' ? 'bg-slate-100 text-slate-500' : 'bg-amber-100 text-amber-700'">{{ c.status }}</span>
            </div>
            <div class="text-xs text-slate-400 mt-0.5">{{ (c.lastMessageAt || c.createdAt) | date:'dd MMM yy, HH:mm' }}</div>
          </button>
        }
      </div>
    }
  `,
})
export class AccountConversationsComponent implements OnInit {
  private readonly api = inject(ConversationService);

  readonly loading = signal(true);
  readonly sending = signal(false);
  readonly items = signal<Conversation[]>([]);
  readonly selected = signal<ConversationThread | null>(null);
  draft = '';

  ngOnInit(): void { this.load(); }

  open(c: Conversation): void {
    this.api.get(c.id).subscribe((t) => this.selected.set(t));
  }

  send(t: ConversationThread): void {
    const body = this.draft.trim();
    if (!body) return;
    this.sending.set(true);
    this.api.reply(t.conversation.id, body).subscribe({
      next: () => { this.draft = ''; this.sending.set(false); this.open(t.conversation); },
      error: () => this.sending.set(false),
    });
  }

  private load(): void {
    this.loading.set(true);
    this.api.mine().subscribe({
      next: (r) => { this.items.set(r); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
}
