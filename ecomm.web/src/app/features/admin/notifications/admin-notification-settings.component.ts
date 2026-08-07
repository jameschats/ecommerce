import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { API_BASE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';

interface NotificationSwitch {
  event: string;
  label: string;
  description: string;
  recipient: string;
  email: boolean;
  sms: boolean;
  hasSms: boolean;
}

/**
 * Which automatic messages go out.
 *
 * Deliberately not the same screen as message templates: that one is about wording, this is
 * about whether anything is sent at all. Until now the order emails could not be stopped by
 * anything short of a deploy.
 */
@Component({
  selector: 'app-admin-notification-settings',
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Notifications</h1>
      <p class="text-sm text-slate-500 mb-5">
        What the shop sends automatically. Switch anything off and it stops going out immediately.
      </p>

      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      @if (loading()) {
        <div class="p-10 text-center text-slate-400">Loading…</div>
      } @else {
        <div class="bg-white border border-slate-200 rounded-xl overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="text-left text-slate-400 border-b border-slate-100">
              <tr>
                <th class="px-4 py-2">Message</th>
                <th class="px-2 py-2">Goes to</th>
                <th class="px-2 py-2 text-center">Email</th>
                <th class="px-2 py-2 text-center">SMS</th>
              </tr>
            </thead>
            <tbody>
              @for (r of rows(); track r.event) {
                <tr class="border-b border-slate-50">
                  <td class="px-4 py-2">
                    <div class="text-slate-800 font-medium">{{ r.label }}</div>
                    <div class="text-xs text-slate-500">{{ r.description }}</div>
                  </td>
                  <td class="px-2 py-2 text-slate-600">{{ r.recipient }}</td>
                  <td class="px-2 py-2 text-center">
                    <input type="checkbox" class="w-4 h-4" [checked]="r.email"
                           (change)="set(r, 'Email', $any($event.target).checked)" />
                  </td>
                  <td class="px-2 py-2 text-center">
                    @if (r.hasSms) {
                      <input type="checkbox" class="w-4 h-4" [checked]="r.sms"
                             (change)="set(r, 'SMS', $any($event.target).checked)" />
                    } @else {
                      <span class="text-slate-300">—</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <div class="mt-4 rounded-xl bg-slate-50 border border-slate-200 px-4 py-3 text-[13px] text-slate-600">
          <strong class="text-slate-800">Sign-in and account emails are not listed here, on purpose.</strong>
          One-time codes, password resets and email verification always send. A switch that turns
          those off is a switch that locks every customer out of their own account, and the
          symptom looks like a broken site rather than a setting somebody changed.
        </div>

        <p class="text-xs text-slate-400 mt-3">
          SMS is currently in test mode, so nothing actually leaves regardless of these switches.
          Wording lives in Settings → Message templates.
        </p>
      }
    </div>
  `,
})
export class AdminNotificationSettingsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/notification-settings`;

  readonly rows = signal<NotificationSwitch[]>([]);
  readonly loading = signal(true);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  ngOnInit(): void { this.load(); }

  private load(): void {
    this.loading.set(true);
    this.http.get<ApiResponse<NotificationSwitch[]>>(this.base).subscribe({
      next: (r) => { this.rows.set(r.data ?? []); this.loading.set(false); },
      error: () => { this.loading.set(false); this.error.set('Could not load notification settings.'); },
    });
  }

  set(row: NotificationSwitch, channel: 'Email' | 'SMS', enabled: boolean): void {
    this.error.set(null);
    this.http.put<ApiResponse<NotificationSwitch[]>>(this.base, { event: row.event, channel, enabled })
      .subscribe({
        next: (r) => {
          // Re-seated from the server rather than trusting the click: if the write was refused
          // the checkbox must go back to what is actually stored.
          this.rows.set(r.data ?? this.rows());
          this.message.set(r.message ?? 'Saved.');
          setTimeout(() => this.message.set(null), 2000);
        },
        error: (e) => {
          this.error.set(e?.error?.message ?? 'Could not save that change.');
          this.load();
        },
      });
  }
}
