import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-forgot-password',
  imports: [FormsModule, RouterLink],
  template: `
    <section class="page-container py-12 flex justify-center">
      <div class="w-full max-w-sm">
        <h1 class="text-xl font-bold text-slate-900">Reset your password</h1>
        <p class="text-sm text-slate-500 mt-1 mb-6">
          @if (step() === 'request') { Enter your email and we'll send you a reset code. }
          @else { Enter the code we emailed you and choose a new password. }
        </p>

        @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
        @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

        @if (step() === 'request') {
          <form (ngSubmit)="requestCode()" class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-slate-700 mb-1">Email</label>
              <input type="email" name="email" [(ngModel)]="email" required autocomplete="email"
                class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary focus:border-primary"
                placeholder="you@example.com" />
            </div>
            <button type="submit" [disabled]="submitting() || !email.trim()"
              class="w-full bg-primary hover:bg-primary-dark disabled:opacity-60 text-white font-medium py-2.5 rounded-lg transition">
              {{ submitting() ? 'Sending…' : 'Send reset code' }}
            </button>
          </form>
        } @else {
          <form (ngSubmit)="reset()" class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-slate-700 mb-1">Reset code</label>
              <input name="code" [(ngModel)]="code" required inputmode="numeric" autocomplete="one-time-code"
                class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm tracking-widest focus:outline-none focus:ring-2 focus:ring-primary focus:border-primary"
                placeholder="6-digit code" />
            </div>
            <div>
              <label class="block text-sm font-medium text-slate-700 mb-1">New password</label>
              <input type="password" name="newPassword" [(ngModel)]="newPassword" required autocomplete="new-password"
                class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary focus:border-primary"
                placeholder="At least 6 characters" />
            </div>
            <button type="submit" [disabled]="submitting() || !code.trim() || newPassword.length < 6"
              class="w-full bg-primary hover:bg-primary-dark disabled:opacity-60 text-white font-medium py-2.5 rounded-lg transition">
              {{ submitting() ? 'Updating…' : 'Update password' }}
            </button>
            <button type="button" (click)="requestCode()" [disabled]="submitting()" class="w-full text-sm text-slate-500 hover:text-slate-700">Resend code</button>
          </form>
        }

        <p class="text-sm text-slate-500 mt-6 text-center">
          <a routerLink="/login" class="text-primary hover:underline">Back to sign in</a>
        </p>
      </div>
    </section>
  `,
})
export class ForgotPasswordComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly step = signal<'request' | 'reset'>('request');
  readonly submitting = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  email = '';
  code = '';
  newPassword = '';

  requestCode(): void {
    if (!this.email.trim()) return;
    this.submitting.set(true);
    this.error.set(null);
    this.message.set(null);
    this.auth.forgotPassword(this.email.trim()).subscribe({
      next: () => {
        this.submitting.set(false);
        this.step.set('reset');
        this.message.set('If an account exists for that email, a reset code is on its way.');
      },
      error: () => { this.submitting.set(false); this.error.set('Something went wrong. Please try again.'); },
    });
  }

  reset(): void {
    if (!this.code.trim() || this.newPassword.length < 6) return;
    this.submitting.set(true);
    this.error.set(null);
    this.message.set(null);
    this.auth.resetPassword(this.email.trim(), this.code.trim(), this.newPassword).subscribe({
      next: () => { this.submitting.set(false); this.router.navigate(['/login'], { queryParams: { reset: 1 } }); },
      error: (e) => { this.submitting.set(false); this.error.set(e?.error?.message ?? 'Could not reset password.'); },
    });
  }
}
