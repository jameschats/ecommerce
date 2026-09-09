import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { NotificationSender, NotificationTemplate, NotificationTemplatesService } from '../../../core/services/notification-templates.service';

@Component({
  selector: 'app-admin-notification-templates',
  imports: [FormsModule],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Notifications</h1>
      <p class="text-sm text-slate-500 mb-5">The emails and texts your customers get, and who they appear to come from.</p>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

      @if (loading()) { <p class="text-slate-400 text-sm">Loading…</p> }
      @else {
        <!-- Sender identity -->
        <div class="bg-white border border-slate-200 rounded-xl p-6 mb-6 space-y-3">
          <h2 class="font-semibold text-slate-800">Sender identity</h2>
          <p class="text-xs text-slate-400">Emails are sent from the platform address for deliverability; only the display name and reply-to change.</p>
          <div class="grid sm:grid-cols-2 gap-3">
            <label class="block"><span class="lbl">Sender name</span><input class="input" [(ngModel)]="sender.senderName" name="sn" placeholder="Your Store" /></label>
            <label class="block"><span class="lbl">Reply-to email</span><input class="input" [(ngModel)]="sender.replyToEmail" name="rt" placeholder="hello@yourstore.com" /></label>
          </div>
          <button type="button" (click)="saveSender()" [disabled]="savingSender()" class="btn-primary px-4 py-2">{{ savingSender() ? 'Saving…' : 'Save sender' }}</button>
        </div>

        <!-- Templates -->
        <h2 class="font-semibold text-slate-800 mb-3">Message templates</h2>
        @if (templates().length === 0) {
          <p class="text-slate-400 text-sm">No templates yet.</p>
        }
        <div class="space-y-3">
          @for (t of templates(); track t.id) {
            <div class="bg-white border border-slate-200 rounded-xl overflow-hidden">
              <button type="button" (click)="toggle(t.id)" class="w-full flex items-center justify-between px-5 py-3 text-left hover:bg-slate-50">
                <span class="flex items-center gap-2">
                  <span class="font-medium text-slate-800">{{ t.label }}</span>
                  <span class="text-xs px-1.5 py-0.5 rounded bg-slate-100 text-slate-500">{{ t.channel }}</span>
                  @if (!t.isActive) { <span class="text-xs px-1.5 py-0.5 rounded bg-slate-100 text-slate-400">Off</span> }
                </span>
                <span class="text-slate-400 text-sm">{{ open() === t.id ? '−' : 'Edit' }}</span>
              </button>
              @if (open() === t.id) {
                <div class="border-t border-slate-100 p-5 space-y-3">
                  @if (t.channel === 'Email') {
                    <label class="block"><span class="lbl">Subject</span><input class="input" [(ngModel)]="t.subject" [name]="'sub' + t.id" /></label>
                  }
                  @if (t.channel === 'WhatsApp') {
                    <div class="rounded-lg bg-amber-50 border border-amber-200 text-amber-800 text-xs px-3 py-2">
                      WhatsApp only sends pre-approved Meta templates. Submit the template's wording for approval in your
                      BSP's console first, then paste the approved template's id below — keep the message text here in
                      the same wording and placeholder order as what got approved.
                    </div>
                    <label class="block">
                      <span class="lbl">External template ID (from Gupshup/Meta)</span>
                      <input class="input font-mono text-xs" [(ngModel)]="t.externalTemplateId" [name]="'ext' + t.id" placeholder="e.g. 8f2c1a90-..." />
                    </label>
                  }
                  <label class="block">
                    <span class="lbl">{{ t.channel === 'Email' ? 'Body (HTML)' : 'Message' }}</span>
                    <textarea class="input font-mono text-xs" rows="8" [(ngModel)]="t.body" [name]="'body' + t.id"></textarea>
                    <span class="text-xs text-slate-400 mt-1 block font-mono">{{ tokenHint }}</span>
                  </label>
                  <label class="flex items-center gap-2 cursor-pointer">
                    <input type="checkbox" [(ngModel)]="t.isActive" [name]="'act' + t.id" class="w-4 h-4" />
                    <span class="text-sm text-slate-700">Active (send this notification)</span>
                  </label>
                  @if (t.channel === 'WhatsApp' && t.isActive && !t.externalTemplateId) {
                    <p class="text-xs text-red-600">Active with no template id set — sends on this channel will silently fail and fall back to SMS/Email.</p>
                  }
                  <button type="button" (click)="save(t)" [disabled]="savingId() === t.id" class="btn-primary px-4 py-2">{{ savingId() === t.id ? 'Saving…' : 'Save template' }}</button>
                </div>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class AdminNotificationTemplatesComponent implements OnInit {
  private readonly api = inject(NotificationTemplatesService);
  readonly loading = signal(true);
  readonly savingId = signal<number | null>(null);
  readonly savingSender = signal(false);
  readonly message = signal<string | null>(null);
  readonly open = signal<number | null>(null);
  readonly templates = signal<NotificationTemplate[]>([]);
  sender: NotificationSender = { senderName: '', replyToEmail: '' };
  // Literal example text — kept as a property so Angular doesn't treat the {{…}} as interpolation.
  readonly tokenHint = 'Use {{token}} placeholders, e.g. {{orderNumber}}, {{customerName}}.';

  ngOnInit(): void {
    forkJoin({ list: this.api.list(), sender: this.api.getSender() }).subscribe({
      next: ({ list, sender }) => { this.templates.set(list); this.sender = sender; this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  toggle(id: number): void { this.open.update((o) => (o === id ? null : id)); }

  private flash(m: string): void { this.message.set(m); setTimeout(() => this.message.set(null), 2500); }

  save(t: NotificationTemplate): void {
    this.savingId.set(t.id); this.message.set(null);
    this.api.update(t.id, { subject: t.subject, body: t.body, externalTemplateId: t.externalTemplateId, isActive: t.isActive }).subscribe({
      next: (updated) => {
        this.templates.update((list) => list.map((x) => (x.id === updated.id ? updated : x)));
        this.savingId.set(null); this.flash(`${updated.label} template saved.`);
      },
      error: () => this.savingId.set(null),
    });
  }

  saveSender(): void {
    this.savingSender.set(true); this.message.set(null);
    this.api.updateSender(this.sender).subscribe({
      next: (s) => { this.sender = s; this.savingSender.set(false); this.flash('Sender identity saved.'); },
      error: () => this.savingSender.set(false),
    });
  }
}
