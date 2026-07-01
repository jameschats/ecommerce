import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccountService } from '../../core/services/account.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-profile',
  imports: [FormsModule],
  template: `
    @if (!loading() && email()) {
      <div class="max-w-lg mb-4">
        @if (emailVerified()) {
          <div class="flex items-center gap-2 text-sm text-green-700 bg-green-50 border border-green-200 rounded-lg px-3 py-2">
            <span>✓</span> Your email is verified.
          </div>
        } @else {
          <div class="bg-amber-50 border border-amber-200 rounded-lg px-4 py-3">
            <div class="flex items-center justify-between gap-3">
              <div class="text-sm text-amber-800">Your email isn't verified yet.</div>
              @if (!codeSent()) {
                <button type="button" (click)="requestVerify()" [disabled]="verifying()" class="text-sm text-primary hover:underline whitespace-nowrap">
                  {{ verifying() ? 'Sending…' : 'Verify email' }}
                </button>
              }
            </div>
            @if (codeSent()) {
              <div class="flex gap-2 mt-2">
                <input [(ngModel)]="verifyCode" name="vcode" placeholder="6-digit code" inputmode="numeric"
                  class="input flex-1 tracking-widest" />
                <button type="button" (click)="confirmVerify()" [disabled]="verifying() || !verifyCode.trim()" class="btn-primary text-sm px-4">Confirm</button>
              </div>
            }
            @if (verifyMsg()) { <p class="text-xs mt-2" [class]="verifyErr() ? 'text-red-600' : 'text-green-700'">{{ verifyMsg() }}</p> }
          </div>
        }
      </div>
    }

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
  readonly emailVerified = signal(false);
  fullName = '';
  phoneNumber = '';

  // email verification
  readonly verifying = signal(false);
  readonly codeSent = signal(false);
  readonly verifyMsg = signal<string | null>(null);
  readonly verifyErr = signal(false);
  verifyCode = '';

  ngOnInit(): void {
    this.account.getProfile().subscribe({
      next: (p) => {
        this.email.set(p.email);
        this.emailVerified.set(p.isEmailVerified);
        this.fullName = p.fullName ?? '';
        this.phoneNumber = p.phoneNumber ?? '';
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  requestVerify(): void {
    this.verifying.set(true);
    this.verifyMsg.set(null);
    this.verifyErr.set(false);
    this.auth.requestEmailVerification().subscribe({
      next: () => { this.verifying.set(false); this.codeSent.set(true); this.verifyMsg.set('Code sent — check your email.'); },
      error: (e) => { this.verifying.set(false); this.verifyErr.set(true); this.verifyMsg.set(e?.error?.message ?? 'Could not send code.'); },
    });
  }

  confirmVerify(): void {
    if (!this.verifyCode.trim()) return;
    this.verifying.set(true);
    this.verifyMsg.set(null);
    this.verifyErr.set(false);
    this.auth.confirmEmailVerification(this.verifyCode.trim()).subscribe({
      next: () => { this.verifying.set(false); this.emailVerified.set(true); this.codeSent.set(false); this.verifyCode = ''; },
      error: (e) => { this.verifying.set(false); this.verifyErr.set(true); this.verifyMsg.set(e?.error?.message ?? 'Invalid or expired code.'); },
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
