import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { VoiceOptions, MarketingStudioService } from '../../../core/services/marketing-studio.service';

/**
 * MS3·a — voiceover preview. The first slice of the video pipeline: type a line, pick an Indian
 * language + voice, and hear a Sarvam-generated MP3. This audio is what later gets muxed into a reel
 * by the render worker. When no TTS key is configured the page says so and stays inert.
 */
@Component({
  selector: 'app-admin-marketing-voice',
  imports: [FormsModule],
  template: `
    <div class="max-w-2xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900">Marketing Studio — Voiceover</h1>
      <p class="text-sm text-slate-500 mb-6">Generate a spoken voiceover in your language. It'll power the narration in your reels.</p>

      @if (opts(); as o) {
        @if (!o.enabled) {
          <div class="rounded-xl border border-amber-200 bg-amber-50 p-5 text-sm text-amber-800">
            Voice generation isn't switched on for this store yet. Once the voice provider key is set, this page goes live.
          </div>
        } @else {
          <div class="bg-white border border-slate-200 rounded-xl p-5 space-y-4">
            <div>
              <label class="lbl">Script</label>
              <textarea [(ngModel)]="text" name="text" rows="3" class="input" placeholder="e.g. Elegance that belongs in every celebration."></textarea>
            </div>
            <div class="grid sm:grid-cols-2 gap-4">
              <div>
                <label class="lbl">Language</label>
                <select [(ngModel)]="language" name="language" class="input">
                  @for (l of o.languages; track l.code) { <option [ngValue]="l.code">{{ l.name }}</option> }
                </select>
              </div>
              <div>
                <label class="lbl">Voice</label>
                <select [(ngModel)]="speaker" name="speaker" class="input">
                  @for (s of o.speakers; track s.code) { <option [ngValue]="s.code">{{ s.name }}</option> }
                </select>
              </div>
            </div>

            <div class="flex items-center gap-3">
              <button type="button" (click)="generate()" [disabled]="busy() || !text.trim()" class="btn-primary disabled:opacity-60">
                {{ busy() ? 'Generating…' : 'Generate voiceover' }}
              </button>
              @if (error()) { <span class="text-sm text-red-600">{{ error() }}</span> }
            </div>

            @if (audioUrl(); as url) {
              <div class="pt-2">
                <audio [src]="url" controls class="w-full"></audio>
              </div>
            }
          </div>
        }
      } @else {
        <p class="text-sm text-slate-500">Loading…</p>
      }
    </div>
  `,
})
export class AdminMarketingVoiceComponent implements OnInit {
  private readonly api = inject(MarketingStudioService);

  readonly opts = signal<VoiceOptions | null>(null);
  readonly busy = signal(false);
  readonly audioUrl = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  text = '';
  language = 'en-IN';
  speaker = '';

  ngOnInit(): void {
    this.api.voiceOptions().subscribe((o) => this.opts.set(o));
  }

  generate(): void {
    this.busy.set(true);
    this.error.set(null);
    this.audioUrl.set(null);
    this.api.voicePreview(this.text.trim(), this.language, this.speaker || null).subscribe({
      next: (r) => { this.audioUrl.set(r.audioUrl); this.busy.set(false); },
      error: () => { this.error.set('Could not generate audio. Please try again.'); this.busy.set(false); },
    });
  }
}
