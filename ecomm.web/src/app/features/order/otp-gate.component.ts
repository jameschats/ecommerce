import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';

type Channel = 'email' | 'mobile';
type Step = 'identify' | 'code';

/**
 * The login gate, shown only when the buyer submits an order (design.md §7.2).
 *
 * Building the order is free; identity is required at the point it becomes a commitment.
 * Requiring a login before a dealer can even see prices would cost far more conversions
 * than it protects against — and by the time an order exists, so does an account, which
 * is what makes "log in to check my orders" work with no separate registration step.
 */
@Component({
  selector: 'app-otp-gate',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div class="fixed inset-0 bg-slate-900/50 z-50 grid place-items-center p-4" (click)="cancel()">
        <div class="bg-white rounded-2xl shadow-2xl w-full max-w-sm p-6" (click)="$event.stopPropagation()"
             role="dialog" aria-modal="true" aria-label="Verify to place your order">

          <h2 class="text-lg font-bold text-slate-900">Verify to place your order</h2>
          <p class="text-sm text-slate-500 mt-1">
            Your order is saved — this only confirms who you are.
          </p>

          @if (step() === 'identify') {
            <div class="mt-4 flex rounded-lg border border-slate-200 p-0.5 bg-slate-50">
              <button type="button" (click)="channel.set('email')"
                      [class]="channel() === 'email' ? 'tab-active' : 'tab-idle'">Email</button>
              <button type="button" (click)="channel.set('mobile')"
                      [class]="channel() === 'mobile' ? 'tab-active' : 'tab-idle'">Mobile</button>
            </div>

            @if (channel() === 'email') {
              <label class="block mt-4">
                <span class="form-label">Email address</span>
                <input type="email" [(ngModel)]="email" class="form-input" placeholder="you@example.com"
                       (keydown.enter)="requestCode()" />
              </label>
            } @else {
              <label class="block mt-4">
                <span class="form-label">Mobile number</span>
                <input type="tel" [ngModel]="mobile()" (ngModelChange)="mobile.set(sanitizeMobile($event))"
                       (blur)="normalizeMobile()" maxlength="14" inputmode="numeric" class="form-input"
                       placeholder="10 digits, +91 is fine" (keydown.enter)="requestCode()" />
              </label>
            }

            <button type="button" (click)="requestCode()" [disabled]="!canRequest() || busy()"
                    class="mt-4 w-full bg-primary hover:bg-primary-dark disabled:bg-slate-300
                           text-white font-semibold py-2.5 rounded-lg transition">
              {{ busy() ? 'Sending…' : 'Send verification code' }}
            </button>
          } @else {
            <p class="mt-4 text-sm text-slate-600">
              Code sent to <span class="font-medium text-slate-900">{{ identifier() }}</span>.
              <button type="button" (click)="step.set('identify')" class="text-primary hover:underline ml-1">Change</button>
            </p>

            <label class="block mt-3">
              <span class="form-label">6-digit code</span>
              <input type="text" [(ngModel)]="code" maxlength="6" inputmode="numeric" autocomplete="one-time-code"
                     class="form-input text-center text-lg tracking-[0.4em] font-semibold"
                     placeholder="______" (keydown.enter)="verify()" />
            </label>

            <button type="button" (click)="verify()" [disabled]="code().length !== 6 || busy()"
                    class="mt-4 w-full bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-300
                           text-white font-semibold py-2.5 rounded-lg transition">
              {{ busy() ? 'Verifying…' : 'Verify & place order' }}
            </button>
          }

          @if (error()) {
            <p class="mt-3 text-sm text-red-700 bg-red-50 border border-red-200 rounded px-3 py-2">{{ error() }}</p>
          }

          <button type="button" (click)="cancel()" class="mt-3 w-full text-sm text-slate-500 hover:text-slate-800">
            Cancel
          </button>
        </div>
      </div>
    }
  `,
  styles: [`
    .tab-active { flex: 1; padding: 0.4rem; border-radius: 0.4rem; background: #fff; font-size: 0.85rem;
                  font-weight: 600; color: rgb(15 23 42); box-shadow: 0 1px 2px rgb(0 0 0 / 0.08); }
    .tab-idle   { flex: 1; padding: 0.4rem; border-radius: 0.4rem; font-size: 0.85rem;
                  font-weight: 500; color: rgb(100 116 139); }
  `],
})
export class OtpGateComponent {
  private readonly auth = inject(AuthService);

  readonly open = input(false);
  readonly cancelled = output<void>();
  readonly verified = output<void>();

  readonly channel = signal<Channel>('email');
  readonly step = signal<Step>('identify');
  readonly email = signal('');
  readonly mobile = signal('');
  readonly code = signal('');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly identifier = computed(() => (this.channel() === 'email' ? this.email() : this.toTenDigits(this.mobile())));

  /**
   * Strips everything but digits while typing. Deliberately does NOT trim a leading +91
   * here — doing that on every keystroke made digits visibly vanish from the front of the
   * field once the user typed past 10 of them. The +91 is collapsed away in toTenDigits()
   * instead, applied on blur and again right before the request is sent.
   */
  sanitizeMobile(raw: string): string {
    return raw.replace(/\D/g, '').slice(0, 12);
  }

  /** Collapses a +91/91/0-prefixed number down to its bare last-10 digits. */
  private toTenDigits(v: string): string {
    let d = v.replace(/\D/g, '');
    if (d.length > 10 && (d.startsWith('91') || d.startsWith('0'))) d = d.slice(d.startsWith('91') ? 2 : 1);
    return d.slice(-10);
  }

  /** Tidies the field to its clean 10-digit form once the user leaves it. */
  normalizeMobile(): void { this.mobile.set(this.toTenDigits(this.mobile())); }

  readonly canRequest = computed(() =>
    this.channel() === 'email' ? /\S+@\S+\.\S+/.test(this.email()) : /^\d{10}$/.test(this.toTenDigits(this.mobile())),
  );

  requestCode(): void {
    if (!this.canRequest() || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);

    const request$ =
      this.channel() === 'email'
        ? this.auth.requestEmailOtp(this.email())
        : this.auth.requestMobileOtp(this.toTenDigits(this.mobile()));

    request$.subscribe({
      next: () => {
        this.busy.set(false);
        this.step.set('code');
      },
      error: (e) => {
        this.busy.set(false);
        this.error.set(e?.error?.message ?? 'Could not send the code. Please try again.');
      },
    });
  }

  verify(): void {
    if (this.code().length !== 6 || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);

    const verify$ =
      this.channel() === 'email'
        ? this.auth.verifyEmailOtp(this.email(), this.code())
        : this.auth.verifyMobileOtp(this.toTenDigits(this.mobile()), this.code());

    verify$.subscribe({
      next: () => {
        this.busy.set(false);
        this.reset();
        this.verified.emit();
      },
      error: (e) => {
        this.busy.set(false);
        this.error.set(e?.error?.message ?? 'That code was not correct.');
      },
    });
  }

  cancel(): void {
    this.reset();
    this.cancelled.emit();
  }

  private reset(): void {
    this.step.set('identify');
    this.code.set('');
    this.error.set(null);
    this.busy.set(false);
  }
}
