import { Component, inject } from '@angular/core';
import { CdkDragDrop, DragDropModule } from '@angular/cdk/drag-drop';
import { PosterCanvasService } from '../../../../core/services/poster-canvas.service';
import { PosterLayer } from '../../../../core/services/marketing-studio.service';

const TYPE_ICON: Record<PosterLayer['type'], string> = { text: 'T', image: '▧', shape: '◆' };

/**
 * The layers list — top-of-stack first (Photoshop/Canva convention), reordering via the same
 * `@angular/cdk/drag-drop` primitive already used elsewhere in this admin (e.g. the nav menu builder).
 * A sibling of `PosterCanvasComponent` under the same `PosterCanvasService` provider (see
 * `admin-marketing-poster.component.ts`), so clicking/reordering here acts on the same live canvas.
 */
@Component({
  selector: 'app-poster-layers-panel',
  imports: [DragDropModule],
  template: `
    <section class="bg-white border border-slate-200 rounded-xl p-3">
      <h2 class="text-xs font-semibold text-slate-500 uppercase tracking-wide px-1 mb-2">Layers</h2>
      @if (displayLayers().length === 0) {
        <p class="text-xs text-slate-400 px-1">No layers yet.</p>
      } @else {
        <div cdkDropList (cdkDropListDropped)="onDrop($event)" class="space-y-1">
          @for (layer of displayLayers(); track layer.id) {
            <div cdkDrag
                 (click)="svc.selectLayer(layer.id)"
                 class="flex items-center gap-2 px-2 py-1.5 rounded-lg cursor-pointer text-sm"
                 [class.bg-teal-50]="svc.selection()?.id === layer.id"
                 [class.text-teal-800]="svc.selection()?.id === layer.id"
                 [class.hover:bg-slate-50]="svc.selection()?.id !== layer.id">
              <span class="text-xs w-4 text-center text-slate-400 shrink-0">{{ typeIcon(layer) }}</span>
              <span class="flex-1 truncate">{{ label(layer) }}</span>
              <button type="button" (click)="toggleHidden(layer, $event)" class="text-xs text-slate-400 hover:text-slate-700 shrink-0" [title]="layer.opacity === 0 ? 'Show' : 'Hide'">
                {{ layer.opacity === 0 ? '🚫' : '👁' }}
              </button>
              <button type="button" (click)="remove(layer, $event)" class="text-xs text-slate-400 hover:text-red-600 shrink-0" title="Delete">✕</button>
            </div>
          }
        </div>
      }
    </section>
  `,
})
export class PosterLayersPanelComponent {
  readonly svc = inject(PosterCanvasService);
  private readonly restoreOpacity = new Map<string, number>();

  displayLayers(): PosterLayer[] {
    return [...this.svc.layers()].reverse();   // top of stack first
  }

  typeIcon(layer: PosterLayer): string {
    return TYPE_ICON[layer.type];
  }

  label(layer: PosterLayer): string {
    if (layer.role) return layer.role[0].toUpperCase() + layer.role.slice(1);
    if (layer.type === 'text') return layer.text?.slice(0, 24) || 'Text';
    if (layer.type === 'image') return 'Image';
    return layer.shapeKind ? layer.shapeKind[0].toUpperCase() + layer.shapeKind.slice(1) : 'Shape';
  }

  toggleHidden(layer: PosterLayer, event: Event): void {
    event.stopPropagation();
    if (layer.opacity === 0) {
      this.svc.setLayerProp(layer.id, { opacity: this.restoreOpacity.get(layer.id) ?? 1 });
    } else {
      this.restoreOpacity.set(layer.id, layer.opacity);
      this.svc.setLayerProp(layer.id, { opacity: 0 });
    }
  }

  remove(layer: PosterLayer, event: Event): void {
    event.stopPropagation();
    this.svc.deleteLayer(layer.id);
  }

  onDrop(event: CdkDragDrop<PosterLayer[]>): void {
    const displayed = this.displayLayers();
    const moved = displayed[event.previousIndex];
    if (!moved || event.previousIndex === event.currentIndex) return;
    const total = displayed.length;
    // The panel shows top-of-stack first; Fabric's own stacking index counts from the back — invert.
    const rawTargetIndex = total - 1 - event.currentIndex;
    this.svc.reorderLayer(moved.id, rawTargetIndex);
  }
}
