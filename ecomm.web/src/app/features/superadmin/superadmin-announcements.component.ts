import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SuperAdminService } from '../../core/services/superadmin.service';
import { Announcement } from '../../core/models/superadmin.model';

/** Author platform-wide announcements shown to every merchant. */
@Component({
  selector: 'app-superadmin-announcements',
  imports: [FormsModule, DatePipe],
  template: `
    <h1 class="text-xl font-bold text-slate-900 mb-1">Announcements</h1>
    <p class="text-sm text-slate-500 mb-4">Broadcast a message to every merchant's admin.</p>

    @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
    @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

    <div class="bg-white border border-slate-200 rounded-xl p-4 max-w-2xl mb-6">
      <div class="text-sm font-medium text-slate-700 mb-2">New announcement</div>
      <div class="grid gap-2">
        <input [(ngModel)]="form.title" placeholder="Title" class="input" />
        <textarea [(ngModel)]="form.body" rows="2" placeholder="Message" class="input"></textarea>
        <div class="grid sm:grid-cols-3 gap-2">
          <select [(ngModel)]="form.level" class="input"><option value="info">Info</option><option value="warning">Warning</option><option value="critical">Critical</option></select>
          <label class="block"><span class="lbl">Starts (optional)</span><input type="date" [(ngModel)]="startsAt" class="input" /></label>
          <label class="block"><span class="lbl">Ends (optional)</span><input type="date" [(ngModel)]="endsAt" class="input" /></label>
        </div>
      </div>
      <button type="button" (click)="post()" class="btn-primary text-xs mt-2">Post announcement</button>
    </div>

    <div class="bg-white border border-slate-200 rounded-xl p-4 max-w-2xl">
      <div class="text-sm font-medium text-slate-700 mb-2">All announcements</div>
      @for (a of items(); track a.id) {
        <div class="border-t border-slate-100 py-2">
          <div class="flex items-center justify-between">
            <span class="font-medium text-slate-800">{{ a.title }} <span class="text-xs px-1.5 py-0.5 rounded" [class]="levelClass(a.level)">{{ a.level }}</span>@if (!a.isActive) { <span class="text-xs text-slate-400 ml-1">hidden</span> }</span>
            @if (a.isActive) { <button type="button" (click)="setActive(a.id, false)" class="text-red-500 hover:underline text-xs">Hide</button> }
            @else { <button type="button" (click)="setActive(a.id, true)" class="text-green-600 hover:underline text-xs">Show</button> }
          </div>
          <div class="text-sm text-slate-600">{{ a.body }}</div>
          <div class="text-[11px] text-slate-400">{{ a.createdAt | date:'medium' }}@if (a.endsAt) { · until {{ a.endsAt | date:'mediumDate' }} }</div>
        </div>
      }
      @if (!items().length) { <div class="text-slate-400 text-sm text-center py-6">No announcements.</div> }
    </div>
  `,
})
export class SuperAdminAnnouncementsComponent implements OnInit {
  private readonly svc = inject(SuperAdminService);
  readonly items = signal<Announcement[]>([]);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  form = { title: '', body: '', level: 'info' };
  startsAt = '';
  endsAt = '';

  ngOnInit(): void { this.load(); }
  load(): void { this.svc.announcements().subscribe((a) => this.items.set(a)); }

  post(): void {
    if (!this.form.title.trim() || !this.form.body.trim()) { this.error.set('Title and message are required.'); return; }
    this.svc.createAnnouncement({
      title: this.form.title.trim(), body: this.form.body.trim(), level: this.form.level,
      startsAt: this.startsAt ? new Date(this.startsAt).toISOString() : null,
      endsAt: this.endsAt ? new Date(this.endsAt).toISOString() : null,
    }).subscribe({
      next: () => { this.form = { title: '', body: '', level: 'info' }; this.startsAt = ''; this.endsAt = ''; this.error.set(null); this.toast('Announcement posted.'); this.load(); },
      error: (e) => this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Could not post.'),
    });
  }
  setActive(id: number, active: boolean): void { this.svc.setAnnouncementActive(id, active).subscribe(() => { this.toast('Updated.'); this.load(); }); }

  levelClass(l: string): string {
    return l === 'critical' ? 'bg-red-50 text-red-700 border border-red-200'
      : l === 'warning' ? 'bg-amber-50 text-amber-700 border border-amber-200'
      : 'bg-blue-50 text-blue-700 border border-blue-200';
  }
  private toast(m: string): void { this.message.set(m); this.error.set(null); setTimeout(() => this.message.set(null), 3000); }
}
