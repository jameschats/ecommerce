import { DatePipe } from '@angular/common';
import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SupportService } from '../../../core/services/support.service';
import { NotificationService } from '../../../core/services/notification.service';
import { Ticket, TicketThread } from '../../../core/models/support.model';

/** Merchant support: open a ticket, read the thread, reply. */
@Component({
  selector: 'app-admin-support',
  imports: [FormsModule, DatePipe],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Support</h1>
      <p class="text-sm text-slate-500 mb-4">Message the platform team. We'll reply here.</p>

      <div class="grid lg:grid-cols-2 gap-4">
        <div class="bg-white border border-slate-200 rounded-xl p-4">
          <div class="text-sm font-medium text-slate-700 mb-2">New request</div>
          <input [(ngModel)]="subject" placeholder="Subject" class="input w-full mb-2" />
          <textarea [(ngModel)]="message" rows="3" placeholder="How can we help?" class="input w-full mb-2"></textarea>
          <button type="button" (click)="create()" class="btn-primary text-sm">Create ticket</button>

          <div class="text-sm font-medium text-slate-700 mt-5 mb-2">Your tickets</div>
          <table class="w-full text-sm">
            <tbody>
              @for (t of tickets(); track t.id) {
                <tr class="border-b border-slate-100 hover:bg-slate-50 cursor-pointer" (click)="open(t.id)">
                  <td class="py-2"><div class="font-medium text-slate-800">{{ t.subject }}</div><div class="text-xs text-slate-400">@if (t.reference) { <span>{{ t.reference }} · </span> }{{ t.lastMessageAt ? (t.lastMessageAt | date:'short') : (t.createdAt | date:'short') }}</div></td>
                  <td class="text-right"><span class="text-xs px-1.5 py-0.5 rounded" [class]="statusClass(t.status)">{{ t.status }}</span></td>
                </tr>
              }
              @if (!tickets().length) { <tr><td class="py-6 text-center text-slate-400">No tickets yet.</td></tr> }
            </tbody>
          </table>
        </div>

        <div class="bg-white border border-slate-200 rounded-xl p-4">
          @if (thread(); as th) {
            <h2 class="font-semibold text-slate-800">{{ th.ticket.subject }}</h2>
            @if (th.ticket.reference) { <div class="text-xs text-slate-400 mb-3">{{ th.ticket.reference }}</div> } @else { <div class="mb-3"></div> }
            <div class="space-y-2 max-h-80 overflow-auto mb-3">
              @for (m of th.messages; track m.id) {
                <div class="rounded-lg px-3 py-2 text-sm" [class]="m.fromPlatform ? 'bg-blue-50 text-blue-900' : 'bg-slate-100 text-slate-700'">
                  <div class="text-[11px] mb-0.5 opacity-70">{{ m.fromPlatform ? 'Support' : 'You' }} · {{ m.createdAt | date:'short' }}</div>
                  {{ m.body }}
                </div>
              }
            </div>
            @if (th.ticket.status !== 'Closed') {
              <textarea [(ngModel)]="reply" rows="2" placeholder="Reply…" class="input w-full mb-2"></textarea>
              <button type="button" (click)="send(th.ticket.id)" class="btn-primary text-sm">Send</button>
            } @else { <p class="text-sm text-slate-400">This ticket is closed. Reply to reopen.</p>
              <textarea [(ngModel)]="reply" rows="2" placeholder="Reply to reopen…" class="input w-full mb-2 mt-2"></textarea>
              <button type="button" (click)="send(th.ticket.id)" class="btn-primary text-sm">Send</button>
            }
          } @else {
            <div class="text-slate-400 text-sm p-8 text-center">Select a ticket, or create one.</div>
          }
        </div>
      </div>
    </div>
  `,
})
export class AdminSupportComponent implements OnInit {
  private readonly svc = inject(SupportService);
  private readonly notifications = inject(NotificationService);
  readonly tickets = signal<Ticket[]>([]);
  readonly thread = signal<TicketThread | null>(null);
  subject = '';
  message = '';
  reply = '';

  constructor() {
    // A platform reply arrives live in the open thread (dedup by id); the socket only accelerates
    // what a refresh would show, so a missed push costs a reload, not a message.
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

  ngOnInit(): void { this.load(); }
  load(): void { this.svc.myTickets().subscribe((t) => this.tickets.set(t)); }
  open(id: number): void {
    const prev = this.thread()?.ticket.id;
    if (prev && prev !== id) void this.notifications.leaveConversation(prev);
    this.svc.thread(id).subscribe((th) => { this.thread.set(th); void this.notifications.joinConversation(id); });
  }

  create(): void {
    if (!this.subject.trim() || !this.message.trim()) return;
    this.svc.createTicket(this.subject.trim(), this.message.trim()).subscribe((t) => { this.subject = ''; this.message = ''; this.load(); this.open(t.id); });
  }
  send(id: number): void {
    if (!this.reply.trim()) return;
    this.svc.reply(id, this.reply.trim()).subscribe(() => { this.reply = ''; this.open(id); this.load(); });
  }

  statusClass(s: string): string {
    return s === 'Closed' ? 'bg-slate-100 text-slate-500'
      : s === 'Pending' ? 'bg-amber-50 text-amber-700 border border-amber-200'
      : 'bg-green-50 text-green-700 border border-green-200';
  }
}
