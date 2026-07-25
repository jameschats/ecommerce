import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
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
              <select [(ngModel)]="settings.Font" class="input">
                <option value="Inter">Inter</option><option value="Roboto">Roboto</option>
                <option value="Poppins">Poppins</option><option value="system-ui">System</option>
              </select>
            </div>
            <div>
              <label class="lbl">Button style</label>
              <select [(ngModel)]="settings.ButtonStyle" class="input">
                <option value="rounded">Rounded</option><option value="pill">Pill</option><option value="square">Square</option>
              </select>
            </div>
          </div>

          <!-- Text size. Segmented rather than a dropdown so all five steps are visible at
               once, and each label is drawn at the size it selects — the control is its own
               preview. -->
          <div>
            <label class="lbl">Website text size</label>
            <div class="flex flex-wrap gap-2">
              @for (s of textSizes; track s.value) {
                <button type="button" (click)="settings.TextScale = s.value"
                        class="px-3 py-2 rounded-lg border transition"
                        [class]="settings.TextScale === s.value
                          ? 'border-primary bg-primary/5 text-slate-900'
                          : 'border-slate-200 text-slate-600 hover:border-slate-300'">
                  <span [style.fontSize]="0.875 * +s.value + 'rem'">{{ s.label }}</span>
                </button>
              }
            </div>
            <p class="text-xs text-slate-500 mt-1.5">
              Applies to the customer-facing site only — these admin screens keep their own
              sizing. Save to apply.
            </p>
          </div>

          <div>
            <label class="lbl">Logo</label>
            <div class="flex items-center gap-3">
              <div class="w-24 h-12 rounded border border-slate-200 bg-slate-50 grid place-items-center overflow-hidden shrink-0">
                @if (settings.Logo) { <img [src]="settings.Logo" alt="logo" class="max-w-full max-h-full object-contain" /> }
                @else { <span class="text-[10px] text-slate-400">No logo</span> }
              </div>
              <input type="text" [(ngModel)]="settings.Logo" placeholder="Paste a URL or upload →" class="input flex-1" />
              <label class="text-sm text-blue-600 hover:underline cursor-pointer whitespace-nowrap">
                {{ uploading() ? 'Uploading…' : 'Upload' }}
                <input type="file" accept="image/*" class="hidden" (change)="uploadLogo($event)" />
              </label>
            </div>
          </div>

          <div class="flex items-center gap-3 pt-1">
            <button type="button" (click)="save()" [disabled]="saving()" class="btn-primary">{{ saving() ? 'Saving…' : 'Save theme' }}</button>
            <span class="text-sm text-slate-500">Preview:</span>
            <button type="button" class="btn-primary">Sample button</button>
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
  private readonly base = `${API_BASE_URL}/admin/theme`;

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly uploading = signal(false);
  readonly message = signal<string | null>(null);

  /** Stored as the multiplier itself, so the CSS needs no lookup table. */
  readonly textSizes = [
    { value: '0.9',   label: 'Compact' },
    { value: '0.95',  label: 'Small' },
    { value: '1',     label: 'Default' },
    { value: '1.075', label: 'Large' },
    { value: '1.15',  label: 'Extra large' },
  ];

  settings = {
    PrimaryColor: '#2563eb', SecondaryColor: '#1e293b', Font: 'Inter',
    ButtonStyle: 'rounded', Logo: '', TextScale: '1',
  };

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
          TextScale: s['TextScale'] || '1',
        };
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  preview(): void {
    this.theme.apply(this.settings);
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
      },
      error: () => { this.saving.set(false); this.message.set('Save failed.'); },
    });
  }
}
