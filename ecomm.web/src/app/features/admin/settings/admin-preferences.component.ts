import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { StorefrontPreferences, StorefrontPrefsService } from '../../../core/services/storefront-prefs.service';

@Component({
  selector: 'app-admin-preferences',
  imports: [FormsModule],
  template: `
    <div class="max-w-2xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Store preferences</h1>
      <p class="text-sm text-slate-500 mb-5">How your store appears in search + social, and whether it's password-protected before launch.</p>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

      @if (loading()) { <p class="text-slate-400 text-sm">Loading…</p> }
      @else {
        <form (ngSubmit)="save()" class="space-y-5">
          <!-- SEO -->
          <div class="bg-white border border-slate-200 rounded-xl p-6 space-y-3">
            <h2 class="font-semibold text-slate-800">Search &amp; social (SEO)</h2>
            <label class="block"><span class="lbl">Home page title</span><input class="input" [(ngModel)]="form.seoTitle" name="t" placeholder="Your Store — tagline" /></label>
            <label class="block"><span class="lbl">Meta description</span><textarea class="input" rows="2" [(ngModel)]="form.seoDescription" name="d" placeholder="Shown under your title in Google"></textarea></label>
            <label class="block"><span class="lbl">Social sharing image URL</span><input class="input" [(ngModel)]="form.seoImage" name="i" placeholder="https://…/share.png (1200×630)" /></label>
          </div>

          <!-- Password gate -->
          <div class="bg-white border border-slate-200 rounded-xl p-6 space-y-3">
            <h2 class="font-semibold text-slate-800">Pre-launch password</h2>
            <label class="flex items-center gap-3 cursor-pointer">
              <input type="checkbox" [(ngModel)]="form.passwordEnabled" name="pe" class="w-4 h-4" />
              <span><span class="text-sm font-medium text-slate-800">Restrict access with a password</span>
                <span class="block text-xs text-slate-400">Visitors need the password to view your store. Great before you launch.</span></span>
            </label>
            @if (form.passwordEnabled) {
              <div class="grid sm:grid-cols-2 gap-3 pt-1">
                <label class="block"><span class="lbl">Password</span><input class="input" [(ngModel)]="form.password" name="pw" [placeholder]="hadPassword() ? '•••• (saved — leave blank to keep)' : 'Set a password'" /></label>
              </div>
              <label class="block"><span class="lbl">Message to visitors (optional)</span><textarea class="input" rows="2" [(ngModel)]="form.passwordMessage" name="pm" placeholder="We're launching soon!"></textarea></label>
              <p class="text-xs text-amber-600">This is a soft gate for pre-launch, not account security. Share the password with your testers.</p>
            }
          </div>

          <button type="submit" [disabled]="saving()" class="btn-primary px-5 py-2.5">{{ saving() ? 'Saving…' : 'Save preferences' }}</button>
        </form>
      }
    </div>
  `,
})
export class AdminPreferencesComponent implements OnInit {
  private readonly api = inject(StorefrontPrefsService);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly message = signal<string | null>(null);
  readonly hadPassword = signal(false);

  form: StorefrontPreferences = { seoTitle: '', seoDescription: '', seoImage: '', passwordEnabled: false, password: '', passwordMessage: '' };

  ngOnInit(): void { this.load(); }
  private apply(p: StorefrontPreferences): void {
    this.hadPassword.set(!!p.password);
    this.form = { ...p, password: '' };
  }
  private load(): void { this.api.get().subscribe({ next: (p) => { this.apply(p); this.loading.set(false); }, error: () => this.loading.set(false) }); }

  save(): void {
    this.saving.set(true); this.message.set(null);
    this.api.update(this.form).subscribe({
      next: (p) => { this.apply(p); this.saving.set(false); this.message.set('Preferences saved.'); setTimeout(() => this.message.set(null), 2500); },
      error: () => this.saving.set(false),
    });
  }
}
