import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SuperAdminService } from '../../core/services/superadmin.service';
import { PlatformStaff } from '../../core/models/superadmin.model';

/** Platform operators — invite/list/deactivate super-admin users. Flat model (all are full super-admins). */
@Component({
  selector: 'app-superadmin-staff',
  imports: [FormsModule, DatePipe],
  template: `
    <h1 class="text-xl font-bold text-slate-900 mb-1">Platform staff</h1>
    <p class="text-sm text-slate-500 mb-4">Everyone with full platform-admin access. Invited users can sign in at the platform login.</p>

    @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
    @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

    <div class="bg-white border border-slate-200 rounded-xl p-4 max-w-2xl">
      <table class="w-full text-sm mb-4">
        <thead class="text-left text-slate-400 border-b border-slate-200"><tr><th class="py-1">Name</th><th>Email</th><th>Last login</th><th></th></tr></thead>
        <tbody>
          @for (s of staff(); track s.userId) {
            <tr class="border-b border-slate-100">
              <td class="py-2 text-slate-800">{{ s.fullName || '—' }}@if (!s.isActive) { <span class="text-xs text-red-500 ml-1">(revoked)</span> }</td>
              <td class="text-slate-500">{{ s.email }}</td>
              <td class="text-slate-400 text-xs">{{ s.lastLoginAt ? (s.lastLoginAt | date:'short') : 'never' }}</td>
              <td class="text-right">
                @if (s.isActive) { <button type="button" (click)="setActive(s.userId, false)" class="text-red-500 hover:underline text-xs">Revoke</button> }
                @else { <button type="button" (click)="setActive(s.userId, true)" class="text-green-600 hover:underline text-xs">Restore</button> }
              </td>
            </tr>
          }
        </tbody>
      </table>

      <div class="border-t border-slate-200 pt-3">
        <div class="text-sm font-medium text-slate-700 mb-2">Invite a platform admin</div>
        <div class="grid sm:grid-cols-3 gap-2">
          <input [(ngModel)]="email" placeholder="email" class="input" />
          <input [(ngModel)]="fullName" placeholder="name (optional)" class="input" />
          <input [(ngModel)]="password" type="password" placeholder="initial password (min 8)" class="input" />
        </div>
        <button type="button" (click)="create()" class="btn-primary text-xs mt-2">Add platform admin</button>
        <p class="text-[11px] text-slate-400 mt-1">They get full cross-tenant access. Granular roles (viewer/limited) are planned.</p>
      </div>
    </div>
  `,
})
export class SuperAdminStaffComponent implements OnInit {
  private readonly svc = inject(SuperAdminService);
  readonly staff = signal<PlatformStaff[]>([]);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  email = '';
  fullName = '';
  password = '';

  ngOnInit(): void { this.load(); }
  load(): void { this.svc.staff().subscribe((s) => this.staff.set(s)); }

  create(): void {
    if (!this.email.trim() || this.password.length < 8) { this.error.set('Email and an 8+ char password are required.'); return; }
    this.svc.createStaff(this.email.trim(), this.fullName.trim() || null, this.password).subscribe({
      next: () => { this.email = ''; this.fullName = ''; this.password = ''; this.error.set(null); this.toast('Staff member added.'); this.load(); },
      error: (e) => this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Could not add.'),
    });
  }
  setActive(userId: number, active: boolean): void {
    this.svc.setStaffActive(userId, active).subscribe({ next: () => { this.toast(active ? 'Access restored.' : 'Access revoked.'); this.load(); }, error: (e) => this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Failed.') });
  }

  private toast(m: string): void { this.message.set(m); this.error.set(null); setTimeout(() => this.message.set(null), 3000); }
}
