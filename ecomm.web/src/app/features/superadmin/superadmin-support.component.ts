import { DatePipe } from '@angular/common';
import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { SupportService } from '../../core/services/support.service';
import { NotificationService } from '../../core/services/notification.service';
import { Agent, QueueFilter, Ticket, TicketThread } from '../../core/models/support.model';

/**
 * Platform support desk — an agent workspace over the cross-tenant queue. Filter by status / priority /
 * tier / assignment, then work a ticket: assign, escalate, set status/priority/tags, reply or note, and
 * read the activity trail. Replies arrive live over SignalR.
 */
@Component({
  selector: 'app-superadmin-support',
  imports: [FormsModule, RouterLink, DatePipe],
  template: `
    <h1 class="text-xl font-bold text-slate-900 mb-1">Support desk</h1>
    <p class="text-sm text-slate-500 mb-4">Tickets from every store.</p>

    <!-- Filters -->
    <div class="flex flex-wrap items-center gap-2 mb-4 text-sm">
      <select [(ngModel)]="filter.status" (ngModelChange)="loadQueue()" class="input py-1 w-32">
        <option [ngValue]="undefined">Any status</option>
        @for (s of statuses; track s) { <option [ngValue]="s">{{ s }}</option> }
      </select>
      <select [(ngModel)]="filter.priority" (ngModelChange)="loadQueue()" class="input py-1 w-32">
        <option [ngValue]="undefined">Any priority</option>
        @for (p of priorities; track p) { <option [ngValue]="p">{{ p }}</option> }
      </select>
      <select [(ngModel)]="filter.tier" (ngModelChange)="loadQueue()" class="input py-1 w-24">
        <option [ngValue]="undefined">Any tier</option>
        @for (t of tiers; track t) { <option [ngValue]="t">{{ t }}</option> }
      </select>
      <button type="button" (click)="toggle('mine')" class="px-3 py-1.5 rounded-lg border"
              [class]="filter.mine ? 'border-primary text-primary bg-blue-50' : 'border-slate-200 text-slate-600'">My tickets</button>
      <button type="button" (click)="toggle('unassigned')" class="px-3 py-1.5 rounded-lg border"
              [class]="filter.unassigned ? 'border-primary text-primary bg-blue-50' : 'border-slate-200 text-slate-600'">Unassigned</button>
    </div>

    <div class="grid lg:grid-cols-5 gap-4">
      <!-- Queue -->
      <div class="lg:col-span-2 bg-white border border-slate-200 rounded-xl p-3 space-y-1">
        @for (t of tickets(); track t.id) {
          <button type="button" (click)="open(t.id)"
                  class="w-full text-left rounded-lg p-2.5 border transition"
                  [class]="thread()?.ticket?.id === t.id ? 'border-primary ring-1 ring-primary/20' : 'border-transparent hover:bg-slate-50'">
            <div class="flex items-center justify-between gap-2">
              <span class="text-sm font-medium text-slate-800 truncate">{{ t.subject }}</span>
              <span class="text-[10px] px-1.5 py-0.5 rounded-full shrink-0" [class]="statusClass(t.status)">{{ t.status }}</span>
            </div>
            <div class="flex items-center gap-1.5 mt-1">
              <span class="text-[10px] px-1 rounded" [class]="priorityClass(t.priority)">{{ t.priority }}</span>
              <span class="text-[10px] px-1 rounded bg-slate-100 text-slate-500">{{ t.escalationTier }}</span>
              <span class="text-xs text-slate-400 truncate">{{ t.storeName }}</span>
            </div>
            <div class="text-[11px] text-slate-400 mt-0.5">
              {{ t.reference }} · {{ t.assignedToName || 'unassigned' }}
            </div>
          </button>
        }
        @if (!tickets().length) { <p class="py-8 text-center text-slate-400 text-sm">No tickets match.</p> }
      </div>

      <!-- Ticket detail -->
      <div class="lg:col-span-3">
        @if (thread(); as th) {
          <div class="bg-white border border-slate-200 rounded-xl p-4">
            <div class="flex items-start justify-between gap-3">
              <div>
                <h2 class="font-semibold text-slate-800">{{ th.ticket.subject }}</h2>
                <div class="text-xs text-slate-500">
                  {{ th.ticket.reference }} ·
                  <a [routerLink]="['/superadmin/tenants', th.ticket.tenantId]" class="text-blue-600 hover:underline">{{ th.ticket.storeName || 'store' }} ↗</a>
                </div>
              </div>
            </div>

            <!-- Controls -->
            <div class="grid sm:grid-cols-2 gap-3 mt-4 pb-4 border-b border-slate-100">
              <label class="text-xs text-slate-500">Status
                <select [ngModel]="th.ticket.status" (ngModelChange)="setStatus($event)" class="input py-1 mt-0.5 text-sm">
                  @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
                </select>
              </label>
              <label class="text-xs text-slate-500">Priority
                <select [ngModel]="th.ticket.priority" (ngModelChange)="setPriority($event)" class="input py-1 mt-0.5 text-sm">
                  @for (p of priorities; track p) { <option [value]="p">{{ p }}</option> }
                </select>
              </label>
              <label class="text-xs text-slate-500">Assignee
                <select [ngModel]="th.ticket.assignedToUserId" (ngModelChange)="assign($event)" class="input py-1 mt-0.5 text-sm">
                  <option [ngValue]="null">Unassigned</option>
                  @for (a of agents(); track a.userId) { <option [ngValue]="a.userId">{{ a.name }}</option> }
                </select>
              </label>
              <div class="text-xs text-slate-500">Tier: <span class="font-medium text-slate-700">{{ th.ticket.escalationTier }}</span>
                @if (th.ticket.escalationTier !== 'L3') {
                  <button type="button" (click)="escalate()" class="ml-2 px-2 py-0.5 rounded border border-amber-300 text-amber-700 hover:bg-amber-50">Escalate ↑</button>
                }
              </div>
            </div>

            <!-- Tags -->
            <div class="py-3 border-b border-slate-100">
              <label class="text-xs text-slate-500">Tags <span class="text-slate-400">(comma-separated)</span></label>
              <div class="flex gap-2 mt-0.5">
                <input [(ngModel)]="tagsDraft" class="input py-1 text-sm" placeholder="billing, urgent, refund" />
                <button type="button" (click)="saveTags()" class="px-3 py-1 rounded-lg border border-slate-300 text-sm text-slate-600 hover:bg-slate-50">Save</button>
              </div>
            </div>

            <!-- Thread -->
            <div class="space-y-2 max-h-72 overflow-auto my-3">
              @for (m of th.messages; track m.id) {
                <div class="rounded-lg px-3 py-2 text-sm" [class]="msgClass(m.fromPlatform, m.isInternalNote)">
                  <div class="text-[11px] mb-0.5 opacity-70">{{ m.isInternalNote ? 'Internal note' : (m.fromPlatform ? 'You' : 'Merchant') }} · {{ m.createdAt | date:'short' }}</div>
                  <div class="whitespace-pre-line">{{ m.body }}</div>
                </div>
              }
            </div>

            <textarea [(ngModel)]="reply" rows="2" placeholder="Reply…" class="input w-full mb-2"></textarea>
            <div class="flex items-center gap-2">
              <button type="button" (click)="send(false)" class="btn-primary text-sm">Send reply</button>
              <button type="button" (click)="send(true)" class="px-3 py-1.5 rounded-lg border border-amber-300 text-amber-700 text-sm hover:bg-amber-50">Internal note</button>
            </div>

            <!-- Activity -->
            @if (th.activity.length) {
              <div class="mt-4 pt-3 border-t border-slate-100">
                <h3 class="text-xs font-semibold text-slate-500 mb-1.5">Activity</h3>
                <ul class="space-y-1">
                  @for (a of th.activity; track a.id) {
                    <li class="text-xs text-slate-500">{{ a.detail }} <span class="text-slate-400">· {{ a.createdAt | date:'short' }}</span></li>
                  }
                </ul>
              </div>
            }
          </div>
        } @else {
          <div class="bg-white border border-slate-200 rounded-xl p-8 text-center text-slate-400 text-sm">Select a ticket.</div>
        }
      </div>
    </div>
  `,
})
export class SuperAdminSupportComponent implements OnInit {
  private readonly svc = inject(SupportService);
  private readonly notifications = inject(NotificationService);

  readonly tickets = signal<Ticket[]>([]);
  readonly thread = signal<TicketThread | null>(null);
  readonly agents = signal<Agent[]>([]);
  filter: QueueFilter = {};
  reply = '';
  tagsDraft = '';

  readonly statuses = ['New', 'Open', 'Pending', 'OnHold', 'Resolved', 'Closed'];
  readonly priorities = ['Low', 'Normal', 'High', 'Urgent'];
  readonly tiers = ['L1', 'L2', 'L3'];

  constructor() {
    // A pushed reply appends to the open thread (dedup by id).
    effect(() => {
      const m = this.notifications.liveMessage();
      const open = this.thread();
      if (!m || !open || m.conversationId !== open.ticket.id) return;
      if (open.messages.some((x) => x.id === m.messageId)) return;
      this.thread.set({
        ...open,
        messages: [...open.messages, { id: m.messageId, fromPlatform: m.authorType !== 'Merchant', isInternalNote: false, body: m.body, createdAt: m.createdAt }],
      });
    });
  }

  ngOnInit(): void {
    this.svc.agents().subscribe((a) => this.agents.set(a));
    this.loadQueue();
  }

  toggle(k: 'mine' | 'unassigned'): void {
    this.filter[k] = !this.filter[k];
    if (k === 'mine' && this.filter.mine) this.filter.unassigned = false;
    if (k === 'unassigned' && this.filter.unassigned) this.filter.mine = false;
    this.loadQueue();
  }

  loadQueue(): void { this.svc.queue(this.filter).subscribe((t) => this.tickets.set(t)); }

  open(id: number): void {
    const prev = this.thread()?.ticket.id;
    if (prev && prev !== id) void this.notifications.leaveConversation(prev);
    this.svc.adminThread(id).subscribe((th) => {
      this.thread.set(th);
      this.tagsDraft = th.ticket.tags ?? '';
      void this.notifications.joinConversation(id);
    });
  }

  send(isInternal: boolean): void {
    const th = this.thread(); if (!th || !this.reply.trim()) return;
    this.svc.adminReply(th.ticket.id, this.reply.trim(), isInternal).subscribe(() => { this.reply = ''; this.open(th.ticket.id); this.loadQueue(); });
  }

  setStatus(s: string): void { this.mutate((id) => this.svc.setStatus(id, s)); }
  setPriority(p: string): void { this.mutate((id) => this.svc.setPriority(id, p)); }
  assign(userId: number | null): void { this.mutate((id) => this.svc.assign(id, userId)); }
  escalate(): void { this.mutate((id) => this.svc.escalate(id)); }
  saveTags(): void { this.mutate((id) => this.svc.setTags(id, this.tagsDraft)); }

  /** Apply a ticket mutation, patch the header from the returned ticket, and refresh the queue + activity. */
  private mutate(call: (id: number) => import('rxjs').Observable<Ticket>): void {
    const th = this.thread(); if (!th) return;
    call(th.ticket.id).subscribe((updated) => {
      this.open(updated.id);   // reloads thread incl. new activity
      this.loadQueue();
    });
  }

  statusClass(s: string): string {
    return s === 'Closed' || s === 'Resolved' ? 'bg-slate-100 text-slate-500'
      : s === 'Pending' || s === 'OnHold' ? 'bg-amber-50 text-amber-700'
      : s === 'New' ? 'bg-violet-50 text-violet-700'
      : 'bg-green-50 text-green-700';
  }
  priorityClass(p: string): string {
    return p === 'Urgent' ? 'bg-red-100 text-red-700'
      : p === 'High' ? 'bg-orange-100 text-orange-700'
      : p === 'Low' ? 'bg-slate-100 text-slate-400'
      : 'bg-slate-100 text-slate-500';
  }
  msgClass(fromPlatform: boolean, isInternal: boolean): string {
    return isInternal ? 'bg-amber-50 border border-amber-200 text-amber-900'
      : fromPlatform ? 'bg-blue-50 text-blue-900' : 'bg-slate-100 text-slate-700';
  }
}
