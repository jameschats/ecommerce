import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-register',
  imports: [FormsModule, RouterLink],
  templateUrl: './register.component.html',
})
export class RegisterComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly allowed = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  fullName = '';
  email = '';
  password = '';

  ngOnInit(): void {
    this.auth.getConfig().subscribe({
      next: (cfg) => {
        const email = cfg.providers.find((p) => p.provider === 'EmailPassword' && p.isEnabled);
        this.allowed.set(!!email?.allowRegistration);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Unable to reach the server.');
        this.loading.set(false);
      },
    });
  }

  submit(): void {
    this.submitting.set(true);
    this.error.set(null);
    this.auth
      .register({ email: this.email.trim(), password: this.password, fullName: this.fullName.trim() || null })
      .subscribe({
        next: () => this.router.navigateByUrl('/'),
        error: (e) => {
          this.submitting.set(false);
          this.error.set(e?.error?.message ?? 'Registration failed. Please try again.');
        },
      });
  }
}
