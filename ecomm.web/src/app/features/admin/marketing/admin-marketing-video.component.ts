import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { VideoOptions, VideoPlan, VoiceOptions, MarketingStudioService } from '../../../core/services/marketing-studio.service';

/**
 * MS3·b — reel planner. Product → Goal → Platform → a ready-to-voice, ready-to-render scene plan (AI
 * narration + deterministic scenes/captions). The narration can be voiced right here via Sarvam TTS.
 * Final MP4 assembly comes from the render worker (next slice).
 */
@Component({
  selector: 'app-admin-marketing-video',
  imports: [FormsModule],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900">Marketing Studio — Reels</h1>
      <p class="text-sm text-slate-500 mb-6">Pick a product, a goal and a platform. We'll draft the reel — script, scenes and voiceover.</p>

      @if (opts(); as o) {
        <section class="bg-white border border-slate-200 rounded-xl p-5 grid sm:grid-cols-3 gap-4">
          <div>
            <label class="lbl">Product</label>
            <select [(ngModel)]="productId" name="product" class="input">
              <option [ngValue]="null">Organization (no product)</option>
              @for (p of o.products; track p.id) { <option [ngValue]="p.id">{{ p.name }}</option> }
            </select>
          </div>
          <div>
            <label class="lbl">Goal</label>
            <select [(ngModel)]="goal" name="goal" class="input">
              @for (g of o.goals; track g.code) { <option [ngValue]="g.code">{{ g.name }}</option> }
            </select>
          </div>
          <div>
            <label class="lbl">Platform</label>
            <select [(ngModel)]="platform" name="platform" class="input">
              @for (p of o.platforms; track p.code) { <option [ngValue]="p.code">{{ p.name }}</option> }
            </select>
          </div>
          <div class="sm:col-span-3">
            <button type="button" (click)="generate()" [disabled]="busy()" class="btn-primary disabled:opacity-60">
              {{ busy() ? 'Drafting…' : 'Generate reel plan' }}
            </button>
          </div>
        </section>

        @if (plan(); as pl) {
          <section class="bg-white border border-slate-200 rounded-xl p-5 mt-5 space-y-4">
            <div class="flex flex-wrap gap-2 text-xs">
              <span class="px-2 py-0.5 rounded-full bg-slate-100 text-slate-600">{{ pl.durationSeconds }}s</span>
              <span class="px-2 py-0.5 rounded-full bg-slate-100 text-slate-600">{{ pl.aspect }}</span>
              <span class="px-2 py-0.5 rounded-full bg-slate-100 text-slate-600">music: {{ pl.musicVertical }}</span>
            </div>

            <div>
              <label class="lbl">Narration (spoken track)</label>
              <textarea [(ngModel)]="narration" name="narration" rows="3" class="input"></textarea>
            </div>

            <div>
              <div class="lbl mb-1">Scenes</div>
              <ol class="space-y-2">
                @for (s of pl.scenes; track $index; let i = $index) {
                  <li class="flex items-center gap-3 text-sm">
                    <span class="w-6 h-6 rounded-full bg-slate-900 text-white grid place-items-center text-xs shrink-0">{{ i + 1 }}</span>
                    <span class="text-slate-400 w-10 shrink-0">{{ s.durationSeconds }}s</span>
                    <span class="text-slate-500 w-40 shrink-0">{{ visualLabel(s.visual) }}</span>
                    <span class="font-medium text-slate-800">{{ s.text }}</span>
                  </li>
                }
              </ol>
            </div>

            @if (voice()?.enabled) {
              <div class="flex items-center gap-3 pt-1">
                <select [(ngModel)]="language" name="language" class="input max-w-[160px]">
                  @for (l of voice()!.languages; track l.code) { <option [ngValue]="l.code">{{ l.name }}</option> }
                </select>
                <button type="button" (click)="voiceIt()" [disabled]="voicing() || !narration.trim()" class="btn-primary text-sm disabled:opacity-60">
                  {{ voicing() ? 'Voicing…' : 'Generate voiceover' }}
                </button>
                @if (voiceErr()) { <span class="text-sm text-red-600">{{ voiceErr() }}</span> }
              </div>
              @if (audioUrl(); as url) { <audio [src]="url" controls class="w-full mt-2"></audio> }
            } @else {
              <p class="text-xs text-slate-400">Voiceover turns on once the voice provider key is set.</p>
            }

            <p class="text-xs text-slate-400 border-t border-slate-100 pt-3">
              Rendering this into a finished MP4 (Ken Burns motion + captions + music) is the render-worker step we're building next.
            </p>
          </section>
        }
      } @else {
        <p class="text-sm text-slate-500">Loading…</p>
      }
    </div>
  `,
})
export class AdminMarketingVideoComponent implements OnInit {
  private readonly api = inject(MarketingStudioService);

  readonly opts = signal<VideoOptions | null>(null);
  readonly voice = signal<VoiceOptions | null>(null);
  readonly plan = signal<VideoPlan | null>(null);
  readonly busy = signal(false);
  readonly voicing = signal(false);
  readonly audioUrl = signal<string | null>(null);
  readonly voiceErr = signal<string | null>(null);

  productId: number | null = null;
  goal = 'product-promo';
  platform = 'instagram';
  narration = '';
  language = 'en-IN';

  ngOnInit(): void {
    this.api.videoOptions().subscribe((o) => this.opts.set(o));
    this.api.voiceOptions().subscribe((v) => this.voice.set(v));
  }

  generate(): void {
    this.busy.set(true);
    this.audioUrl.set(null);
    this.api.videoPlan(this.productId, this.goal, this.platform).subscribe({
      next: (p) => { this.plan.set(p); this.narration = p.narration; this.busy.set(false); },
      error: () => this.busy.set(false),
    });
  }

  voiceIt(): void {
    this.voicing.set(true);
    this.voiceErr.set(null);
    this.audioUrl.set(null);
    this.api.voicePreview(this.narration.trim(), this.language, null).subscribe({
      next: (r) => { this.audioUrl.set(r.audioUrl); this.voicing.set(false); },
      error: () => { this.voiceErr.set('Could not generate audio.'); this.voicing.set(false); },
    });
  }

  visualLabel(v: string): string {
    return { hero_product: 'Hero shot', zoom_product: 'Zoom in', detail_product: 'Detail', multiple_product_images: 'Gallery', brand_logo: 'Logo / CTA' }[v] ?? v;
  }
}
