import { isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthProvider } from '../../../core/models/auth.model';
import { AuthService } from '../../../core/services/auth.service';
import { PlatformBrandService } from '../../../core/services/platform-brand.service';
import { PlatformInfoService } from '../../../core/services/platform-info.service';

declare const google: any;

type Method = 'email' | 'otp';

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink],
  templateUrl: './login.component.html',
  styles: [`
    .f-display { font-family: 'Sora', ui-sans-serif, system-ui, sans-serif; letter-spacing: -0.02em; }
    .navlink { position: relative; padding-block: 4px; }
    .navlink::after {
      content: ""; position: absolute; left: 50%; right: 50%; bottom: 0; height: 2px;
      background: rgb(79 70 229); border-radius: 2px;
      transition: left 0.22s ease, right 0.22s ease;
    }
    .navlink:hover::after, .navlink:focus-visible::after { left: 0; right: 0; }
    .navlink:active { transform: scale(0.95); }
    @media (prefers-reduced-motion: no-preference) { .navlink { transition: transform 0.1s ease; } }
  `],
})
export class LoginComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly platform = inject(PlatformInfoService);
  private readonly brand = inject(PlatformBrandService);

  /** The apex host has no tenant/store, so the normal storefront header never renders here — this
   *  page brings its own platform header only in that case (a tenant store's login keeps its real
   *  storefront header from the app shell, untouched). */
  readonly isApex = signal(false);

  readonly loadingConfig = signal(true);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly otpSent = signal(false);
  readonly method = signal<Method>('email');

  private readonly providers = signal<AuthProvider[]>([]);
  readonly emailProvider = computed(() => this.enabled('EmailPassword'));
  readonly otpProvider = computed(() => this.enabled('MobileOtp'));
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
    this.platform.hostInfo().subscribe((info) => {
      if (info.hostType !== 'apex') return;
      this.isApex.set(true);
      this.brand.loadFonts();
    });
    this.auth.getConfig().subscribe({
      next: (cfg) => {
        this.providers.set(cfg.providers);
        // Mobile-first: default to OTP when available, else email.
        this.method.set(this.otpProvider() ? 'otp' : 'email');
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

  resetOtp(): void {
    this.otpSent.set(false);
    this.code = '';
  }

  // --- Google Identity Services (only when enabled + configured) ---
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

  googleSignIn(): void {
    if (this.isBrowser && typeof google !== 'undefined') google.accounts.id.prompt();
  }

  private run(obs: ReturnType<AuthService['login']>): void {
    this.submitting.set(true);
    this.error.set(null);
    obs.subscribe({
      next: () => {
        this.submitting.set(false);
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        this.router.navigateByUrl(returnUrl ?? this.defaultHome());
      },
      error: (e) => this.fail(e),
    });
  }

  /**
   * Where to land when there's no returnUrl. Plain '/' is wrong for staff: on the apex the landing
   * guard bounces it straight to /welcome, so a signed-in super admin appeared to go nowhere.
   */
  private defaultHome(): string {
    if (this.auth.isSuperAdmin()) return '/superadmin';
    if (this.auth.isAdmin()) return '/admin';
    return '/';
  }

  private fail(e: any): void {
    this.submitting.set(false);
    this.error.set(e?.error?.message ?? 'Something went wrong. Please try again.');
  }
}
