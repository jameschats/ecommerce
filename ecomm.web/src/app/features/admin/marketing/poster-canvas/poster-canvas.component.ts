import { Component, ElementRef, HostListener, OnDestroy, afterNextRender, inject, input, output, signal, viewChild } from '@angular/core';
import { PosterCanvasService } from '../../../../core/services/poster-canvas.service';
import { MediaService } from '../../../../core/services/media.service';
import { PosterDocument } from '../../../../core/services/marketing-studio.service';

/**
 * The freeform canvas surface — a thin host around `PosterCanvasService`'s live Fabric instance.
 * Select/move/resize/rotate and double-click-to-edit text all come from Fabric's own defaults.
 *
 * `PosterCanvasService` is NOT provided here — it's provided once by the parent editor page
 * (`admin-marketing-poster.component.ts`) so this component's siblings (the layers panel, the
 * properties panel) share the exact same live Fabric instance via ordinary hierarchical DI, rather
 * than each needing this component to broker access to it.
 *
 * Deliberately mounted only once its starting `document` is already known (the parent gates this with
 * `@if (doc(); as d) { <app-poster-canvas [document]="d" /> }`) — avoids reactive re-init complexity
 * for a document that changes after the canvas already exists, which this stage doesn't need.
 *
 * The canvas always draws at its document's real resolution (1080px+) so exports stay full quality —
 * but that's wider than this column on any normal screen, so it's scaled down for DISPLAY only
 * (`PosterCanvasService.setDisplaySize`, Fabric's `cssOnly` dimension mode) to fit whatever width the
 * wrapper div actually has.
 */
@Component({
  selector: 'app-poster-canvas',
  template: `
    <div class="space-y-2">
      <div class="flex items-center gap-2 flex-wrap">
        <button type="button" (click)="svc.addTextLayer()" class="text-xs px-2.5 py-1.5 rounded-lg border border-slate-300 text-slate-600 hover:bg-slate-50">+ Text</button>
        <button type="button" (click)="fileInputRef().nativeElement.click()" [disabled]="uploading()" class="text-xs px-2.5 py-1.5 rounded-lg border border-slate-300 text-slate-600 hover:bg-slate-50 disabled:opacity-50">
          {{ uploading() ? 'Uploading…' : '+ Image' }}
        </button>
        <input #fileInput type="file" accept="image/*" class="hidden" (change)="onImageSelected($event)" />
        <button type="button" (click)="svc.addShapeLayer('rect')" class="text-xs px-2.5 py-1.5 rounded-lg border border-slate-300 text-slate-600 hover:bg-slate-50">+ Rect</button>
        <button type="button" (click)="svc.addShapeLayer('ellipse')" class="text-xs px-2.5 py-1.5 rounded-lg border border-slate-300 text-slate-600 hover:bg-slate-50">+ Circle</button>
        <button type="button" (click)="svc.addShapeLayer('line')" class="text-xs px-2.5 py-1.5 rounded-lg border border-slate-300 text-slate-600 hover:bg-slate-50">+ Line</button>
        <span class="w-px h-5 bg-slate-200"></span>
        <button type="button" (click)="svc.undo()" [disabled]="!svc.canUndo()" class="text-xs px-2.5 py-1.5 rounded-lg border border-slate-300 text-slate-600 hover:bg-slate-50 disabled:opacity-40">↶ Undo</button>
        <button type="button" (click)="svc.redo()" [disabled]="!svc.canRedo()" class="text-xs px-2.5 py-1.5 rounded-lg border border-slate-300 text-slate-600 hover:bg-slate-50 disabled:opacity-40">↷ Redo</button>
        <button type="button" (click)="deleteSelected()" [disabled]="!svc.selection()" class="text-xs px-2.5 py-1.5 rounded-lg border border-slate-300 text-slate-600 hover:bg-slate-50 disabled:opacity-40 ml-auto">
          Delete selected
        </button>
      </div>
      @if (uploadError()) { <p class="text-xs text-red-600">{{ uploadError() }}</p> }
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
  readonly fileInputRef = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');
  readonly svc = inject(PosterCanvasService);
  private readonly mediaApi = inject(MediaService);
  private resizeObserver: ResizeObserver | null = null;

  readonly uploading = signal(false);
  readonly uploadError = signal<string | null>(null);

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

  onImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';   // allow re-selecting the same file later
    if (!file) return;
    this.uploadError.set(null);
    this.uploading.set(true);
    this.mediaApi.upload(file).subscribe({
      next: (media) => { this.uploading.set(false); this.svc.addImageLayer(media.url); },
      error: () => { this.uploading.set(false); this.uploadError.set('Upload failed. Please try again.'); },
    });
  }

  @HostListener('window:resize')
  onResize(): void {
    const doc = this.document();
    this.applyDisplaySize(doc.format.width, doc.format.height);
  }

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    const target = event.target as HTMLElement | null;
    const typing = target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable);

    // Delete/Backspace: skip while typing anywhere else on the page — including inside Fabric's OWN
    // text-editing mode, which manages a hidden native <textarea> positioned over the canvas while a
    // Textbox is being edited. Without this guard, Backspace while typing a headline would both delete
    // a character (Fabric's own handling) AND delete the entire selected layer (this handler).
    if ((event.key === 'Delete' || event.key === 'Backspace') && !typing) {
      if (this.svc.selection()) this.deleteSelected();
      return;
    }

    // Ctrl/Cmd+Z / Ctrl/Cmd+Shift+Z — also skipped while typing, so it doesn't fight a browser's own
    // native undo inside a text input elsewhere on the page (e.g. the Caption textarea).
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z' && !typing) {
      event.preventDefault();
      if (event.shiftKey) this.svc.redo(); else this.svc.undo();
    }
  }

  deleteSelected(): void { this.svc.deleteSelected(); }

  toDocument(): PosterDocument { return this.svc.toDocument(); }
  exportPng(): Promise<Blob> { return this.svc.exportPng(); }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.svc.destroy();
  }
}
