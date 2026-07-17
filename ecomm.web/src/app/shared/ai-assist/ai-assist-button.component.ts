import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AiAssistService } from '../../core/services/ai-assist.service';

/**
 * The one reusable "✨ Improve with AI" affordance (AI-1). Drop it next to any text field:
 *   <app-ai-assist purpose="product-description" [text]="form.description" [context]="form.name"
 *                  (applied)="form.description = $event" />
 * It hides itself when AI is disabled platform-wide, calls the metered /ai/improve endpoint, previews the
 * suggestion, and emits the chosen text on "Use this". Out-of-credits (402) links to the buy-credits screen.
 */
@Component({
  selector: 'app-ai-assist',
  imports: [RouterLink],
  template: `
    @if (ai.enabled()) {
      <div class="mt-1.5">
        <button type="button" (click)="run()" [disabled]="loading()"
                class="inline-flex items-center gap-1 text-xs px-2 py-1 rounded-md border border-violet-200 text-violet-700 bg-violet-50 hover:bg-violet-100 disabled:opacity-60">
          <span aria-hidden="true">✨</span>{{ loading() ? 'Thinking…' : label }}
        </button>

        @if (error()) {
          <div class="mt-1.5 text-xs text-red-600">
            {{ error() }}
            @if (outOfCredits()) { — <a routerLink="/admin/ai" class="underline font-medium">Buy credits</a> }
          </div>
        }

        @if (suggestion() !== null) {
          <div class="mt-1.5 rounded-lg border border-violet-200 bg-violet-50/60 p-2.5">
            <div class="text-[11px] font-medium text-violet-700 mb-1">✨ Suggestion</div>
            <p class="text-sm text-slate-700 whitespace-pre-wrap">{{ suggestion() }}</p>
            <div class="flex flex-wrap gap-2 mt-2">
              <button type="button" (click)="apply()" class="text-xs px-2.5 py-1 rounded-md bg-violet-600 text-white hover:bg-violet-700">Use this</button>
              <button type="button" (click)="run()" [disabled]="loading()" class="text-xs px-2.5 py-1 rounded-md border border-slate-300 bg-white hover:bg-slate-50">Regenerate</button>
              <button type="button" (click)="dismiss()" class="text-xs px-2.5 py-1 rounded-md border border-slate-300 bg-white hover:bg-slate-50">Dismiss</button>
            </div>
          </div>
        }
      </div>
    }
  `,
})
export class AiAssistButtonComponent {
  readonly ai = inject(AiAssistService);

  @Input({ required: true }) purpose = '';
  @Input() text = '';
  @Input() context = '';
  @Input() label = 'Improve with AI';
  @Output() applied = new EventEmitter<string>();

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly outOfCredits = signal(false);
  readonly suggestion = signal<string | null>(null);

  constructor() { this.ai.ensureStatus(); }

  run(): void {
    this.loading.set(true); this.error.set(null); this.outOfCredits.set(false);
    this.ai.improve(this.purpose, this.text ?? '', this.context || undefined).subscribe({
      next: (t) => { this.suggestion.set(t); this.loading.set(false); },
      error: (e: unknown) => { this.loading.set(false); this.fail(e); },
    });
  }

  apply(): void {
    const s = this.suggestion();
    if (s !== null) { this.applied.emit(s); this.suggestion.set(null); }
  }

  dismiss(): void { this.suggestion.set(null); }

  private fail(e: unknown): void {
    const err = e as { status?: number; error?: { message?: string } };
    this.outOfCredits.set(err?.status === 402);
    this.error.set(err?.error?.message ?? 'AI request failed. Please try again.');
  }
}
