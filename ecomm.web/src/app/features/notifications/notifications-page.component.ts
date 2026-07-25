import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AppNotification } from '../../core/models/notification.model';
import { NotificationService } from '../../core/services/notification.service';

/** Full notification history page. Used by both the account (customer) and admin sections;
 *  the API scopes the feed by role. */
@Component({
  selector: 'app-notifications-page',
  imports: [DatePipe],
  template: `
    <!-- Centred: this page is shared by the customer account and the admin shell, and the
         admin content area is far wider, which left the card stranded against the left edge. -->
    <div class="max-w-3xl mx-auto">
      <div class="flex items-center justify-between mb-4">
        <h2 class="font-semibold text-slate-800">Notifications</h2>
        <div class="flex items-center gap-3">
          @if (unread() > 0) { <button type="button" (click)="markAllRead()" class="text-sm text-primary hover:underline">Mark all read</button> }
          <div class="flex rounded-lg border border-slate-200 overflow-hidden text-xs">
            <button type="button" (click)="filter.set('all')" class="px-3 py-1" [class]="filter() === 'all' ? 'bg-primary text-white' : 'bg-white text-slate-600'">All</button>
            <button type="button" (click)="filter.set('unread')" class="px-3 py-1" [class]="filter() === 'unread' ? 'bg-primary text-white' : 'bg-white text-slate-600'">Unread</button>
          </div>
        </div>
      </div>

      @if (loading()) {
        <p class="text-slate-400 text-sm">Loading…</p>
      } @else if (!visible().length) {
        <div class="bg-white rounded-xl border border-slate-200 p-10 text-center text-slate-500">
          {{ filter() === 'unread' ? 'No unread notifications.' : "You're all caught up." }}
        </div>
      } @else {
        <div class="bg-white rounded-xl border border-slate-200 divide-y divide-slate-100 overflow-hidden">
          @for (n of visible(); track n.id) {
            <button type="button" (click)="open(n)" class="w-full text-left px-4 py-3 hover:bg-slate-50 flex gap-3" [class.bg-primary/5]="!n.isRead">
              <span class="mt-1.5 w-2 h-2 rounded-full shrink-0" [class]="n.isRead ? 'bg-transparent' : 'bg-primary'"></span>
              <span class="min-w-0 flex-1">
                <span class="block text-sm font-medium text-slate-800">{{ n.title }}</span>
                @if (n.message) { <span class="block text-xs text-slate-500">{{ n.message }}</span> }
                <span class="block text-[11px] text-slate-400 mt-0.5">{{ n.createdAt | date: 'dd MMM yyyy, HH:mm' }}</span>
              </span>
            </button>
          }
        </div>
      }
    </div>
  `,
})
export class NotificationsPageComponent implements OnInit {
  private readonly svc = inject(NotificationService);
  private readonly router = inject(Router);

  readonly items = signal<AppNotification[]>([]);
  readonly loading = signal(true);
  readonly filter = signal<'all' | 'unread'>('all');

  readonly unread = computed(() => this.items().filter((n) => !n.isRead).length);
  readonly visible = computed(() => this.filter() === 'unread' ? this.items().filter((n) => !n.isRead) : this.items());

  ngOnInit(): void {
    this.svc.load(50).subscribe({
      next: (list) => { this.items.set(list); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  open(n: AppNotification): void {
    if (!n.isRead) {
      this.items.set(this.items().map((x) => (x.id === n.id ? { ...x, isRead: true } : x)));
      this.svc.markRead(n.id);   // keeps the header bell count in sync
    }
    if (n.linkUrl) this.router.navigateByUrl(n.linkUrl);
  }

  markAllRead(): void {
    this.items.set(this.items().map((n) => ({ ...n, isRead: true })));
    this.svc.markAllRead();
  }
}
