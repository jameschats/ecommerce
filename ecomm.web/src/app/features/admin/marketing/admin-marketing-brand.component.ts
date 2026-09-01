import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MediaService } from '../../../core/services/media.service';
import { MarketingBrand, MarketingStudioService } from '../../../core/services/marketing-studio.service';

/**
 * MS0 — Marketing Studio brand kit. The visual identity (logo, name, colours, fonts, handles) that
 * every generated creative starts from, plus the default include/exclude toggles. Colours are
 * pre-seeded from the store's published theme on first visit. Separate from "Brand voice" (Growth),
 * which sets tone/language for text.
 */
@Component({
  selector: 'app-admin-marketing-brand',
  imports: [FormsModule],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900">Marketing Studio — Brand kit</h1>
      <p class="text-sm text-slate-500 mb-6">
        Your logo, name and colours. Every poster, post and video the studio makes starts from these.
      </p>

      @if (brand(); as b) {
        <div class="grid lg:grid-cols-3 gap-6">
          <!-- editor -->
          <div class="lg:col-span-2 space-y-5">
            <section class="bg-white border border-slate-200 rounded-xl p-5 space-y-4">
              <h2 class="font-semibold text-slate-900">Identity</h2>
              <div class="grid sm:grid-cols-2 gap-4">
                <div>
                  <label class="lbl">Company name</label>
                  <input [(ngModel)]="b.companyName" name="companyName" class="input" placeholder="e.g. Meenakshi Silks" />
                </div>
                <div>
                  <label class="lbl">Tagline <span class="text-slate-400 font-normal">(optional)</span></label>
                  <input [(ngModel)]="b.tagline" name="tagline" class="input" placeholder="Handwoven, since 1985" />
                </div>
              </div>

              <div>
                <label class="lbl">Logo</label>
                <div class="flex items-center gap-4">
                  <div class="w-20 h-20 rounded-lg border border-slate-200 bg-slate-50 grid place-items-center overflow-hidden shrink-0">
                    @if (b.logoUrl) { <img [src]="b.logoUrl" alt="logo" class="max-w-full max-h-full object-contain" /> }
                    @else { <span class="text-slate-300 text-xs">No logo</span> }
                  </div>
                  <div class="space-y-1">
                    <input type="file" accept="image/*" (change)="onLogo($event, b)" class="text-sm" />
                    @if (uploading()) { <p class="text-xs text-slate-500">Uploading…</p> }
                    @if (b.logoUrl) {
                      <button type="button" (click)="b.logoUrl = null" class="block text-xs text-red-600 hover:underline">Remove logo</button>
                    }
                  </div>
                </div>
              </div>
            </section>

            <section class="bg-white border border-slate-200 rounded-xl p-5 space-y-4">
              <h2 class="font-semibold text-slate-900">Brand colours</h2>
              <p class="text-xs text-slate-500 -mt-2">Pre-filled from your storefront theme — adjust if your marketing look differs.</p>
              <div class="grid sm:grid-cols-3 gap-4">
                <div>
                  <label class="lbl">Primary</label>
                  <div class="flex items-center gap-2">
                    <input type="color" [(ngModel)]="b.primaryColor" name="primaryColor" class="h-9 w-12 rounded border border-slate-200 p-0.5" />
                    <input [(ngModel)]="b.primaryColor" name="primaryHex" class="input font-mono text-sm" />
                  </div>
                </div>
                <div>
                  <label class="lbl">Secondary</label>
                  <div class="flex items-center gap-2">
                    <input type="color" [(ngModel)]="b.secondaryColor" name="secondaryColor" class="h-9 w-12 rounded border border-slate-200 p-0.5" />
                    <input [(ngModel)]="b.secondaryColor" name="secondaryHex" class="input font-mono text-sm" />
                  </div>
                </div>
                <div>
                  <label class="lbl">Accent</label>
                  <div class="flex items-center gap-2">
                    <input type="color" [(ngModel)]="b.accentColor" name="accentColor" class="h-9 w-12 rounded border border-slate-200 p-0.5" />
                    <input [(ngModel)]="b.accentColor" name="accentHex" class="input font-mono text-sm" />
                  </div>
                </div>
              </div>
              <div class="sm:w-1/2">
                <label class="lbl">Font <span class="text-slate-400 font-normal">(optional)</span></label>
                <input [(ngModel)]="b.font" name="font" class="input" placeholder="e.g. Poppins" />
              </div>
            </section>

            <section class="bg-white border border-slate-200 rounded-xl p-5 space-y-3">
              <h2 class="font-semibold text-slate-900">Defaults for new creatives</h2>
              <label class="flex items-center gap-2 text-sm text-slate-700">
                <input type="checkbox" [(ngModel)]="b.includeLogoByDefault" name="includeLogo" /> Include my logo by default
              </label>
              <label class="flex items-center gap-2 text-sm text-slate-700">
                <input type="checkbox" [(ngModel)]="b.includeNameByDefault" name="includeName" /> Include my company name by default
              </label>
              <p class="text-xs text-slate-500">You can still flip these per poster or video.</p>
            </section>

            <section class="bg-white border border-slate-200 rounded-xl p-5 space-y-4">
              <h2 class="font-semibold text-slate-900">Social handles <span class="text-slate-400 font-normal text-sm">(optional)</span></h2>
              <div class="grid sm:grid-cols-2 gap-4">
                <div><label class="lbl">Instagram</label><input [(ngModel)]="b.instagramHandle" name="ig" class="input" placeholder="@yourstore" /></div>
                <div><label class="lbl">Facebook</label><input [(ngModel)]="b.facebookHandle" name="fb" class="input" placeholder="yourstore" /></div>
                <div><label class="lbl">LinkedIn</label><input [(ngModel)]="b.linkedInHandle" name="li" class="input" placeholder="company/yourstore" /></div>
                <div><label class="lbl">Pinterest</label><input [(ngModel)]="b.pinterestHandle" name="pin" class="input" placeholder="yourstore" /></div>
                <div><label class="lbl">YouTube</label><input [(ngModel)]="b.youTubeHandle" name="yt" class="input" placeholder="@yourstore" /></div>
                <div><label class="lbl">WhatsApp number</label><input [(ngModel)]="b.whatsAppNumber" name="wa" class="input" placeholder="+91 90000 00000" /></div>
                <div class="sm:col-span-2"><label class="lbl">Website</label><input [(ngModel)]="b.websiteUrl" name="web" class="input" placeholder="https://…" /></div>
              </div>
            </section>

            <div class="flex items-center gap-3">
              <button type="button" (click)="save(b)" [disabled]="saving()" class="btn-primary disabled:opacity-60">
                {{ saving() ? 'Saving…' : 'Save brand kit' }}
              </button>
              @if (saved()) { <span class="text-sm text-green-700">Saved.</span> }
            </div>
          </div>

          <!-- live preview -->
          <div class="lg:col-span-1">
            <div class="sticky top-6">
              <p class="lbl mb-2">Preview</p>
              <div class="rounded-xl border border-slate-200 overflow-hidden shadow-sm">
                <div class="h-24 flex items-center justify-center" [style.background]="b.primaryColor">
                  @if (b.logoUrl) { <img [src]="b.logoUrl" alt="logo" class="max-h-14 max-w-[70%] object-contain" /> }
                  @else { <span class="text-white/90 font-semibold text-lg">{{ b.companyName || 'Your brand' }}</span> }
                </div>
                <div class="p-4 bg-white space-y-3">
                  <div class="font-semibold text-slate-900" [style.fontFamily]="b.font || 'inherit'">
                    {{ b.companyName || 'Your company' }}
                  </div>
                  @if (b.tagline) { <div class="text-sm text-slate-500">{{ b.tagline }}</div> }
                  <button type="button" class="text-sm font-medium text-white rounded-lg px-3 py-1.5" [style.background]="b.accentColor">
                    Shop now
                  </button>
                  <div class="flex gap-2 pt-1">
                    <span class="h-6 w-6 rounded-full border border-slate-200" [style.background]="b.primaryColor" title="Primary"></span>
                    <span class="h-6 w-6 rounded-full border border-slate-200" [style.background]="b.secondaryColor" title="Secondary"></span>
                    <span class="h-6 w-6 rounded-full border border-slate-200" [style.background]="b.accentColor" title="Accent"></span>
                  </div>
                </div>
              </div>
              <p class="text-xs text-slate-400 mt-2">A rough sense of how your brand looks on a creative.</p>
            </div>
          </div>
        </div>
      } @else {
        <p class="text-sm text-slate-500">Loading…</p>
      }
    </div>
  `,
})
export class AdminMarketingBrandComponent implements OnInit {
  private readonly api = inject(MarketingStudioService);
  private readonly media = inject(MediaService);

  readonly brand = signal<MarketingBrand | null>(null);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly uploading = signal(false);

  ngOnInit(): void {
    this.api.getBrand().subscribe((b) => this.brand.set(b));
  }

  onLogo(event: Event, b: MarketingBrand): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.uploading.set(true);
    this.media.upload(file).subscribe({
      next: (m) => { b.logoUrl = m.url; this.uploading.set(false); },
      error: () => this.uploading.set(false),
    });
  }

  save(b: MarketingBrand): void {
    this.saving.set(true);
    this.saved.set(false);
    this.api.saveBrand(b).subscribe({
      next: (res) => { this.brand.set(res); this.saving.set(false); this.saved.set(true); },
      error: () => this.saving.set(false),
    });
  }
}
