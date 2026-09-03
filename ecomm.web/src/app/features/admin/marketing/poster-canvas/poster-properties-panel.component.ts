import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PosterCanvasService } from '../../../../core/services/poster-canvas.service';
import { MediaService } from '../../../../core/services/media.service';
import { PosterLayer } from '../../../../core/services/marketing-studio.service';

const FONTS = ['Poppins', 'Montserrat', 'Archivo', 'Oswald', 'Bebas Neue', 'Space Grotesk', 'DM Sans'];
const WEIGHTS = ['400', '700', '800', '900'];

/**
 * Field set for whatever's currently selected on the canvas — a sibling of `PosterCanvasComponent`
 * under the same `PosterCanvasService` provider, reading `svc.selection()` and writing through
 * `svc.setLayerProp()` (which itself debounces the undo-history commit, so typing/dragging a field
 * here doesn't spam one history entry per keystroke).
 */
@Component({
  selector: 'app-poster-properties-panel',
  imports: [FormsModule],
  template: `
    @if (svc.selection(); as l) {
      <section class="bg-white border border-slate-200 rounded-xl p-3 space-y-3">
        <div class="flex items-center justify-between">
          <h2 class="text-xs font-semibold text-slate-500 uppercase tracking-wide">{{ l.type }}{{ l.role ? ' · ' + l.role : '' }}</h2>
          <button type="button" (click)="svc.deleteLayer(l.id)" class="text-xs text-red-500 hover:text-red-700">Delete</button>
        </div>

        @if (l.type === 'text') {
          <div>
            <label class="lbl">Text</label>
            <textarea class="input" rows="2" [ngModel]="l.text" (ngModelChange)="set(l, { text: $event })"></textarea>
          </div>
          <div class="grid grid-cols-2 gap-2">
            <div>
              <label class="lbl">Font</label>
              <select class="input" [ngModel]="l.fontFamily" (ngModelChange)="set(l, { fontFamily: $event })">
                @for (f of fonts; track f) { <option [ngValue]="f">{{ f }}</option> }
              </select>
            </div>
            <div>
              <label class="lbl">Weight</label>
              <select class="input" [ngModel]="l.fontWeight" (ngModelChange)="set(l, { fontWeight: $event })">
                @for (w of weights; track w) { <option [ngValue]="w">{{ w }}</option> }
              </select>
            </div>
          </div>
          <div class="grid grid-cols-2 gap-2">
            <div>
              <label class="lbl">Size</label>
              <input type="number" class="input" [ngModel]="l.fontSize" (ngModelChange)="set(l, { fontSize: $event })" />
            </div>
            <div>
              <label class="lbl">Colour</label>
              <input type="color" class="input h-9 p-0.5" [ngModel]="l.color" (ngModelChange)="set(l, { color: $event })" />
            </div>
          </div>
          <div>
            <label class="lbl">Align</label>
            <div class="flex gap-1">
              @for (a of aligns; track a) {
                <button type="button" (click)="set(l, { textAlign: a })" class="flex-1 text-xs py-1.5 rounded-lg border"
                        [class]="l.textAlign === a ? 'bg-teal-600 text-white border-teal-600' : 'border-slate-300 text-slate-600'">{{ a }}</button>
              }
            </div>
          </div>
        }

        @if (l.type === 'shape') {
          <div class="grid grid-cols-2 gap-2">
            <div>
              <label class="lbl">Fill</label>
              <input type="color" class="input h-9 p-0.5" [ngModel]="l.fill" (ngModelChange)="set(l, { fill: $event })" />
            </div>
            <div>
              <label class="lbl">Stroke</label>
              <input type="color" class="input h-9 p-0.5" [ngModel]="l.stroke ?? '#000000'" (ngModelChange)="set(l, { stroke: $event })" />
            </div>
          </div>
          <div class="grid grid-cols-2 gap-2">
            <div>
              <label class="lbl">Stroke width</label>
              <input type="number" min="0" class="input" [ngModel]="l.strokeWidth" (ngModelChange)="set(l, { strokeWidth: $event })" />
            </div>
            @if (l.shapeKind === 'rect') {
              <div>
                <label class="lbl">Corner radius</label>
                <input type="number" min="0" class="input" [ngModel]="l.cornerRadius" (ngModelChange)="set(l, { cornerRadius: $event })" />
              </div>
            }
          </div>
        }

        @if (l.type === 'image') {
          <div>
            <label class="lbl">Fit</label>
            <select class="input" [ngModel]="l.fit ?? 'cover'" (ngModelChange)="set(l, { fit: $event })">
              <option value="cover">Cover</option>
              <option value="contain">Contain</option>
            </select>
          </div>
          <button type="button" (click)="fileInputRef().nativeElement.click()" [disabled]="uploading()"
                  class="w-full text-sm px-3 py-2 rounded-lg border border-slate-300 text-slate-600 hover:bg-slate-50 disabled:opacity-50">
            {{ uploading() ? 'Uploading…' : (l.imageUrl ? 'Replace image' : 'Add image') }}
          </button>
          <input #fileInput type="file" accept="image/*" class="hidden" (change)="onFileSelected($event, l)" />
          @if (uploadError()) { <p class="text-xs text-red-600">{{ uploadError() }}</p> }
        }

        <div class="grid grid-cols-2 gap-2 pt-2 border-t border-slate-100">
          <div>
            <label class="lbl">X</label>
            <input type="number" class="input" [ngModel]="round(l.x)" (ngModelChange)="set(l, { x: $event })" />
          </div>
          <div>
            <label class="lbl">Y</label>
            <input type="number" class="input" [ngModel]="round(l.y)" (ngModelChange)="set(l, { y: $event })" />
          </div>
          <div>
            <label class="lbl">Width</label>
            <input type="number" min="1" class="input" [ngModel]="round(l.width)" (ngModelChange)="set(l, { width: $event })" />
          </div>
          <div>
            <label class="lbl">Height</label>
            <input type="number" min="1" class="input" [ngModel]="round(l.height)" (ngModelChange)="set(l, { height: $event })" />
          </div>
          <div>
            <label class="lbl">Rotation</label>
            <input type="number" class="input" [ngModel]="round(l.rotation)" (ngModelChange)="set(l, { rotation: $event })" />
          </div>
          <div>
            <label class="lbl">Opacity</label>
            <input type="range" min="0" max="1" step="0.05" class="w-full mt-2.5" [ngModel]="l.opacity" (ngModelChange)="set(l, { opacity: $event })" />
          </div>
        </div>
      </section>
    }
  `,
})
export class PosterPropertiesPanelComponent {
  readonly svc = inject(PosterCanvasService);
  private readonly mediaApi = inject(MediaService);
  readonly fileInputRef = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');

  readonly fonts = FONTS;
  readonly weights = WEIGHTS;
  readonly aligns: Array<NonNullable<PosterLayer['textAlign']>> = ['left', 'center', 'right'];

  readonly uploading = signal(false);
  readonly uploadError = signal<string | null>(null);

  set(layer: PosterLayer, patch: Partial<PosterLayer>): void {
    this.svc.setLayerProp(layer.id, patch);
  }

  round(n: number): number {
    return Math.round(n);
  }

  onFileSelected(event: Event, layer: PosterLayer): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';   // allow re-selecting the same file later
    if (!file) return;
    this.uploadError.set(null);
    this.uploading.set(true);
    this.mediaApi.upload(file).subscribe({
      next: (media) => { this.uploading.set(false); void this.svc.setLayerImage(layer.id, media.url); },
      error: () => { this.uploading.set(false); this.uploadError.set('Upload failed. Please try again.'); },
    });
  }
}
