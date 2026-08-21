import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { API_BASE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';

interface HelpdeskSettings {
  chatbotEnabled: boolean;
  activeHoursStart: string | null;
  activeHoursEnd: string | null;
}

interface UnansweredQuestion {
  id: number;
  conversationId: number | null;
  question: string;
  createdAt: string;
}

/** Merchant controls for the AI chatbot (v4 Phase 2): on/off, active hours, and the questions it
 * couldn't answer — the content-gap signal for what's worth adding to the FAQ page next to it. */
@Component({
  selector: 'app-admin-helpdesk',
  imports: [FormsModule, DatePipe, RouterLink],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900">AI Assistant</h1>
      <p class="text-sm text-slate-500 mb-6">Controls for the chatbot that answers customers on your storefront livechat widget.</p>

      @if (loading()) {
        <p class="text-slate-400 text-sm">Loading…</p>
      } @else if (settings(); as s) {
        <div class="bg-white border border-slate-200 rounded-xl p-5 mb-6">
          <h2 class="font-semibold text-slate-800 mb-3">Settings</h2>

          <label class="flex items-center gap-2 text-sm text-slate-700 mb-4">
            <input type="checkbox" [(ngModel)]="s.chatbotEnabled" name="enabled" />
            Let the AI assistant answer customers before a team member sees the conversation
          </label>

          <div class="grid sm:grid-cols-2 gap-3 mb-1">
            <div>
              <label class="lbl">Active hours start (optional)</label>
              <input [(ngModel)]="s.activeHoursStart" name="start" type="time" class="input" />
            </div>
            <div>
              <label class="lbl">Active hours end (optional)</label>
              <input [(ngModel)]="s.activeHoursEnd" name="end" type="time" class="input" />
            </div>
          </div>
          <p class="text-xs text-slate-400 mb-4">
            Leave both empty for no restriction. Outside these hours the assistant still tries to help and still
            hands off on the same triggers — the only difference is the message customers see once it does,
            letting them know a reply may wait until support hours resume.
          </p>

          @if (error()) { <p class="text-sm text-red-600 mb-3">{{ error() }}</p> }
          @if (saved()) { <p class="text-sm text-green-600 mb-3">Settings saved.</p> }
          <button type="button" (click)="save(s)" [disabled]="busy()" class="btn-primary disabled:opacity-60">
            {{ busy() ? 'Saving…' : 'Save' }}
          </button>
        </div>
      }

      <div class="bg-white border border-slate-200 rounded-xl p-5">
        <h2 class="font-semibold text-slate-800 mb-1">What the assistant couldn't answer</h2>
        <p class="text-sm text-slate-500 mb-4">
          Every time it hands off because it has no grounded answer — a good source of new FAQ content.
        </p>

        @if (loadingUnanswered()) {
          <p class="text-slate-400 text-sm">Loading…</p>
        } @else if (unanswered().length === 0) {
          <p class="text-slate-400 text-sm">Nothing yet — the assistant hasn't hit a gap it couldn't cover.</p>
        } @else {
          <div class="divide-y divide-slate-100">
            @for (q of unanswered(); track q.id) {
              <div class="py-3 flex items-start justify-between gap-3">
                <div class="min-w-0">
                  <div class="text-sm text-slate-800">{{ q.question }}</div>
                  <div class="text-xs text-slate-400 mt-0.5">{{ q.createdAt | date:'dd MMM yy, HH:mm' }}</div>
                </div>
                @if (q.conversationId) {
                  <a [routerLink]="['/admin/inbox', q.conversationId]" class="text-sm text-primary hover:underline shrink-0">View</a>
                }
              </div>
            }
          </div>
        }
      </div>
    </div>
  `,
})
export class AdminHelpdeskComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/helpdesk`;

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly saved = signal(false);
  readonly settings = signal<HelpdeskSettings | null>(null);

  readonly loadingUnanswered = signal(true);
  readonly unanswered = signal<UnansweredQuestion[]>([]);

  ngOnInit(): void {
    this.loadSettings();
    this.loadUnanswered();
  }

  save(s: HelpdeskSettings): void {
    if ((s.activeHoursStart && !s.activeHoursEnd) || (!s.activeHoursStart && s.activeHoursEnd)) {
      this.error.set('Set both active hours, or leave both empty.');
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.saved.set(false);
    this.http.put<ApiResponse<HelpdeskSettings>>(`${this.base}/settings`, s).subscribe({
      next: (r) => { this.settings.set(r.data ?? s); this.busy.set(false); this.saved.set(true); setTimeout(() => this.saved.set(false), 4000); },
      error: (e) => { this.error.set(e?.error?.message ?? 'Could not save that.'); this.busy.set(false); },
    });
  }

  private loadSettings(): void {
    this.loading.set(true);
    this.http.get<ApiResponse<HelpdeskSettings>>(`${this.base}/settings`).subscribe({
      next: (r) => { this.settings.set(r.data ?? null); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  private loadUnanswered(): void {
    this.loadingUnanswered.set(true);
    this.http.get<ApiResponse<UnansweredQuestion[]>>(`${this.base}/unanswered`).subscribe({
      next: (r) => { this.unanswered.set(r.data ?? []); this.loadingUnanswered.set(false); },
      error: () => this.loadingUnanswered.set(false),
    });
  }
}
