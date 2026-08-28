import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminCatalogService } from '../../../core/services/admin-catalog.service';
import { ProductListItem } from '../../../core/models/catalog.model';
import { Campaign, CampaignSendStatus, CampaignSummary, CustomerSegment, Goal, GrowthService } from '../../../core/services/growth.service';

@Component({
  selector: 'app-admin-campaigns',
  imports: [FormsModule, RouterLink, DatePipe],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <div class="flex items-start justify-between gap-4 mb-6">
        <div>
          <h1 class="text-xl font-bold text-slate-900">📣 Campaigns</h1>
          <p class="text-sm text-slate-500">One goal → a post for every channel, generated together.</p>
        </div>
        <a routerLink="/admin/growth" class="text-sm text-primary hover:underline shrink-0">Single post →</a>
      </div>

      @if (locked()) {
        <div class="rounded-xl border border-violet-200 bg-violet-50 p-6 text-center">
          <div class="text-3xl">📣</div>
          <h2 class="font-semibold text-violet-900 mt-2">Campaigns are a plan upgrade away</h2>
          <a routerLink="/admin/billing" class="inline-block mt-3 btn-primary">See plans</a>
        </div>
      } @else {
        <!-- Builder -->
        <div class="bg-white border border-slate-200 rounded-xl p-5 mb-6">
          <label class="lbl">Goal</label>
          <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-2 mb-4">
            @for (g of goals(); track g.key) {
              <button type="button" (click)="goal.set(g.key)"
                      class="text-left rounded-lg border p-2.5 transition"
                      [class]="goal() === g.key ? 'border-primary ring-1 ring-primary/20 bg-blue-50/40' : 'border-slate-200 hover:border-slate-300'">
                <div class="text-sm font-medium text-slate-800">{{ g.label }}</div>
                <div class="text-xs text-slate-500 mt-0.5">{{ g.description }}</div>
              </button>
            }
          </div>

          <div class="grid sm:grid-cols-2 gap-4">
            <div>
              <label class="lbl">Product</label>
              <select [(ngModel)]="productId" name="product" class="input">
                <option [ngValue]="null" disabled>Choose a product…</option>
                @for (p of products(); track p.productId) { <option [ngValue]="p.productId">{{ p.name }}</option> }
              </select>
            </div>
            <div>
              <label class="lbl">Language</label>
              <select [(ngModel)]="language" name="lang" class="input">
                <option [ngValue]="null">Brand default</option>
                @for (l of languages; track l) { <option [ngValue]="l">{{ l }}</option> }
              </select>
            </div>
          </div>
          <div class="mt-3">
            <label class="lbl">Anything to add? <span class="text-slate-400 font-normal">(optional)</span></label>
            <input [(ngModel)]="brief" name="brief" class="input" placeholder="e.g. Diwali — flat 20% off, mention free delivery" />
          </div>

          @if (error()) { <p class="text-sm text-red-600 mt-3">{{ error() }}</p> }
          <button type="button" (click)="generate()" [disabled]="generating() || !goal() || productId === null"
                  class="btn-primary mt-4 disabled:opacity-60">
            {{ generating() ? 'Generating all channels…' : 'Generate campaign (16 credits)' }}
          </button>
        </div>

        <!-- Fresh result -->
        @if (result(); as c) {
          <div class="bg-white border border-emerald-200 rounded-xl p-5 mb-6">
            <h2 class="font-semibold text-slate-800 mb-3">{{ c.name }}</h2>
            @for (ch of c.channels; track ch.channel) {
              <div class="border-t border-slate-100 py-3 first:border-0">
                <div class="flex items-center justify-between">
                  <span class="text-[11px] px-2 py-0.5 rounded-full bg-slate-100 text-slate-600">{{ label(ch.channel) }}</span>
                  @if (ch.content) {
                    <button type="button" (click)="copy(ch)" class="text-sm text-primary hover:underline">{{ copied() === ch.channel ? 'Copied ✓' : 'Copy' }}</button>
                  }
                </div>
                @if (ch.content) {
                  @if (ch.content.title) { <div class="text-sm font-medium text-slate-800 mt-2">{{ ch.content.title }}</div> }
                  <p class="text-sm text-slate-600 mt-1 whitespace-pre-line">{{ ch.content.body }}</p>
                } @else {
                  <p class="text-sm text-red-600 mt-1">Couldn't generate this one: {{ ch.error }}</p>
                }
              </div>
            }
            <div class="flex gap-2 pt-3 mt-1 border-t border-slate-100">
              <button type="button" (click)="openSend(c.id, c.name)" class="btn-primary text-sm">📧 Send / schedule email</button>
              <button type="button" (click)="exportCampaign(c.id, c.name)" class="text-sm text-slate-600 hover:text-primary border border-slate-200 rounded-lg px-3">⬇ Export pack</button>
            </div>
          </div>
        }

        <!-- Past campaigns -->
        <h2 class="font-semibold text-slate-800 mb-2">Recent campaigns</h2>
        @if ((past()?.length ?? 0) === 0) {
          <p class="text-sm text-slate-400">No campaigns yet.</p>
        } @else {
          <div class="space-y-2">
            @for (c of past() ?? []; track c.id) {
              <div class="bg-white border border-slate-200 rounded-lg p-3 flex items-center justify-between">
                <div>
                  <div class="text-sm font-medium text-slate-800">{{ c.name }}</div>
                  <a [routerLink]="['/admin/growth/library']" [queryParams]="{ campaignId: c.id }"
                     class="text-xs text-primary hover:underline">{{ c.pieces }} pieces</a>
                  <span class="text-xs text-slate-400"> · {{ c.createdAt | date:'dd MMM, HH:mm' }}</span>
                  @if (c.status && c.status !== 'Draft' && c.status !== 'Kept') {
                    <span class="ml-2 text-[11px] px-2 py-0.5 rounded-full"
                          [class]="statusClass(c.status)">{{ statusLabel(c.status) }}</span>
                  }
                </div>
                <div class="flex items-center gap-3 shrink-0">
                  <button type="button" (click)="openSend(c.id, c.name)" class="text-sm text-primary hover:underline">Send</button>
                  <button type="button" (click)="exportCampaign(c.id, c.name)" class="text-sm text-slate-500 hover:text-primary">Export</button>
                  <button type="button" (click)="removeCampaign(c)" class="text-sm text-red-600 hover:underline">Delete</button>
                </div>
              </div>
            }
          </div>
        }

        <!-- Send / schedule modal -->
        @if (sendFor(); as sc) {
          <div class="fixed inset-0 z-50 flex items-center justify-center p-4">
            <div class="absolute inset-0 bg-black/40" (click)="closeSend()"></div>
            <div class="relative bg-white rounded-xl shadow-xl w-full max-w-md p-5">
              <div class="flex items-start justify-between gap-3 mb-3">
                <h3 class="font-semibold text-slate-900">Send email campaign</h3>
                <button type="button" (click)="closeSend()" class="text-slate-400 text-xl leading-none">×</button>
              </div>
              <p class="text-xs text-slate-500 -mt-2 mb-4 truncate">{{ sc.name }}</p>

              @if (sendStatus() && !sendStatus()!.hasEmailContent) {
                <p class="text-sm text-amber-700 bg-amber-50 border border-amber-200 rounded-lg p-3">
                  This campaign has no email copy to send. Generate the email channel first.
                </p>
              } @else if (sendStatus()?.status === 'Sending') {
                <p class="text-sm text-slate-600">This campaign is currently being sent…</p>
              } @else {
                <label class="lbl">Send to</label>
                <select [(ngModel)]="sendSegment" name="seg" (ngModelChange)="refreshPreview()" class="input mb-1">
                  @for (s of segments(); track s.key) { <option [ngValue]="s.key">{{ s.label }} ({{ s.count }})</option> }
                </select>
                <p class="text-xs text-slate-500 mb-4">
                  @if (previewBusy()) { Checking… }
                  @else { <strong>{{ sendStatus()?.eligibleNow ?? 0 }}</strong> customer(s) here have opted in to email marketing. }
                </p>

                <label class="lbl">When</label>
                <div class="flex items-center gap-3 mb-2 text-sm">
                  <label class="inline-flex items-center gap-1.5"><input type="radio" name="when" [checked]="!scheduleLater" (change)="scheduleLater = false" /> Send now</label>
                  <label class="inline-flex items-center gap-1.5"><input type="radio" name="when" [checked]="scheduleLater" (change)="scheduleLater = true" /> Schedule</label>
                </div>
                @if (scheduleLater) {
                  <input type="datetime-local" [(ngModel)]="scheduleAt" name="at" class="input mb-2" />
                }

                @if (sendError()) { <p class="text-sm text-red-600 mb-2">{{ sendError() }}</p> }

                <div class="flex items-center justify-between mt-4">
                  @if (sendStatus()?.status === 'Scheduled') {
                    <button type="button" (click)="cancelSend()" class="text-sm text-red-600 hover:underline">Cancel scheduled send</button>
                  } @else { <span></span> }
                  <button type="button" (click)="doSend()" [disabled]="sendBusy() || (sendStatus()?.eligibleNow ?? 0) === 0"
                          class="btn-primary text-sm disabled:opacity-60">
                    {{ sendBusy() ? 'Working…' : (scheduleLater ? 'Schedule send' : 'Send now') }}
                  </button>
                </div>
              }
            </div>
          </div>
        }
      }
    </div>
  `,
})
export class AdminCampaignsComponent implements OnInit {
  private readonly api = inject(GrowthService);
  private readonly catalog = inject(AdminCatalogService);

  readonly goals = signal<Goal[]>([]);
  readonly products = signal<ProductListItem[]>([]);
  readonly result = signal<Campaign | null>(null);
  readonly past = signal<CampaignSummary[] | null>(null);
  readonly generating = signal(false);
  readonly locked = signal(false);
  readonly copied = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  goal = signal<string | null>(null);
  productId: number | null = null;
  language: string | null = null;
  brief = '';

  readonly languages = ['English', 'Hindi', 'Tamil', 'Telugu', 'Hinglish'];
  private readonly labels: Record<string, string> = {
    'instagram-caption': 'Instagram', 'facebook-post': 'Facebook', 'whatsapp': 'WhatsApp', 'email': 'Email',
  };

  // ----- Send / schedule (M1) -----
  readonly segments = signal<CustomerSegment[]>([]);
  readonly sendFor = signal<{ id: number; name: string } | null>(null);
  readonly sendStatus = signal<CampaignSendStatus | null>(null);
  readonly sendBusy = signal(false);
  readonly previewBusy = signal(false);
  readonly sendError = signal<string | null>(null);
  sendSegment: string | null = 'all';
  scheduleLater = false;
  scheduleAt = '';

  ngOnInit(): void {
    this.api.goals().subscribe({
      next: (g) => this.goals.set(g),
      error: (e) => { if (e?.status === 402) this.locked.set(true); },
    });
    this.catalog.listProducts({ page: 1, pageSize: 200 }).subscribe((r) => this.products.set(r.items));
    this.api.segments().subscribe({ next: (s) => this.segments.set(s), error: () => {} });
    this.loadPast();
  }

  openSend(id: number, name: string): void {
    this.sendFor.set({ id, name });
    this.sendStatus.set(null);
    this.sendError.set(null);
    this.scheduleLater = false;
    this.scheduleAt = '';
    this.refreshPreview();
  }
  closeSend(): void { this.sendFor.set(null); }

  refreshPreview(): void {
    const sc = this.sendFor();
    if (!sc) return;
    this.previewBusy.set(true);
    this.api.sendPreview(sc.id, this.sendSegment).subscribe({
      next: (st) => { this.sendStatus.set(st); this.previewBusy.set(false); },
      error: () => this.previewBusy.set(false),
    });
  }

  doSend(): void {
    const sc = this.sendFor();
    if (!sc) return;
    this.sendBusy.set(true);
    this.sendError.set(null);
    const scheduledAt = this.scheduleLater && this.scheduleAt ? new Date(this.scheduleAt).toISOString() : null;
    this.api.sendCampaign(sc.id, this.sendSegment, scheduledAt).subscribe({
      next: () => { this.sendBusy.set(false); this.closeSend(); this.loadPast(); },
      error: (e) => { this.sendBusy.set(false); this.sendError.set(e?.error?.message ?? 'Could not start the send.'); },
    });
  }

  cancelSend(): void {
    const sc = this.sendFor();
    if (!sc) return;
    this.sendBusy.set(true);
    this.api.cancelSend(sc.id).subscribe({
      next: () => { this.sendBusy.set(false); this.closeSend(); this.loadPast(); },
      error: (e) => { this.sendBusy.set(false); this.sendError.set(e?.error?.message ?? 'Could not cancel.'); },
    });
  }

  exportCampaign(id: number, name: string): void {
    this.api.exportCampaign(id).subscribe((blob) => {
      const slug = name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'campaign';
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url; a.download = `campaign-${slug}.md`;
      document.body.appendChild(a); a.click(); a.remove();
      URL.revokeObjectURL(url);
    });
  }

  statusLabel(s: string): string {
    return { Scheduled: 'Scheduled', Sending: 'Sending…', Sent: 'Sent', Failed: 'Failed' }[s] ?? s;
  }
  statusClass(s: string): string {
    return {
      Scheduled: 'bg-amber-100 text-amber-700',
      Sending: 'bg-blue-100 text-blue-700',
      Sent: 'bg-emerald-100 text-emerald-700',
      Failed: 'bg-red-100 text-red-700',
    }[s] ?? 'bg-slate-100 text-slate-600';
  }

  label(key: string): string { return this.labels[key] ?? key; }

  generate(): void {
    if (!this.goal() || this.productId === null) return;
    this.generating.set(true);
    this.error.set(null);
    this.api.createCampaign({
      goal: this.goal()!,
      productId: this.productId,
      brief: this.brief.trim() || null,
      language: this.language,
    }).subscribe({
      next: (c) => { this.result.set(c); this.generating.set(false); this.loadPast(); },
      error: (e) => {
        this.error.set(e?.status === 402
          ? "You're out of AI credits — top up to run a campaign."
          : e?.error?.message ?? 'Could not generate the campaign.');
        this.generating.set(false);
      },
    });
  }

  copy(ch: { channel: string; content: { title: string | null; body: string } | null }): void {
    if (!ch.content) return;
    const text = (ch.content.title ? ch.content.title + '\n\n' : '') + ch.content.body;
    navigator.clipboard?.writeText(text).then(() => {
      this.copied.set(ch.channel);
      setTimeout(() => this.copied.set(null), 2000);
    });
  }

  removeCampaign(c: CampaignSummary): void {
    this.api.removeCampaign(c.id).subscribe(() => this.loadPast());
  }

  private loadPast(): void {
    this.api.campaigns().subscribe((r) => this.past.set(r.items));
  }
}
