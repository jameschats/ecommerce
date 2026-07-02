import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AppNotification } from '../../core/models/notification.model';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-notification-bell',
  imports: [DatePipe],
  template: `
    <div class="relative">
      <button type="button" (click)="toggle()" aria-label="Notifications"
        class="relative flex items-center text-slate-600 hover:text-primary">
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M18 8a6 6 0 0 0-12 0c0 7-3 9-3 9h18s-3-2-3-9" />
          <path d="M13.7 21a2 2 0 0 1-3.4 0" />
        </svg>
        @if (svc.hasUnread()) {
          <span class="absolute -top-2 -right-2 min-w-[18px] h-[18px] px-1 rounded-full bg-red-500 text-white text-[10px] font-bold flex items-center justify-center">
            {{ svc.unreadCount() > 9 ? '9+' : svc.unreadCount() }}
          </span>
        }
      </button>

      @if (open()) {
        <div class="fixed inset-0 z-40" (click)="open.set(false)"></div>
        <div class="absolute right-0 mt-2 w-80 max-w-[90vw] bg-white rounded-xl border border-slate-200 shadow-lg z-50 overflow-hidden">
          <div class="flex items-center justify-between px-4 py-2.5 border-b border-slate-100">
            <span class="font-semibold text-slate-800 text-sm">Notifications</span>
            @if (svc.hasUnread()) {
              <button type="button" (click)="svc.markAllRead()" class="text-xs text-primary hover:underline">Mark all read</button>
            }
          </div>
          <div class="max-h-96 overflow-y-auto">
            @if (loading()) {
              <p class="px-4 py-6 text-center text-sm text-slate-400">Loading…</p>
            } @else if (!svc.notifications().length) {
              <p class="px-4 py-8 text-center text-sm text-slate-400">You're all caught up.</p>
            } @else {
              @for (n of svc.notifications(); track n.id) {
                <button type="button" (click)="go(n)"
                  class="w-full text-left px-4 py-3 border-b border-slate-50 hover:bg-slate-50 flex gap-2.5"
                  [class.bg-primary/5]="!n.isRead">
                  <span class="mt-1.5 w-2 h-2 rounded-full shrink-0" [class]="n.isRead ? 'bg-transparent' : 'bg-primary'"></span>
                  <span class="min-w-0">
                    <span class="block text-sm font-medium text-slate-800">{{ n.title }}</span>
                    @if (n.message) { <span class="block text-xs text-slate-500 truncate">{{ n.message }}</span> }
                    <span class="block text-[11px] text-slate-400 mt-0.5">{{ n.createdAt | date: 'dd MMM, HH:mm' }}</span>
                  </span>
                </button>
              }
            }
          </div>
        </div>
      }
    </div>
  `,
})
export class NotificationBellComponent {
  readonly svc = inject(NotificationService);
  private readonly router = inject(Router);

  readonly open = signal(false);
  readonly loading = signal(false);

  toggle(): void {
    const next = !this.open();
    this.open.set(next);
    if (next) this.refresh();
  }

  private refresh(): void {
    this.loading.set(true);
    this.svc.load().subscribe({
      next: (list) => { this.svc.notifications.set(list); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  go(n: AppNotification): void {
    if (!n.isRead) this.svc.markRead(n.id);
    this.open.set(false);
    if (n.linkUrl) this.router.navigateByUrl(n.linkUrl);
  }
}
