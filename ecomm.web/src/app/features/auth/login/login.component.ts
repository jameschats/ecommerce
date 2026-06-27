import { isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthProvider } from '../../../core/models/auth.model';
import { AuthService } from '../../../core/services/auth.service';

declare const google: any;

type Method = 'email' | 'otp';

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
