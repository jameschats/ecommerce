import { isPlatformBrowser } from '@angular/common';
import { Component, ElementRef, OnInit, PLATFORM_ID, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthProvider } from '../../../core/models/auth.model';
import { AuthService } from '../../../core/services/auth.service';

declare const google: any;

type Method = 'emailOtp' | 'otp' | 'email';

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink],
  templateUrl: './login.component.html',
})
export class LoginComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly googleBtnContainer = viewChild<ElementRef<HTMLDivElement>>('googleBtnContainer');

  readonly loadingConfig = signal(true);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly otpSent = signal(false);
  readonly method = signal<Method>('email');

  private readonly providers = signal<AuthProvider[]>([]);
  readonly emailProvider = computed(() => this.enabled('EmailPassword'));
  readonly otpProvider = computed(() => this.enabled('MobileOtp'));
  readonly emailOtpProvider = computed(() => this.enabled('EmailOtp'));

  /**
   * The sign-in methods on offer, so the switcher adapts instead of assuming two.
   * Turning Mobile OTP off in production must leave a usable page, not a broken grid.
   */
  readonly methods = computed(() => {
    const out: { key: Method; label: string }[] = [];
    if (this.emailOtpProvider()) out.push({ key: 'emailOtp', label: 'Email OTP' });
    if (this.otpProvider()) out.push({ key: 'otp', label: 'Mobile OTP' });
    if (this.emailProvider()) out.push({ key: 'email', label: 'Password' });
    return out;
  });
  readonly googleProvider = computed(() => {
    const p = this.enabled('Google');
    return p && p.clientId ? p : null;
  });
  readonly registrationAllowed = computed(() => this.emailProvider()?.allowRegistration ?? false);

  // form fields
  email = '';
  password = '';
  phone = '';
  code = '';

  ngOnInit(): void {
    this.auth.getConfig().subscribe({
      next: (cfg) => {
        this.providers.set(cfg.providers);
        // Whatever is on offer first — the order comes from admin, so the shop decides
        // which method leads rather than it being fixed here.
        this.method.set(this.methods()[0]?.key ?? 'email');
        this.loadingConfig.set(false);
        if (this.googleProvider()) this.initGoogle();
      },
      error: () => {
        this.error.set('Unable to reach the server. Is the API running?');
        this.loadingConfig.set(false);
      },
    });
  }

  private enabled(name: string): AuthProvider | null {
    return this.providers().find((p) => p.provider === name && p.isEnabled) ?? null;
  }

  submitEmail(): void {
    this.run(this.auth.login({ email: this.email.trim(), password: this.password }));
  }

  sendOtp(): void {
    if (!this.phone.trim()) return;
    this.submitting.set(true);
    this.error.set(null);
    this.auth.requestOtp(this.phone.trim()).subscribe({
      next: () => {
        this.otpSent.set(true);
        this.submitting.set(false);
      },
      error: (e) => this.fail(e),
    });
  }

  verifyOtp(): void {
    this.run(this.auth.verifyOtp(this.phone.trim(), this.code.trim()));
  }

  // Email OTP — the same two-step shape as mobile, against the endpoints the quick-order
  // checkout gate has always used.
  sendEmailOtp(): void {
    const email = this.email.trim();
    if (!email.includes('@')) {
      this.error.set('Enter a valid email address.');
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    this.auth.requestEmailOtp(email).subscribe({
      next: () => {
        this.otpSent.set(true);
        this.submitting.set(false);
      },
      error: (e) => this.fail(e),
    });
  }

  verifyEmailOtp(): void {
    this.run(this.auth.verifyEmailOtp(this.email.trim(), this.code.trim()));
  }

  resetOtp(): void {
    this.otpSent.set(false);
    this.code = '';
  }

  /** Switching method must not carry a half-finished OTP attempt across. */
  chooseMethod(m: Method): void {
    this.method.set(m);
    this.otpSent.set(false);
    this.code = '';
    this.error.set(null);
  }

  // --- Google Identity Services (only when enabled + configured) ---
  //
  // Renders Google's own button rather than a custom one driving accounts.id.prompt():
  // prompt() is the One Tap flow, which depends on FedCM finding an already-signed-in
  // Google browser session — with none, it fails outright (AbortError/NetworkError, never
  // falling back to a real sign-in screen), and even when it does work it only shows as a
  // small anchored popup, not the full account chooser "Continue with Google" implies.
  // renderButton's popup has neither problem, at the cost of not being able to fully
  // custom-style the button (theme/size/shape below are as close as Google allows).
  private initGoogle(): void {
    if (!this.isBrowser) return;
    const clientId = this.googleProvider()?.clientId;
    if (!clientId) return;
    const start = () => {
      if (typeof google === 'undefined') return;
      google.accounts.id.initialize({
        client_id: clientId,
        callback: (resp: { credential: string }) => this.run(this.auth.googleLogin(resp.credential)),
      });
      // Deferred a frame: the container only enters the DOM once googleProvider() flips
      // true (same signal renderButton's own caller depends on), and on a repeat visit
      // within the same SPA session — GSI script already loaded — this would otherwise
      // run before that render lands.
      requestAnimationFrame(() => {
        const el = this.googleBtnContainer()?.nativeElement;
        if (!el) return;
        const width = Math.min(400, Math.round(el.getBoundingClientRect().width) || 300);
        google.accounts.id.renderButton(el, {
          type: 'standard', theme: 'outline', size: 'large', shape: 'rectangular', text: 'continue_with', width,
        });
      });
    };
    if (typeof google !== 'undefined') {
      start();
    } else {
      const s = document.createElement('script');
      s.src = 'https://accounts.google.com/gsi/client';
      s.async = true;
      s.onload = start;
      document.head.appendChild(s);
    }
  }

  private run(obs: ReturnType<AuthService['login']>): void {
    this.submitting.set(true);
    this.error.set(null);
    obs.subscribe({
      next: () => {
        this.submitting.set(false);
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/';
        this.router.navigateByUrl(returnUrl);
      },
      error: (e) => this.fail(e),
    });
  }

  private fail(e: any): void {
    this.submitting.set(false);
    this.error.set(e?.error?.message ?? 'Something went wrong. Please try again.');
  }
}
