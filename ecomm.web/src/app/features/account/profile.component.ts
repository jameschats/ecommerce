import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccountService } from '../../core/services/account.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-profile',
  imports: [FormsModule],
  template: `
    <div class="bg-white rounded-xl border border-slate-200 p-6 max-w-lg">
      <h2 class="font-semibold text-slate-800 mb-4">Profile details</h2>
      @if (loading()) {
        <p class="text-slate-400 text-sm">Loading…</p>
      } @else {
        <form (ngSubmit)="save()" class="space-y-4">
          <div>
            <label class="lbl">Email</label>
            <input class="input bg-slate-50" [value]="email() || '—'" disabled />
          </div>
          <div>
            <label class="lbl">Full name</label>
            <input class="input" [(ngModel)]="fullName" name="fullName" placeholder="Your name" />
          </div>
          <div>
            <label class="lbl">Phone number</label>
            <input class="input" [(ngModel)]="phoneNumber" name="phoneNumber" placeholder="10-digit mobile" />
          </div>
          <div class="flex items-center gap-3 pt-1">
            <button type="submit" [disabled]="saving()" class="btn-primary px-5 py-2.5">{{ saving() ? 'Saving…' : 'Save changes' }}</button>
            @if (saved()) { <span class="text-sm text-green-600">✓ Saved</span> }
          </div>
        </form>
      }
    </div>
  `,
})
export class ProfileComponent implements OnInit {
  private readonly account = inject(AccountService);
  private readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly email = signal<string | null>(null);
  fullName = '';
  phoneNumber = '';

  ngOnInit(): void {
    this.account.getProfile().subscribe({
      next: (p) => {
        this.email.set(p.email);
        this.fullName = p.fullName ?? '';
        this.phoneNumber = p.phoneNumber ?? '';
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  save(): void {
    this.saving.set(true);
    this.saved.set(false);
    this.account.updateProfile({ fullName: this.fullName.trim(), phoneNumber: this.phoneNumber.trim() || null }).subscribe({
      next: (p) => {
        this.saving.set(false);
        this.saved.set(true);
        setTimeout(() => this.saved.set(false), 2500);
        // keep the header name in sync
        const u = this.auth.currentUser();
        if (u) this.auth.currentUser.set({ ...u, fullName: p.fullName, phoneNumber: p.phoneNumber });
      },
      error: () => this.saving.set(false),
    });
  }
}
