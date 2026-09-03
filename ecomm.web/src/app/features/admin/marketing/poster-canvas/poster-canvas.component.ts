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
      <div class="border border-slate-200 rounded-xl overflow-auto bg-slate-100 flex items-center justify-center p-4">
        <canvas #host></canvas>
      </div>
    </div>
  `,
})
export class PosterCanvasComponent implements OnDestroy {
  readonly document = input.required<PosterDocument>();
  readonly ready = output<void>();

  private readonly hostRef = viewChild.required<ElementRef<HTMLCanvasElement>>('host');
  readonly svc = inject(PosterCanvasService);

  constructor() {
    afterNextRender(async () => {
      const doc = this.document();
      await this.svc.init(this.hostRef().nativeElement, doc.format.width, doc.format.height);
      await this.svc.loadDocument(doc);
      this.ready.emit();
    });
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

  ngOnDestroy(): void { this.svc.destroy(); }
}
