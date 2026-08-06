import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { API_BASE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';

interface Template {
  notificationTemplateId: number;
  code: string;
  channel: string;
  subject: string | null;
  body: string | null;
  isActive: boolean;
  updatedAt: string | null;
}

/**
 * Editing the messages customers receive.
 *
 * These nine templates have been in the database since migration 024, described in code as
 * "admin-editable", with no screen anywhere — changing the wording meant raw SQL. Codes and
 * channels are fixed because they are wired to send sites in C#; a new one invented here
 * would be a template nothing ever sends.
 */
@Component({
  selector: 'app-admin-templates',
  imports: [FormsModule],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Message templates</h1>
      <p class="text-sm text-slate-500 mb-5">
        The emails and texts customers receive. Placeholders like
        <code class="text-xs bg-slate-100 px-1 rounded">{{ braced('CustomerName') }}</code>
        are filled in when the message is sent.
      </p>

      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      @if (loading()) {
        <div class="p-10 text-center text-slate-400">Loading…</div>
      } @else {
        <div class="space-y-3">
          @for (t of templates(); track t.notificationTemplateId) {
            <div class="bg-white border border-slate-200 rounded-xl">
              <button type="button" (click)="toggle(t.notificationTemplateId)"
                      class="w-full flex items-center gap-3 px-4 py-3 text-left">
                <span class="font-medium text-slate-900">{{ label(t.code) }}</span>
                <span class="text-[11px] uppercase tracking-wide bg-slate-100 text-slate-500 rounded px-1.5 py-0.5">{{ t.channel }}</span>
                @if (!t.isActive) {
                  <span class="text-[11px] bg-amber-50 text-amber-700 border border-amber-200 rounded px-1.5 py-0.5">off</span>
                }
                <span class="ml-auto text-slate-400 text-sm">{{ openId() === t.notificationTemplateId ? '▴' : '▾' }}</span>
              </button>

              @if (openId() === t.notificationTemplateId) {
                <div class="px-4 pb-4 border-t border-slate-100 pt-4 space-y-3">
                  @if (tokensFor(t.code).length) {
                    <div class="text-xs text-slate-500">
                      Available:
                      @for (tok of tokensFor(t.code); track tok) {
                        <button type="button" (click)="insert(t, tok)"
                                class="inline-block bg-slate-100 hover:bg-slate-200 rounded px-1.5 py-0.5 mr-1 mb-1 font-mono">
                          {{ braced(tok) }}
                        </button>
                      }
                    </div>
                  }

                  @if (t.channel === 'Email') {
                    <label class="block">
                      <span class="lbl">Subject</span>
                      <input [(ngModel)]="t.subject" class="input" />
                    </label>
                  }

                  <label class="block">
                    <span class="lbl">{{ t.channel === 'Email' ? 'Body (HTML allowed)' : 'Message' }}</span>
                    <textarea [(ngModel)]="t.body" [rows]="t.channel === 'Email' ? 12 : 4"
                              class="input font-mono text-xs"></textarea>
                  </label>

                  <label class="flex items-center gap-2 cursor-pointer">
                    <input type="checkbox" [(ngModel)]="t.isActive" class="w-4 h-4" />
                    <span class="text-sm text-slate-700">Send this message</span>
                  </label>

                  <div class="flex items-center gap-2">
                    <button type="button" (click)="save(t)" [disabled]="saving()" class="btn-primary">
                      {{ saving() ? 'Saving…' : 'Save' }}
                    </button>
                    <button type="button" (click)="preview(t)" class="btn-ghost border border-slate-300">Preview</button>
                  </div>

                  @if (previewFor() === t.notificationTemplateId && previewHtml(); as p) {
                    <div class="mt-2 border border-slate-200 rounded-lg overflow-hidden">
                      <div class="bg-slate-50 px-3 py-2 text-xs text-slate-500 border-b border-slate-200">
                        Preview with sample values — <strong>{{ p.subject }}</strong>
                      </div>
                      <!-- Rendered as text, not injected as HTML. A template is admin-authored,
                           but piping it through innerHTML would make the editor an XSS vector
                           the moment a second staff role can reach this screen. -->
                      <pre class="p-3 text-xs whitespace-pre-wrap text-slate-700">{{ p.body }}</pre>
                    </div>
                  }
                </div>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class AdminTemplatesComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/notification-templates`;

  readonly templates = signal<Template[]>([]);
  readonly tokens = signal<Record<string, string[]>>({});
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly openId = signal<number | null>(null);
  readonly previewFor = signal<number | null>(null);
  readonly previewHtml = signal<{ subject: string; body: string } | null>(null);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  private readonly labels: Record<string, string> = {
    OrderConfirmation: 'Order received',
    OrderStatusUpdate: 'Order status changed',
    OrderShipped: 'Order dispatched',
    OrderCancelled: 'Order cancelled',
    EmailVerification: 'Email verification code',
    PasswordReset: 'Password reset code',
  };

  ngOnInit(): void {
    this.http.get<ApiResponse<Record<string, string[]>>>(`${this.base}/tokens`)
      .subscribe({ next: (r) => this.tokens.set(r.data ?? {}), error: () => {} });

    this.http.get<ApiResponse<Template[]>>(this.base).subscribe({
      next: (r) => { this.templates.set(r.data ?? []); this.loading.set(false); },
      error: () => { this.error.set('Could not load templates.'); this.loading.set(false); },
    });
  }

  label(code: string): string { return this.labels[code] ?? code; }

  /**
   * Builds a token in braces for display. Written here rather than as HTML entities in the
   * template: entities are decoded before Angular parses interpolation, so `&#123;&#123;`
   * arrives as a live `{{` and the compiler tries to evaluate the token name as an
   * expression.
   */
  braced(token: string): string { return `{{${token}}}`; }
  tokensFor(code: string): string[] { return this.tokens()[code] ?? []; }

  toggle(id: number): void {
    this.openId.set(this.openId() === id ? null : id);
    this.previewHtml.set(null);
  }

  /** Appends the token rather than tracking the caret — simple, and never loses text. */
  insert(t: Template, token: string): void {
    t.body = `${t.body ?? ''}{{${token}}}`;
  }

  save(t: Template): void {
    this.saving.set(true);
    this.message.set(null);
    this.error.set(null);
    this.http.put<ApiResponse<Template>>(`${this.base}/${t.notificationTemplateId}`, {
      subject: t.subject, body: t.body, isActive: t.isActive,
    }).subscribe({
      next: () => { this.saving.set(false); this.message.set('Template saved.'); setTimeout(() => this.message.set(null), 2500); },
      error: (e) => { this.saving.set(false); this.error.set(e?.error?.message ?? 'Could not save.'); },
    });
  }

  preview(t: Template): void {
    this.http.post<ApiResponse<{ subject: string; body: string }>>(
      `${this.base}/${t.notificationTemplateId}/preview`,
      { subject: t.subject, body: t.body, isActive: t.isActive },
    ).subscribe({
      next: (r) => { this.previewFor.set(t.notificationTemplateId); this.previewHtml.set(r.data ?? null); },
      error: () => this.error.set('Could not render the preview.'),
    });
  }
}
