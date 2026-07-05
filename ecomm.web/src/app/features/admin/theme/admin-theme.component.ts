import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { API_BASE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';
import { MediaService } from '../../../core/services/media.service';
import { ThemeDto, ThemeService } from '../../../core/services/theme.service';

@Component({
  selector: 'app-admin-theme',
  imports: [FormsModule],
  template: `
    <div class="max-w-2xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Theme</h1>
      <p class="text-sm text-slate-500 mb-6">Set your brand colors and fonts. Changes preview live and apply to the whole storefront on save.</p>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

      @if (loading()) { <div class="p-8 text-center text-slate-400">Loading…</div> }
      @else {
        <div class="bg-white border border-slate-200 rounded-xl p-5 space-y-4">
          <div class="grid sm:grid-cols-2 gap-4">
            <div>
              <label class="lbl">Primary color</label>
              <div class="flex items-center gap-2">
                <input type="color" [(ngModel)]="settings.PrimaryColor" (ngModelChange)="preview()" class="h-9 w-12 rounded border border-slate-300" />
                <input type="text" [(ngModel)]="settings.PrimaryColor" (ngModelChange)="preview()" class="input" />
              </div>
            </div>
            <div>
              <label class="lbl">Secondary color</label>
              <div class="flex items-center gap-2">
                <input type="color" [(ngModel)]="settings.SecondaryColor" (ngModelChange)="preview()" class="h-9 w-12 rounded border border-slate-300" />
                <input type="text" [(ngModel)]="settings.SecondaryColor" (ngModelChange)="preview()" class="input" />
              </div>
            </div>
            <div>
              <label class="lbl">Font</label>
              <select [(ngModel)]="settings.Font" (ngModelChange)="preview()" class="input">
                <option value="Inter">Inter</option><option value="Roboto">Roboto</option>
                <option value="Poppins">Poppins</option><option value="system-ui">System</option>
              </select>
            </div>
            <div>
              <label class="lbl">Button style</label>
              <select [(ngModel)]="settings.ButtonStyle" (ngModelChange)="preview()" class="input">
                <option value="rounded">Rounded</option><option value="pill">Pill</option><option value="square">Square</option>
              </select>
            </div>
          </div>
          <div>
            <label class="lbl">Logo</label>
            <div class="flex items-center gap-3">
              <div class="w-24 h-12 rounded border border-slate-200 bg-slate-50 grid place-items-center overflow-hidden shrink-0">
                @if (settings.Logo) { <img [src]="settings.Logo" alt="logo" class="max-w-full max-h-full object-contain" /> }
                @else { <span class="text-[10px] text-slate-400">No logo</span> }
              </div>
              <input type="text" [(ngModel)]="settings.Logo" (ngModelChange)="preview()" placeholder="Paste a URL or upload →" class="input flex-1" />
              <label class="text-sm text-blue-600 hover:underline cursor-pointer whitespace-nowrap">
                {{ uploading() ? 'Uploading…' : 'Upload' }}
                <input type="file" accept="image/*" class="hidden" (change)="uploadLogo($event)" />
              </label>
            </div>
          </div>

          <!-- live sample: reflects unsaved edits instantly (theme vars applied to this document) -->
          <div class="border border-slate-200 rounded-xl p-4" [style.font-family]="'var(--app-font)'">
            <div class="text-xs text-slate-400 mb-2">Live preview</div>
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                @if (settings.Logo) { <img [src]="settings.Logo" alt="" class="h-6 object-contain" /> }
                @else { <span class="font-bold" [style.color]="settings.PrimaryColor">Your store</span> }
              </div>
              <div class="flex gap-2">
                <button type="button" class="btn-primary text-xs">Add to cart</button>
                <span class="text-xs px-3 py-1.5 rounded-lg border" [style.color]="settings.SecondaryColor" [style.border-color]="settings.SecondaryColor">Wishlist</span>
              </div>
            </div>
          </div>

          <div class="flex items-center gap-3 pt-1">
            <button type="button" (click)="save()" [disabled]="saving()" class="btn-primary">{{ saving() ? 'Saving…' : 'Save theme' }}</button>
            <span class="text-sm text-slate-400">Live sample updates as you edit; save to apply to your storefront.</span>
          </div>
        </div>

        <!-- the real storefront (reflects the last saved theme) -->
        <div class="mt-6">
          <div class="flex items-center justify-between mb-2">
            <span class="lbl mb-0">Your storefront</span>
            <button type="button" (click)="reloadStore()" class="text-sm text-blue-600 hover:underline">↻ Refresh</button>
          </div>
          <div class="rounded-xl border border-slate-200 overflow-hidden bg-white">
            <iframe [src]="storeUrl()" class="w-full h-[520px] border-0" title="storefront preview"></iframe>
          </div>
        </div>
      }
    </div>
  `,
})
export class AdminThemeComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly theme = inject(ThemeService);
  private readonly media = inject(MediaService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly base = `${API_BASE_URL}/admin/theme`;

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly uploading = signal(false);
  readonly message = signal<string | null>(null);
  readonly storeUrl = signal<SafeResourceUrl>(this.sanitizer.bypassSecurityTrustResourceUrl('/'));

  settings = { PrimaryColor: '#2563eb', SecondaryColor: '#1e293b', Font: 'Inter', ButtonStyle: 'rounded', Logo: '' };

  ngOnInit(): void {
    this.http.get<ApiResponse<ThemeDto>>(this.base).subscribe({
      next: (r) => {
        const s = r.data?.settings ?? {};
        this.settings = {
          PrimaryColor: s['PrimaryColor'] || this.settings.PrimaryColor,
          SecondaryColor: s['SecondaryColor'] || this.settings.SecondaryColor,
          Font: s['Font'] || this.settings.Font,
          ButtonStyle: s['ButtonStyle'] || this.settings.ButtonStyle,
          Logo: s['Logo'] ?? '',
        };
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  preview(): void {
    this.theme.apply(this.settings);
  }

  reloadStore(): void {
    if (this.isBrowser) this.storeUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(`/?_=${Math.floor(performance.now())}`));
  }

  uploadLogo(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.uploading.set(true);
    this.message.set(null);
    this.media.upload(file).subscribe({
      next: (m) => { this.settings.Logo = m.url; this.uploading.set(false); },
      error: () => { this.uploading.set(false); this.message.set('Logo upload failed (max 5 MB; image files only).'); },
    });
    input.value = '';
  }

  save(): void {
    this.saving.set(true);
    this.message.set(null);
    this.http.put<ApiResponse<ThemeDto>>(this.base, { settings: this.settings }).subscribe({
      next: (r) => {
        this.theme.apply(r.data?.settings ?? this.settings);
        this.saving.set(false);
        this.message.set('Theme saved.');
        this.reloadStore();
      },
      error: () => { this.saving.set(false); this.message.set('Save failed.'); },
    });
  }
}
