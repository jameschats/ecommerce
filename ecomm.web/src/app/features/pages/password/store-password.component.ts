import { Component, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CatalogService } from '../../../core/services/catalog.service';
import { ThemeService } from '../../../core/services/theme.service';
import { STORE_UNLOCK_KEY } from '../../../core/services/store-gate';

@Component({
  selector: 'app-store-password',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="min-h-screen flex items-center justify-center bg-slate-50 px-4">
      <div class="w-full max-w-md text-center">
        <h1 class="text-2xl font-bold text-slate-900 mb-1">{{ storeName() }}</h1>
        <p class="text-slate-500 mb-6">{{ message() || 'This store is opening soon.' }}</p>

        <form (ngSubmit)="submit()" class="bg-white border border-slate-200 rounded-xl p-6 space-y-3 text-left">
          <label class="block">
            <span class="lbl">Enter password to preview</span>
            <input class="input" type="password" [(ngModel)]="password" name="pw" autocomplete="off" [disabled]="checking()" />
          </label>
          @if (error()) { <p class="text-sm text-red-600">{{ error() }}</p> }
          <button type="submit" [disabled]="checking() || !password.trim()" class="btn-primary w-full py-2.5">
            {{ checking() ? 'Checking…' : 'Enter' }}
          </button>
        </form>
        <p class="text-xs text-slate-400 mt-4">Are you the store owner? <a routerLink="/login" class="underline">Log in here</a></p>
      </div>
    </div>
  `,
})
export class StorePasswordComponent implements OnInit {
  private readonly catalog = inject(CatalogService);
  private readonly theme = inject(ThemeService);
  private readonly router = inject(Router);
  private readonly platformId = inject(PLATFORM_ID);

  readonly storeName = this.theme.storeName;
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly checking = signal(false);
  password = '';

  ngOnInit(): void {
    // If the gate is off (or already unlocked), don't strand the visitor here.
    if (isPlatformBrowser(this.platformId) && localStorage.getItem(STORE_UNLOCK_KEY) === '1') {
      this.router.navigateByUrl('/');
      return;
    }
    this.catalog.getStoreGate().subscribe((g) => {
      if (!g.passwordProtected) this.router.navigateByUrl('/');
      else this.message.set(g.message);
    });
  }

  submit(): void {
    this.checking.set(true);
    this.error.set(null);
    this.catalog.checkStoreGate(this.password).subscribe((ok) => {
      this.checking.set(false);
      if (ok) {
        if (isPlatformBrowser(this.platformId)) localStorage.setItem(STORE_UNLOCK_KEY, '1');
        this.router.navigateByUrl('/');
      } else {
        this.error.set('That password is incorrect.');
      }
    });
  }
}
