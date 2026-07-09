import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { StaffAdminService } from '../../../core/services/staff-admin.service';
import { InviteStaffRequest, StaffMember, StaffRoleInfo } from '../../../core/models/staff.model';

@Component({
  selector: 'app-admin-staff',
  imports: [FormsModule, DatePipe],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <div class="flex items-center justify-between mb-1">
        <h1 class="text-xl font-bold text-slate-900">Staff & permissions</h1>
        <button type="button" (click)="inviting.set(!inviting())" class="btn-primary">+ Add staff</button>
      </div>
      <p class="text-sm text-slate-500 mb-4">People who can sign in to your store admin, and what they can do.</p>

      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      <!-- Invite -->
      @if (inviting()) {
        <div class="bg-white border border-slate-200 rounded-xl p-5 mb-5">
          <h2 class="font-semibold text-slate-800 mb-3">Add a staff member</h2>
          <div class="grid sm:grid-cols-2 gap-4">
            <div><label class="lbl">Full name</label><input class="input" [(ngModel)]="form.fullName" name="n" /></div>
            <div><label class="lbl">Email</label><input class="input" type="email" [(ngModel)]="form.email" name="e" placeholder="teammate@email.com" /></div>
            <div>
              <label class="lbl">Role</label>
              <select class="input" [(ngModel)]="form.accessLevel" name="r">
                @for (r of assignableRoles(); track r.key) { <option [value]="r.key">{{ r.label }}</option> }
              </select>
            </div>
            <div><label class="lbl">Initial password</label><input class="input" type="text" [(ngModel)]="form.password" name="p" placeholder="≥ 8 characters" /></div>
          </div>
          <p class="text-xs text-slate-400 mt-2">Share this password with your teammate — they can change it after signing in. (Email invites come later.)</p>
          <div class="flex gap-3 mt-4">
            <button type="button" (click)="invite()" [disabled]="saving()" class="btn-primary">{{ saving() ? 'Adding…' : 'Add staff' }}</button>
            <button type="button" (click)="inviting.set(false)" class="btn-ghost">Cancel</button>
          </div>
        </div>
      }

      <!-- Staff table -->
      <div class="bg-white border border-slate-200 rounded-xl overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-slate-50 text-slate-500 text-left">
            <tr>
              <th class="px-4 py-2 font-medium">Member</th>
              <th class="px-4 py-2 font-medium">Role</th>
              <th class="px-4 py-2 font-medium">Last sign-in</th>
              <th class="px-4 py-2 font-medium text-right">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-100">
            @for (s of staff(); track s.userId) {
              <tr [class.opacity-50]="s.status === 'Disabled'">
                <td class="px-4 py-3">
                  <div class="font-medium text-slate-800">{{ s.fullName || 'No name' }} @if (s.isYou) { <span class="text-xs text-slate-400">(you)</span> }</div>
                  <div class="text-xs text-slate-400">{{ s.email }}</div>
                  @if (s.status === 'Disabled') { <span class="text-xs text-red-500">Disabled</span> }
                </td>
                <td class="px-4 py-3">
                  @if (s.accessLevel === 'Owner' || s.isYou) {
                    <span class="text-sm text-slate-700">{{ roleLabel(s.accessLevel) }}</span>
                  } @else {
                    <select class="input py-1 text-sm" [ngModel]="s.accessLevel" (ngModelChange)="changeRole(s, $event)">
                      @for (r of assignableRoles(); track r.key) { <option [value]="r.key">{{ r.label }}</option> }
                    </select>
                  }
                </td>
                <td class="px-4 py-3 text-slate-500">{{ s.lastLoginAt ? (s.lastLoginAt | date:'medium') : 'Never' }}</td>
                <td class="px-4 py-3 text-right">
                  @if (s.accessLevel !== 'Owner' && !s.isYou) {
                    <div class="flex gap-3 justify-end text-sm">
                      @if (s.status === 'Active') {
                        <button type="button" (click)="setStatus(s, 'Disabled')" class="text-slate-500 hover:underline">Disable</button>
                      } @else {
                        <button type="button" (click)="setStatus(s, 'Active')" class="text-green-600 hover:underline">Enable</button>
                      }
                      <button type="button" (click)="remove(s)" class="text-red-500 hover:underline">Remove</button>
                    </div>
                  } @else { <span class="text-xs text-slate-300">—</span> }
                </td>
              </tr>
            }
          </tbody>
        </table>
        @if (loading()) { <div class="p-6 text-center text-slate-400 text-sm">Loading…</div> }
      </div>

      <!-- Role legend -->
      <div class="mt-5 text-xs text-slate-500 space-y-1">
        @for (r of roles(); track r.key) { <div><span class="font-medium text-slate-700">{{ r.label }}:</span> {{ r.description }}</div> }
      </div>
    </div>
  `,
})
export class AdminStaffComponent implements OnInit {
  private readonly api = inject(StaffAdminService);

  readonly staff = signal<StaffMember[]>([]);
  readonly roles = signal<StaffRoleInfo[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly inviting = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  form: InviteStaffRequest = { fullName: '', email: '', accessLevel: 'Staff', password: '' };

  assignableRoles(): StaffRoleInfo[] { return this.roles().filter((r) => r.key !== 'Owner'); }
  roleLabel(key: string): string { return this.roles().find((r) => r.key === key)?.label ?? key; }

  ngOnInit(): void {
    this.api.roles().subscribe((r) => this.roles.set(r));
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.list().subscribe({
      next: (s) => { this.staff.set(s); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  private toast(m: string): void { this.message.set(m); this.error.set(null); setTimeout(() => this.message.set(null), 2500); }
  private fail(e: unknown): void { this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Something went wrong.'); }

  invite(): void {
    this.saving.set(true);
    this.error.set(null);
    this.api.invite(this.form).subscribe({
      next: () => { this.saving.set(false); this.inviting.set(false); this.form = { fullName: '', email: '', accessLevel: 'Staff', password: '' }; this.toast('Staff member added.'); this.load(); },
      error: (e: unknown) => { this.saving.set(false); this.fail(e); },
    });
  }

  changeRole(s: StaffMember, level: string): void {
    if (level === s.accessLevel) return;
    this.api.updateRole(s.userId, level).subscribe({ next: () => { this.toast('Role updated.'); this.load(); }, error: (e: unknown) => { this.fail(e); this.load(); } });
  }

  setStatus(s: StaffMember, status: string): void {
    this.api.setStatus(s.userId, status).subscribe({ next: () => { this.toast('Status updated.'); this.load(); }, error: (e: unknown) => this.fail(e) });
  }

  remove(s: StaffMember): void {
    if (!confirm(`Remove admin access for ${s.fullName || s.email}?`)) return;
    this.api.remove(s.userId).subscribe({ next: () => { this.toast('Access removed.'); this.load(); }, error: (e: unknown) => this.fail(e) });
  }
}
