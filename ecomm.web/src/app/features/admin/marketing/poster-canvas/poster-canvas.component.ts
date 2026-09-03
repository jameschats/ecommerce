import { Component, ElementRef, HostListener, OnDestroy, afterNextRender, inject, input, output, viewChild } from '@angular/core';
import { PosterCanvasService } from '../../../../core/services/poster-canvas.service';
import { PosterDocument } from '../../../../core/services/marketing-studio.service';

/**
 * The freeform canvas surface — a thin host around `PosterCanvasService`'s live Fabric instance.
 * Select/move/resize/rotate and double-click-to-edit text all come from Fabric's own defaults; this
 * component's job is just SSR-safe bootstrap, mounting the document once, and a minimal toolbar
 * (a real layers/properties panel is Stage E — this stage proves the canvas engine itself works).
 *
 * Deliberately mounted only once its starting `document` is already known (the parent gates this with
 * `@if (doc(); as d) { <app-poster-canvas [document]="d" /> }`) — avoids reactive re-init complexity
 * for a document that changes after the canvas already exists, which Stage D doesn't need.
 *
 * The canvas always draws at its document's real resolution (1080px+) so exports stay full quality —
 * but that's wider than this column on any normal screen, so it's scaled down for DISPLAY only
 * (`PosterCanvasService.setDisplaySize`, Fabric's `cssOnly` dimension mode) to fit whatever width the
 * wrapper div actually has. Without this the canvas rendered 1:1, simply wider than its container with
 * nothing to scale it — on a real admin layout that clipped most of the poster out of view entirely
 * rather than just needing a scrollbar.
 */
@Component({
  selector: 'app-poster-canvas',
  providers: [PosterCanvasService],
  template: `
    <div class="space-y-2">
      <div class="flex items-center gap-2">
        <button type="button" (click)="addText()" class="text-xs px-2.5 py-1.5 rounded-lg border border-slate-300 text-slate-600 hover:bg-slate-50">+ Text</button>
        <button type="button" (click)="deleteSelected()" [disabled]="!svc.selection()" class="text-xs px-2.5 py-1.5 rounded-lg border border-slate-300 text-slate-600 hover:bg-slate-50 disabled:opacity-40">
          Delete selected
        </button>
        @if (svc.selection(); as sel) {
          <span class="text-xs text-slate-400 ml-auto">Selected: {{ sel.role ?? sel.type }}</span>
        }
      </div>
      <div #wrap class="border border-slate-200 rounded-xl bg-slate-100 flex items-center justify-center p-4 min-w-0 overflow-hidden">
        <canvas #host class="max-w-full min-w-0"></canvas>
      </div>
    </div>
  `,
})
export class PosterCanvasComponent implements OnDestroy {
  readonly document = input.required<PosterDocument>();
  readonly ready = output<void>();

  private readonly hostRef = viewChild.required<ElementRef<HTMLCanvasElement>>('host');
  private readonly wrapRef = viewChild.required<ElementRef<HTMLDivElement>>('wrap');
  readonly svc = inject(PosterCanvasService);
  private resizeObserver: ResizeObserver | null = null;

  constructor() {
    afterNextRender(async () => {
      const doc = this.document();
      await this.svc.init(this.hostRef().nativeElement, doc.format.width, doc.format.height);
      await this.svc.loadDocument(doc);
      this.ready.emit();

      // ResizeObserver (not a single measurement in this same callback) deliberately: it only ever
      // fires once the browser has actually committed layout for the wrapper, so it can't race a
      // still-inflated intermediate box the way measuring clientWidth synchronously here could — and
      // it keeps firing for any later resize (window resize, sidebar collapse, anything), no separate
      // window:resize listener needed.
      const wrap = this.wrapRef().nativeElement;
      this.resizeObserver = new ResizeObserver(() => this.applyDisplaySize(doc.format.width, doc.format.height));
      this.resizeObserver.observe(wrap);
    });
  }

  /** Fits the canvas inside its wrapper's actual available content width (ResizeObserver's contentRect
   *  already excludes the wrapper's own padding/border), scaling height to match so the aspect ratio —
   *  and every layer's real coordinate space — is untouched. Caps at 1:1 so a canvas smaller than its
   *  container never gets blown up. */
  private applyDisplaySize(docWidth: number, docHeight: number): void {
    const available = this.wrapRef().nativeElement.clientWidth - 32;   // minus the wrapper's own p-4 (16px) each side
    const scale = Math.min(1, (available > 0 ? available : docWidth) / docWidth);
    this.svc.setDisplaySize(docWidth * scale, docHeight * scale);
  }

  @HostListener('document:keydown.delete', ['$event'])
  @HostListener('document:keydown.backspace', ['$event'])
  onDeleteKey(event: Event): void {
    // Skip while typing anywhere else on the page — including inside Fabric's OWN text-editing mode,
    // which manages a hidden native <textarea> positioned over the canvas while a Textbox is being
    // edited. Without this guard, Backspace while typing a headline would both delete a character
    // (Fabric's own handling) AND delete the entire selected layer (this handler) on every keystroke.
    const target = event.target as HTMLElement | null;
    if (target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable)) return;
    if (this.svc.selection()) this.deleteSelected();
  }

  addText(): void { this.svc.addTextLayer(); }
  deleteSelected(): void { this.svc.deleteSelected(); }

  toDocument(): PosterDocument { return this.svc.toDocument(); }
  exportPng(): Promise<Blob> { return this.svc.exportPng(); }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.svc.destroy();
  }
}
