import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { SupportService } from '../../core/services/support.service';
import { Ticket, TicketThread } from '../../core/models/support.model';

/** Cross-tenant support queue: pick a ticket, read the thread (incl. internal notes), reply / note / set status. */
@Component({
  selector: 'app-superadmin-support',
  imports: [FormsModule, RouterLink, DatePipe],
  template: `
    <h1 class="text-xl font-bold text-slate-900 mb-1">Support</h1>
    <p class="text-sm text-slate-500 mb-4">Tickets from every store.</p>

    <div class="grid lg:grid-cols-2 gap-4">
      <div class="bg-white border border-slate-200 rounded-xl p-4">
        <div class="flex items-center justify-between mb-3">
          <h2 class="font-semibold text-slate-800">Queue</h2>
          <select [(ngModel)]="status" (ngModelChange)="loadQueue()" class="input py-1 text-sm w-36">
            <option value="">All</option><option>Open</option><option>Pending</option><option>Closed</option>
          </select>
        </div>
        <table class="w-full text-sm">
          <tbody>
            @for (t of tickets(); track t.id) {
              <tr class="border-b border-slate-100 hover:bg-slate-50 cursor-pointer" (click)="open(t.id)">
                <td class="py-2">
                  <div class="font-medium text-slate-800">{{ t.subject }}</div>
                  <div class="text-xs text-slate-400">{{ t.storeName }} · {{ t.lastMessageAt ? (t.lastMessageAt | date:'short') : (t.createdAt | date:'short') }}</div>
                </td>
                <td class="text-right"><span class="text-xs px-1.5 py-0.5 rounded" [class]="statusClass(t.status)">{{ t.status }}</span></td>
              </tr>
            }
            @if (!tickets().length) { <tr><td class="py-8 text-center text-slate-400">No tickets.</td></tr> }
          </tbody>
        </table>
      </div>

      <div class="bg-white border border-slate-200 rounded-xl p-4">
        @if (thread(); as th) {
          <div class="flex items-center justify-between mb-1">
            <h2 class="font-semibold text-slate-800">{{ th.ticket.subject }}</h2>
            <a [routerLink]="['/superadmin/tenants', th.ticket.tenantId]" class="text-xs text-blue-600 hover:underline">{{ th.ticket.storeName || 'store' }} ↗</a>
          </div>
          <div class="flex gap-1 mb-3">
            @for (s of ['Open','Pending','Closed']; track s) {
              <button type="button" (click)="setStatus(th.ticket.id, s)" class="px-2 py-1 rounded-lg text-xs border" [class]="th.ticket.status === s ? 'bg-slate-900 text-white border-slate-900' : 'border-slate-300 text-slate-600 hover:bg-slate-50'">{{ s }}</button>
            }
          </div>

          <div class="space-y-2 max-h-80 overflow-auto mb-3">
            @for (m of th.messages; track m.id) {
              <div class="rounded-lg px-3 py-2 text-sm" [class]="msgClass(m.fromPlatform, m.isInternalNote)">
                <div class="text-[11px] mb-0.5 opacity-70">{{ m.isInternalNote ? 'Internal note' : (m.fromPlatform ? 'Platform' : 'Merchant') }} · {{ m.createdAt | date:'short' }}</div>
                {{ m.body }}
              </div>
            }
          </div>

          <textarea [(ngModel)]="reply" rows="2" placeholder="Reply…" class="input w-full mb-2"></textarea>
          <div class="flex items-center gap-2">
            <button type="button" (click)="send(th.ticket.id, false)" class="btn-primary text-xs">Send reply</button>
            <button type="button" (click)="send(th.ticket.id, true)" class="px-3 py-1.5 rounded-lg border border-amber-300 text-amber-700 text-xs hover:bg-amber-50">Add internal note</button>
          </div>
        } @else {
          <div class="text-slate-400 text-sm p-8 text-center">Select a ticket.</div>
        }
      </div>
    </div>
  `,
})
export class SuperAdminSupportComponent implements OnInit {
  private readonly svc = inject(SupportService);
  readonly tickets = signal<Ticket[]>([]);
  readonly thread = signal<TicketThread | null>(null);
  status = '';
  reply = '';

  ngOnInit(): void { this.loadQueue(); }
  loadQueue(): void { this.svc.queue(this.status).subscribe((t) => this.tickets.set(t)); }
  open(id: number): void { this.svc.adminThread(id).subscribe((th) => this.thread.set(th)); }

  send(id: number, isInternal: boolean): void {
    if (!this.reply.trim()) return;
    this.svc.adminReply(id, this.reply.trim(), isInternal).subscribe(() => { this.reply = ''; this.open(id); this.loadQueue(); });
  }
  setStatus(id: number, s: string): void { this.svc.setStatus(id, s).subscribe(() => { this.open(id); this.loadQueue(); }); }

  statusClass(s: string): string {
    return s === 'Closed' ? 'bg-slate-100 text-slate-500'
      : s === 'Pending' ? 'bg-amber-50 text-amber-700 border border-amber-200'
      : 'bg-green-50 text-green-700 border border-green-200';
  }
  msgClass(fromPlatform: boolean, isInternal: boolean): string {
    return isInternal ? 'bg-amber-50 border border-amber-200 text-amber-900'
      : fromPlatform ? 'bg-blue-50 text-blue-900' : 'bg-slate-100 text-slate-700';
  }
}
