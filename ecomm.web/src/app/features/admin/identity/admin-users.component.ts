import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { API_BASE_URL } from '../../../core/api.config';
import { ApiResponse, PagedResult } from '../../../core/models/api-response.model';

interface PermissionDto { permissionId: number; code: string; name: string; module: string; }
interface RoleDto { roleId: number; name: string; description: string | null; isSystem: boolean; permissions: string[]; }
interface AccessMatrix { roles: RoleDto[]; permissions: PermissionDto[]; modulePages: Record<string, string>; }
interface AdminUser {
  userId: number; fullName: string | null; email: string | null; phoneNumber: string | null;
  isActive: boolean; createdAt: string; roles: string[];
}
/** userId null ⇒ creating. Password and roles are only asked for on create. */
interface StaffForm {
  userId: number | null; fullName: string; email: string;
  phoneNumber: string; password: string; roles: string[];
}

/**
 * Who has access, and what that access means.
 *
 * The matrix answers "who can do what" in the shape the question is asked — modules down
 * the side, roles across the top — rather than as a list of permission codes nobody outside
 * the code thinks in.
 */
@Component({
  selector: 'app-admin-users',
  imports: [FormsModule],
  template: `
    <div class="max-w-6xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Users &amp; roles</h1>
      <p class="text-sm text-slate-500 mb-5">Who can reach which part of the admin.</p>

      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      <div class="flex gap-1 p-1 mb-5 bg-slate-100 rounded-lg text-sm font-medium w-fit">
        @for (t of tabs; track t) {
          <button type="button" (click)="tab.set(t)" class="px-4 py-2 rounded-md transition"
                  [class]="tab() === t ? 'bg-white shadow-sm text-slate-900' : 'text-slate-500'">{{ t }}</button>
        }
      </div>

      @if (tab() === 'Access matrix') {
        @if (matrix(); as m) {
          <div class="bg-white border border-slate-200 rounded-xl overflow-x-auto">
            <table class="w-full text-sm">
              <thead class="text-left text-slate-400 border-b border-slate-100">
                <tr>
                  <th class="px-4 py-3">Module</th>
                  @for (r of m.roles; track r.roleId) {
                    <th class="px-3 py-3 text-center">
                      <div class="text-slate-700 font-semibold">{{ r.name }}</div>
                      @if (r.isSystem) { <div class="text-[10px] font-normal text-slate-400">built-in</div> }
                    </th>
                  }
                </tr>
              </thead>
              <tbody>
                @for (mod of modules(); track mod) {
                  <tr class="border-b border-slate-50">
                    <td class="px-4 py-2">
                      <div class="text-slate-800 font-medium">{{ mod }}</div>
                      @if (m.modulePages[mod]; as page) {
                        <div class="text-[11px] text-slate-400">{{ page }}</div>
                      }
                    </td>
                    @for (r of m.roles; track r.roleId) {
                      <td class="px-3 py-2 text-center">
                        <div class="flex gap-1.5 justify-center">
                          @for (p of permsFor(mod); track p.code) {
                            <!-- Built-in roles render read-only: Admin edited into a lockout
                                 has no way back short of the database, and Customer holds
                                 nothing on purpose. -->
                            <label class="flex flex-col items-center gap-0.5"
                                   [title]="p.name + (r.isSystem ? ' (built-in role)' : '')">
                              <input type="checkbox" class="w-4 h-4"
                                     [checked]="r.permissions.includes(p.code)"
                                     [disabled]="r.isSystem || saving()"
                                     (change)="toggle(r, p.code, $any($event.target).checked)" />
                              <span class="text-[10px] text-slate-400">{{ action(p.code) }}</span>
                            </label>
                          }
                        </div>
                      </td>
                    }
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <p class="text-xs text-slate-400 mt-2">
            Changes take effect when the person next signs in — permissions travel in their sign-in token.
          </p>
        } @else {
          <div class="p-10 text-center text-slate-400">Loading…</div>
        }
      } @else {
        <div class="flex items-center gap-2 mb-3">
          <input [(ngModel)]="search" (keyup.enter)="loadUsers()" placeholder="Search name, email, phone…" class="input max-w-xs" />
          <label class="flex items-center gap-2 text-sm text-slate-600">
            <input type="checkbox" [(ngModel)]="staffOnly" (ngModelChange)="loadUsers()" class="w-4 h-4" /> staff only
          </label>
          <button type="button" (click)="startCreate()"
                  class="ml-auto px-3 py-1.5 rounded-lg bg-slate-900 text-white text-sm font-medium hover:bg-slate-800">
            Add staff member
          </button>
        </div>

        @if (form(); as f) {
          <div class="mb-4 bg-white border border-slate-200 rounded-xl p-4">
            <h2 class="font-semibold text-slate-900 mb-3">{{ f.userId ? 'Edit ' + (f.fullName || 'staff member') : 'New staff member' }}</h2>
            <div class="grid gap-3 sm:grid-cols-2">
              <label class="text-sm">
                <span class="block text-slate-600 mb-1">Name</span>
                <input [(ngModel)]="f.fullName" class="input w-full" placeholder="Full name" />
              </label>
              <label class="text-sm">
                <span class="block text-slate-600 mb-1">Email <span class="text-red-500">*</span></span>
                <input [(ngModel)]="f.email" type="email" class="input w-full" placeholder="name@example.com" />
              </label>
              <label class="text-sm">
                <span class="block text-slate-600 mb-1">Mobile</span>
                <input [(ngModel)]="f.phoneNumber" class="input w-full" placeholder="Optional" />
              </label>
              @if (!f.userId) {
                <label class="text-sm">
                  <span class="block text-slate-600 mb-1">Initial password <span class="text-red-500">*</span></span>
                  <input [(ngModel)]="f.password" class="input w-full" placeholder="At least 6 characters" />
                  <span class="block text-[11px] text-slate-400 mt-1">Pass this on, then ask them to change it.</span>
                </label>
              }
            </div>

            @if (!f.userId) {
              <div class="mt-3">
                <span class="block text-sm text-slate-600 mb-1">Roles <span class="text-red-500">*</span></span>
                <div class="flex flex-wrap gap-3">
                  @for (r of assignableRoles(); track r.roleId) {
                    <label class="flex items-center gap-1.5 text-sm cursor-pointer">
                      <input type="checkbox" class="w-4 h-4" [checked]="f.roles.includes(r.name)"
                             (change)="toggleFormRole(r.name, $any($event.target).checked)" />
                      {{ r.name }}
                    </label>
                  }
                </div>
              </div>
            }

            <div class="flex items-center gap-2 mt-4">
              <button type="button" (click)="save()" [disabled]="saving()"
                      class="px-4 py-2 rounded-lg bg-slate-900 text-white text-sm font-medium disabled:opacity-50">
                {{ saving() ? 'Saving…' : 'Save' }}
              </button>
              <button type="button" (click)="form.set(null)" class="px-4 py-2 rounded-lg border border-slate-300 text-sm">Cancel</button>
            </div>
          </div>
        }

        @if (users().length) {
          <div class="bg-white border border-slate-200 rounded-xl overflow-x-auto">
            <table class="w-full text-sm">
              <thead class="text-left text-slate-400 border-b border-slate-100">
                <tr>
                  <th class="px-4 py-2">Person</th>
                  <th class="px-4 py-2">Roles</th>
                  <th class="px-4 py-2 text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                @for (u of users(); track u.userId) {
                  <tr class="border-b border-slate-50" [class.opacity-60]="!u.isActive">
                    <td class="px-4 py-2">
                      <div class="text-slate-800">
                        {{ u.fullName || 'Unnamed' }}
                        @if (!u.isActive) { <span class="ml-1 text-[10px] text-amber-700 bg-amber-50 border border-amber-200 rounded px-1">switched off</span> }
                      </div>
                      <div class="text-xs text-slate-500">{{ u.email || u.phoneNumber || '—' }}</div>
                    </td>
                    <td class="px-4 py-2">
                      <div class="flex flex-wrap gap-2">
                        @for (r of allRoles(); track r.roleId) {
                          <label class="flex items-center gap-1.5 text-sm cursor-pointer">
                            <input type="checkbox" class="w-4 h-4"
                                   [checked]="u.roles.includes(r.name)"
                                   (change)="toggleRole(u, r.name, $any($event.target).checked)" />
                            {{ r.name }}
                          </label>
                        }
                      </div>
                    </td>
                    <td class="px-4 py-2 text-right whitespace-nowrap">
                      <!-- Customers are listed and their roles are assignable, but their own
                           details are theirs to change — so the actions only apply to staff. -->
                      @if (isStaff(u)) {
                        <button type="button" (click)="startEdit(u)" class="text-blue-600 hover:underline">Edit</button>
                        <span class="text-slate-300 mx-1.5">·</span>
                        <button type="button" (click)="sendReset(u)" class="text-blue-600 hover:underline">Send reset</button>
                        <span class="text-slate-300 mx-1.5">·</span>
                        <button type="button" (click)="setActive(u, !u.isActive)" class="text-blue-600 hover:underline">
                          {{ u.isActive ? 'Switch off' : 'Switch on' }}
                        </button>
                        <span class="text-slate-300 mx-1.5">·</span>
                        <button type="button" (click)="remove(u)" class="text-red-600 hover:underline">Delete</button>
                      } @else {
                        <span class="text-xs text-slate-400">customer</span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        } @else {
          <div class="bg-white border border-slate-200 rounded-xl p-12 text-center text-slate-500">No one found.</div>
        }
      }
    </div>
  `,
})
export class AdminUsersComponent implements OnInit {
  private readonly http = inject(HttpClient);

  readonly tabs = ['Access matrix', 'People'];
  readonly tab = signal('Access matrix');

  readonly matrix = signal<AccessMatrix | null>(null);
  readonly users = signal<AdminUser[]>([]);
  readonly form = signal<StaffForm | null>(null);
  readonly saving = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  search = '';
  staffOnly = true;

  readonly allRoles = computed(() => this.matrix()?.roles ?? []);

  /** Modules in the order the permissions come back, which is alphabetical by module. */
  readonly modules = computed(() => [...new Set((this.matrix()?.permissions ?? []).map((p) => p.module))]);

  permsFor(module: string): PermissionDto[] {
    return (this.matrix()?.permissions ?? []).filter((p) => p.module === module);
  }

  /** "catalog.manage" → "manage" — the column header already says which module. */
  action(code: string): string { return code.split('.')[1] ?? code; }

  ngOnInit(): void {
    this.loadMatrix();
    this.loadUsers();
  }

  private loadMatrix(): void {
    this.http.get<ApiResponse<AccessMatrix>>(`${API_BASE_URL}/admin/roles`).subscribe({
      next: (r) => this.matrix.set(r.data ?? null),
      error: () => this.error.set('Could not load the access matrix.'),
    });
  }

  loadUsers(): void {
    const url = `${API_BASE_URL}/admin/users?staffOnly=${this.staffOnly}&search=${encodeURIComponent(this.search)}`;
    this.http.get<ApiResponse<PagedResult<AdminUser>>>(url).subscribe({
      next: (r) => this.users.set(r.data?.items ?? []),
      error: () => this.error.set('Could not load users.'),
    });
  }

  toggle(role: RoleDto, code: string, checked: boolean): void {
    const next = checked
      ? [...role.permissions, code]
      : role.permissions.filter((p) => p !== code);
    role.permissions = next;

    this.saving.set(true);
    this.error.set(null);
    this.http.put<ApiResponse<unknown>>(`${API_BASE_URL}/admin/roles/${role.roleId}/permissions`, { permissions: next })
      .subscribe({
        next: (r) => { this.saving.set(false); this.flash(r.message ?? 'Access updated.'); },
        error: (e) => {
          this.saving.set(false);
          this.error.set(e?.error?.message ?? 'Could not update access.');
          this.loadMatrix();   // put the checkbox back where the server says it belongs
        },
      });
  }

  // --- Create / edit / retire ---

  /** Everything except Customer: this form exists to make someone staff. */
  readonly assignableRoles = computed(() => this.allRoles().filter((r) => r.name !== 'Customer'));

  /**
   * "Not a customer" rather than "has a staff role" — an account stripped of every role is a
   * staff member, and hiding its actions would strand it with no way to edit or remove it.
   * Mirrors LoadStaffAsync on the server.
   */
  isStaff(u: AdminUser): boolean {
    return !(u.roles.length > 0 && u.roles.every((r) => r === 'Customer'));
  }

  startCreate(): void {
    this.error.set(null);
    this.form.set({ userId: null, fullName: '', email: '', phoneNumber: '', password: '', roles: [] });
  }

  startEdit(u: AdminUser): void {
    this.error.set(null);
    this.form.set({
      userId: u.userId, fullName: u.fullName ?? '', email: u.email ?? '',
      phoneNumber: u.phoneNumber ?? '', password: '', roles: [...u.roles],
    });
  }

  toggleFormRole(name: string, checked: boolean): void {
    const f = this.form();
    if (!f) return;
    f.roles = checked ? [...f.roles, name] : f.roles.filter((r) => r !== name);
  }

  save(): void {
    const f = this.form();
    if (!f) return;

    this.saving.set(true);
    this.error.set(null);

    const done = (msg: string) => {
      this.saving.set(false);
      this.form.set(null);
      this.flash(msg);
      this.loadUsers();
    };
    const failed = (e: unknown) => {
      this.saving.set(false);
      this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Could not save.');
    };

    if (f.userId) {
      const body = { fullName: f.fullName, email: f.email, phoneNumber: f.phoneNumber };
      this.http.put<ApiResponse<unknown>>(`${API_BASE_URL}/admin/users/${f.userId}`, body)
        .subscribe({ next: (r) => done(r.message ?? 'Saved.'), error: failed });
    } else {
      this.http.post<ApiResponse<unknown>>(`${API_BASE_URL}/admin/users`, f)
        .subscribe({ next: (r) => done(r.message ?? 'Staff member added.'), error: failed });
    }
  }

  setActive(u: AdminUser, isActive: boolean): void {
    // Honest about the window: their sign-in token is refused immediately and they cannot sign
    // in again, but an access token already in their browser stays valid until it expires.
    if (!isActive && !confirm(
      `Switch off ${u.fullName || u.email}? They will not be able to sign in again, `
      + `and any session they have open stops working within the hour.`)) return;

    this.error.set(null);
    this.http.put<ApiResponse<unknown>>(`${API_BASE_URL}/admin/users/${u.userId}/active`, { isActive })
      .subscribe({
        next: (r) => { this.flash(r.message ?? 'Updated.'); this.loadUsers(); },
        error: (e) => this.error.set(e?.error?.message ?? 'Could not update the account.'),
      });
  }

  remove(u: AdminUser): void {
    if (!confirm(`Delete ${u.fullName || u.email}? Their orders are kept, and their email and mobile become free to use again.`)) return;

    this.error.set(null);
    this.http.delete<ApiResponse<unknown>>(`${API_BASE_URL}/admin/users/${u.userId}`)
      .subscribe({
        next: (r) => { this.flash(r.message ?? 'Deleted.'); this.loadUsers(); },
        error: (e) => this.error.set(e?.error?.message ?? 'Could not delete the account.'),
      });
  }

  sendReset(u: AdminUser): void {
    this.error.set(null);
    this.http.post<ApiResponse<unknown>>(`${API_BASE_URL}/admin/users/${u.userId}/send-reset`, {})
      .subscribe({
        next: (r) => this.flash(r.message ?? 'Reset link sent.'),
        error: (e) => this.error.set(e?.error?.message ?? 'Could not send the reset link.'),
      });
  }

  toggleRole(user: AdminUser, roleName: string, checked: boolean): void {
    const next = checked ? [...user.roles, roleName] : user.roles.filter((r) => r !== roleName);
    user.roles = next;

    this.error.set(null);
    this.http.put<ApiResponse<unknown>>(`${API_BASE_URL}/admin/users/${user.userId}/roles`, { roles: next })
      .subscribe({
        next: (r) => this.flash(r.message ?? 'Roles updated.'),
        error: (e) => {
          this.error.set(e?.error?.message ?? 'Could not update roles.');
          this.loadUsers();
        },
      });
  }

  private flash(msg: string): void {
    this.message.set(msg);
    setTimeout(() => this.message.set(null), 3000);
  }
}
