import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BrandKit, GrowthService } from '../../../core/services/growth.service';

@Component({
  selector: 'app-admin-brand-kit',
  imports: [FormsModule],
  template: `
    <div class="max-w-2xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900">Brand voice</h1>
      <p class="text-sm text-slate-500 mb-6">Sets the tone for everything AI generates for you.</p>

      @if (kit(); as k) {
        <div class="bg-white border border-slate-200 rounded-xl p-5 space-y-4">
          <div class="grid sm:grid-cols-2 gap-4">
            <div>
              <label class="lbl">Tone</label>
              <select [(ngModel)]="k.tone" name="tone" class="input">
                @for (t of tones; track t) { <option [ngValue]="t">{{ t }}</option> }
              </select>
            </div>
            <div>
              <label class="lbl">Language</label>
              <select [(ngModel)]="k.language" name="language" class="input">
                @for (l of languages; track l) { <option [ngValue]="l">{{ l }}</option> }
              </select>
            </div>
          </div>
          <div>
            <label class="lbl">Who are your customers?</label>
            <input [(ngModel)]="k.audience" name="audience" class="input" placeholder="e.g. young families in Tamil Nadu" />
          </div>
          <label class="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" [(ngModel)]="k.useEmoji" name="useEmoji" /> Use emoji in posts
          </label>
          <div>
            <label class="lbl">Hashtags to reuse <span class="text-slate-400 font-normal">(optional)</span></label>
            <input [(ngModel)]="k.hashtags" name="hashtags" class="input" placeholder="#MyStore #Handmade" />
          </div>
          <div>
            <label class="lbl">Words or claims to avoid <span class="text-slate-400 font-normal">(optional)</span></label>
            <input [(ngModel)]="k.doNotSay" name="doNotSay" class="input" placeholder="cheap, guaranteed, best in the world" />
          </div>

          @if (saved()) { <p class="text-sm text-green-700">Saved.</p> }
          <button type="button" (click)="save(k)" [disabled]="saving()" class="btn-primary disabled:opacity-60">
            {{ saving() ? 'Saving…' : 'Save brand voice' }}
          </button>
        </div>
      }
    </div>
  `,
})
export class AdminBrandKitComponent implements OnInit {
  private readonly api = inject(GrowthService);

  readonly kit = signal<BrandKit | null>(null);
  readonly saving = signal(false);
  readonly saved = signal(false);

  readonly tones = ['friendly', 'premium', 'value', 'playful'];
  readonly languages = ['English', 'Hindi', 'Tamil', 'Telugu', 'Hinglish'];

  ngOnInit(): void {
    this.api.brandKit().subscribe((k) => this.kit.set(k));
  }

  save(k: BrandKit): void {
    this.saving.set(true);
    this.saved.set(false);
    this.api.saveBrandKit(k).subscribe({
      next: (updated) => { this.kit.set(updated); this.saving.set(false); this.saved.set(true); },
      error: () => this.saving.set(false),
    });
  }
}
