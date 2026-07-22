import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ConversationService, ConversationThread } from '../../../core/services/conversation.service';

/**
 * A conversation opened from the emailed reply link — no sign-in.
 * The signed token in the URL names exactly one conversation, so this page can only
 * ever show that one thread.
 */
@Component({
  selector: 'app-thread',
  imports: [DatePipe, FormsModule],
  template: `
    <section class="page-container py-12">
      <div class="max-w-2xl mx-auto">
        @if (loading()) {
          <p class="text-slate-400 text-sm">Loading…</p>
        } @else if (thread(); as t) {
          <h1 class="text-2xl font-bold text-slate-900">{{ t.conversation.subject }}</h1>
          <p class="text-sm text-slate-500 mt-1 mb-6">{{ t.conversation.reference }} · {{ t.conversation.status }}</p>

          <div class="bg-white border border-slate-200 rounded-2xl p-5">
            <div class="space-y-3">
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

            <div class="pt-4 mt-4 border-t border-slate-100">
              @if (sent()) {
                <p class="text-sm text-green-700">Your reply has been sent.</p>
              }
              <textarea [(ngModel)]="draft" name="draft" rows="3" class="input" placeholder="Write a reply…"></textarea>
              @if (error()) { <p class="text-sm text-red-600 mt-1">{{ error() }}</p> }
              <button type="button" (click)="send()" [disabled]="sending() || !draft.trim()" class="btn-primary mt-2 disabled:opacity-60">
                {{ sending() ? 'Sending…' : 'Send reply' }}
              </button>
            </div>
          </div>
        } @else {
          <div class="bg-white border border-slate-200 rounded-2xl p-8 text-center">
            <div class="text-3xl">🔗</div>
            <h1 class="text-lg font-semibold text-slate-800 mt-2">This link isn't valid</h1>
            <p class="text-slate-500 text-sm mt-1">It may have expired. Please contact the store again.</p>
          </div>
        }
      </div>
    </section>
  `,
})
export class ThreadComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ConversationService);

  readonly loading = signal(true);
  readonly sending = signal(false);
  readonly sent = signal(false);
  readonly error = signal<string | null>(null);
  readonly thread = signal<ConversationThread | null>(null);
  draft = '';

  private token = '';

  ngOnInit(): void {
    this.token = this.route.snapshot.paramMap.get('token') ?? '';
    if (!this.token) { this.loading.set(false); return; }
    this.load();
  }

  send(): void {
    const body = this.draft.trim();
    if (!body) return;
    this.sending.set(true);
    this.error.set(null);
    this.api.replyByToken(this.token, body).subscribe({
      next: () => { this.draft = ''; this.sent.set(true); this.sending.set(false); this.load(); },
      error: (e) => {
        this.error.set(e?.status === 429
          ? 'Too many replies just now — please try again shortly.'
          : e?.error?.message ?? "That didn't send. Please try again.");
        this.sending.set(false);
      },
    });
  }

  private load(): void {
    this.api.byToken(this.token).subscribe({
      next: (t) => { this.thread.set(t); this.loading.set(false); },
      error: () => { this.thread.set(null); this.loading.set(false); },
    });
  }
}
