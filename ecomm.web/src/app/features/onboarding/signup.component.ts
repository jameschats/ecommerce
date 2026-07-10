import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { OnboardingResult, PlanOption, SignupRequest } from '../../core/models/onboarding.model';
import { OnboardingService } from '../../core/services/onboarding.service';

@Component({
  selector: 'app-signup',
  imports: [FormsModule, DatePipe, RouterLink],
  template: `
    <!-- Minimal platform header (this is the SaaS signup, not a storefront) -->
    <header class="border-b border-slate-100">
      <div class="max-w-6xl mx-auto px-6 h-16 flex items-center justify-between">
        <a routerLink="/welcome" class="font-extrabold text-xl tracking-tight">Wav<span class="text-indigo-600">Commerce</span></a>
        <a routerLink="/login" class="text-sm text-slate-600 hover:text-slate-900">Log in</a>
      </div>
    </header>

    <div class="max-w-3xl mx-auto p-6">
      <h1 class="text-2xl font-bold text-slate-900">Start your own store</h1>
      <p class="text-slate-500 mb-6">Launch a store in minutes — 14-day free trial, no card required.</p>

      @if (created()) {
        <div class="rounded-xl border border-green-200 bg-green-50 p-6 text-center">
          <h2 class="text-lg font-semibold text-green-800">🎉 Your store is ready!</h2>
          <p class="text-slate-600 mt-1">Trial ends {{ created()!.trialEndsAt | date: 'mediumDate' }}.</p>
          <div class="flex items-center justify-center gap-3 mt-4">
            <a [href]="created()!.storeUrl + '/admin'" class="btn-primary inline-block">Go to my dashboard →</a>
            <a [href]="created()!.storeUrl" class="text-sm text-primary hover:underline">View store</a>
          </div>
          <p class="text-xs text-slate-400 mt-3">Sign in with the email &amp; password you just set, then add products and pick a theme.</p>
        </div>
      } @else {
        @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

        <!-- Plans -->
        <div class="grid sm:grid-cols-2 lg:grid-cols-4 gap-3 mb-6">
          @for (p of plans(); track p.planId) {
            <button type="button" (click)="form.planSlug = p.slug"
              class="text-left border rounded-xl p-4 transition"
              [class]="form.planSlug === p.slug ? 'border-blue-500 ring-2 ring-blue-200 bg-blue-50' : 'border-slate-200 hover:border-slate-300'">
              <div class="font-semibold text-slate-800">{{ p.name }}</div>
              <div class="text-xl font-bold text-slate-900">₹{{ p.monthlyPrice }}<span class="text-xs font-normal text-slate-400">/mo</span></div>
              <div class="text-xs text-slate-500 mt-1">{{ p.maxProducts ?? 'Unlimited' }} products</div>
              <div class="text-xs text-slate-500">{{ p.aiCredits }} AI credits</div>
            </button>
          }
        </div>

        <div class="bg-white border border-slate-200 rounded-xl p-5 grid sm:grid-cols-2 gap-4">
          <label class="block"><span class="lbl">Store name</span>
            <input [(ngModel)]="form.storeName" (ngModelChange)="onNameChange($event)" name="storeName" class="input w-full" placeholder="Acme Crafts" /></label>
          <label class="block"><span class="lbl">Store address</span>
            <div class="flex items-center">
              <input [(ngModel)]="form.slug" (ngModelChange)="checkSlug()" name="slug" class="input w-full rounded-r-none" placeholder="acme-store" />
              <span class="px-2 py-2 text-sm text-slate-400 border border-l-0 border-slate-300 rounded-r-lg bg-slate-50">.{{ baseDomain() }}</span>
            </div>
            @if (form.slug && slugState() === 'short') { <span class="text-xs text-amber-600">At least 3 characters</span> }
            @if (form.slug && slugState() === 'taken') { <span class="text-xs text-red-500">Taken</span> }
            @if (form.slug && slugState() === 'ok') { <span class="text-xs text-green-600">Available</span> }
            @if (!form.slug) { <span class="text-xs text-slate-400">Leave blank and we'll create one for you (e.g. acme-a3k9).</span> }
          </label>
          <label class="block"><span class="lbl">Your name</span>
            <input [(ngModel)]="form.ownerName" name="ownerName" class="input w-full" /></label>
          <label class="block"><span class="lbl">Email</span>
            <input type="email" [(ngModel)]="form.ownerEmail" name="ownerEmail" class="input w-full" /></label>
          <label class="block sm:col-span-2"><span class="lbl">Password</span>
            <input type="password" [(ngModel)]="form.password" name="password" class="input w-full" placeholder="At least 8 characters" /></label>
        </div>

        <button type="button" (click)="submit()" [disabled]="saving()" class="btn-primary mt-4">
          {{ saving() ? 'Creating your store…' : 'Create my store' }}
        </button>
      }
    </div>
  `,
})
export class SignupComponent implements OnInit {
  private readonly svc = inject(OnboardingService);
  private readonly route = inject(ActivatedRoute);

  readonly plans = signal<PlanOption[]>([]);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly created = signal<OnboardingResult | null>(null);
  readonly slugState = signal<'idle' | 'checking' | 'ok' | 'taken' | 'short'>('idle');
  // The store-address suffix, from the host the signup runs on (wavcommerce.online in prod).
  readonly baseDomain = signal<string>('your-domain.com');

  private slugTouched = false;
  form: SignupRequest = { storeName: '', slug: '', ownerName: '', ownerEmail: '', password: '', planSlug: 'starter' };

  ngOnInit(): void {
    this.svc.plans().subscribe((p) => this.plans.set(p));
    // Preselect the plan chosen on the landing page's pricing (/signup?plan=…).
    const plan = this.route.snapshot.queryParamMap.get('plan');
    if (plan) this.form.planSlug = plan;
    if (typeof window !== 'undefined' && window.location?.hostname) {
      this.baseDomain.set(window.location.hostname.replace(/^www\./, ''));
    }
  }

  onNameChange(name: string): void {
    if (this.slugTouched) return;
    // Suggest a clean base from the name; too short → leave blank so the backend auto-generates
    // a unique Shopify-style handle (e.g. "cafe24-a3k9"). No more single-letter "c" addresses.
    const base = name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 40);
    this.form.slug = base.length >= 3 ? base : '';
    if (this.form.slug) this.checkSlugInternal(); else this.slugState.set('idle');
  }

  checkSlug(): void { this.slugTouched = true; this.checkSlugInternal(); }

  private checkSlugInternal(): void {
    const slug = this.form.slug.trim();
    if (!slug) { this.slugState.set('idle'); return; }
    if (slug.length < 3) { this.slugState.set('short'); return; }
    this.slugState.set('checking');
    this.svc.slugAvailable(slug).subscribe((ok) => this.slugState.set(ok ? 'ok' : 'taken'));
  }

  submit(): void {
    this.saving.set(true);
    this.error.set(null);
    this.svc.signup(this.form).subscribe({
      next: (res) => { this.saving.set(false); this.created.set(res); },
      error: (e) => { this.saving.set(false); this.error.set(e?.error?.message ?? 'Signup failed.'); },
    });
  }
}
