import { DOCUMENT, Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { PosterDocument, PosterLayer } from './marketing-studio.service';
import { fabricObjectToLayer, layerToFabricOptions, FabricObjectLike } from './poster-document.mapper';

// Structural typing for the slice of Fabric's API this service uses — avoids a hard compile-time
// dependency on Fabric's own types so this file never needs `import type { ... } from 'fabric'` (the
// runtime module itself is dynamically imported in `init()`, never at module load time — see the
// SSR-safety note there).
interface FabricCanvasLike {
  add(...objects: FabricObjectLike[]): void;
  remove(...objects: FabricObjectLike[]): void;
  clear(): void;
  getObjects(): FabricObjectLike[];
  getActiveObject(): FabricObjectLike | undefined;
  setActiveObject(obj: FabricObjectLike): void;
  discardActiveObject(): void;
  requestRenderAll(): void;
  moveObjectTo?(obj: FabricObjectLike, index: number): void;
  getElement(): HTMLCanvasElement;
  dispose(): Promise<void> | void;
  on(event: string, handler: (e: unknown) => void): void;
  backgroundColor?: string;
  set(props: Record<string, unknown>): void;
  setDimensions(size: { width: string; height: string }, options: { cssOnly: true }): void;
}

interface TaggedFabricObject extends FabricObjectLike {
  layerMeta?: PosterLayer;
}

const HEADLINE_WEIGHTS = '400;700;800;900';   // wider than ThemeService.loadFonts' 400-700 — poster
                                               // headlines need genuine 900-weight caps, not a browser's
                                               // faux-bold of a lighter weight it happens to have loaded
const MAX_HISTORY = 50;
const COMMIT_DEBOUNCE_MS = 400;

/**
 * A thin, testable-by-construction wrapper around one live Fabric.js `Canvas` instance — deliberately
 * scoped to one editor session (provided once at the top of the Poster Studio editor, shared by the
 * canvas surface, layers panel, and properties panel, all siblings under that same provider) so its
 * Fabric instance/DOM element lifecycle is tied 1:1 to one session, never leaking across navigations.
 * Fabric is dynamically imported only inside `init()`, which is only ever called from `afterNextRender`
 * (see `poster-canvas.component.ts`) — this class itself has no top-level Fabric import, so merely
 * importing this file (e.g. for DI wiring) never pulls Fabric into a server-rendered bundle path.
 */
@Injectable()
export class PosterCanvasService {
  private readonly doc = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly loadedFontLinks = new Set<string>();

  private canvas: FabricCanvasLike | null = null;
  private fabricNs: typeof import('fabric') | null = null;
  private docMeta: Omit<PosterDocument, 'specVersion' | 'layers'> | null = null;

  private history: PosterDocument[] = [];
  private historyIndex = -1;
  private commitTimer: ReturnType<typeof setTimeout> | null = null;

  readonly selection = signal<PosterLayer | null>(null);
  readonly layers = signal<PosterLayer[]>([]);
  readonly canUndo = signal(false);
  readonly canRedo = signal(false);

  async init(canvasEl: HTMLCanvasElement, width: number, height: number): Promise<void> {
    if (!this.isBrowser) return;
    const fabric = await import('fabric');
    this.fabricNs = fabric;
    const canvas = new fabric.Canvas(canvasEl, { width, height, preserveObjectStacking: true }) as unknown as FabricCanvasLike;
    this.canvas = canvas;

    canvas.on('selection:created', (e) => this.onSelectionChanged(e));
    canvas.on('selection:updated', (e) => this.onSelectionChanged(e));
    canvas.on('selection:cleared', () => this.selection.set(null));
    canvas.on('object:modified', (e) => this.onObjectModified(e));
  }

  /** Scales the canvas's on-screen (CSS) size to fit its container while keeping the actual drawing
   *  resolution (and every layer's authored coordinates) untouched — `cssOnly: true` is Fabric's
   *  supported way to do this; it also keeps pointer/drag math correctly mapped back to the real
   *  coordinate space, so this is not just a visual scale, dragging/resizing stays accurate at any
   *  display size. Without this, a 1080px+ canvas rendered at its native resolution inside a
   *  same-size-or-smaller container either overflows (clipping content, what the merchant hit first)
   *  or needs the browser's own scrollbars, neither of which reads as a real design surface. */
  setDisplaySize(width: number, height: number): void {
    this.canvas?.setDimensions({ width: `${Math.round(width)}px`, height: `${Math.round(height)}px` }, { cssOnly: true });
    this.canvas?.requestRenderAll();
  }

  /** Loads a document as the editor's starting point — resets undo history to just this state. */
  async loadDocument(doc: PosterDocument): Promise<void> {
    if (!this.canvas) return;
    await this.renderDocument(doc);
    this.history = [this.toDocument()];
    this.historyIndex = 0;
    this.updateHistoryFlags();
  }

  private async renderDocument(doc: PosterDocument): Promise<void> {
    if (!this.canvas) return;
    const { specVersion: _v, layers, ...meta } = doc;
    this.docMeta = meta;
    this.canvas.clear();
    this.canvas.backgroundColor = doc.background.type === 'color' ? (doc.background.color ?? '#ffffff') : '#ffffff';

    const fonts = new Set(layers.filter((l) => l.type === 'text' && l.fontFamily).map((l) => l.fontFamily as string));
    this.ensureFontsLoaded(fonts);

    const sorted = [...layers].sort((a, b) => a.zIndex - b.zIndex);
    for (const layer of sorted) {
      const obj = await this.buildFabricObject(layer);
      if (obj) this.canvas.add(obj);
    }
    this.canvas.discardActiveObject();
    this.canvas.requestRenderAll();
    this.selection.set(null);
    this.refreshLayers();

    // A just-requested Google Font may not have finished downloading by the time objects were added
    // (browsers paint text in a fallback face until the real one loads) — repaint once it's ready so
    // the live canvas doesn't visually lag behind what will actually get exported.
    if (this.doc.fonts?.ready) this.doc.fonts.ready.then(() => this.canvas?.requestRenderAll());
  }

  /** Reassembles the full document from current canvas state — layer geometry/style from Fabric's own
   *  objects (via the mapper), everything else (format/background/templateId/kind/productId) from the
   *  metadata stashed at `loadDocument` time (background editing isn't in this stage's scope). */
  toDocument(): PosterDocument {
    if (!this.canvas || !this.docMeta) throw new Error('No document loaded.');
    const objects = this.canvas.getObjects() as TaggedFabricObject[];
    const layers = objects
      .filter((o) => o.layerMeta)
      .map((o, i) => ({ ...fabricObjectToLayer(o, o.layerMeta!), zIndex: i }));
    return { specVersion: 'layers-v1', ...this.docMeta, layers };
  }

  /** Rasterizes the current canvas to a PNG blob. Waits for web fonts to finish loading first — the
   *  live canvas may already look right from `loadDocument`'s own post-load repaint, but exporting
   *  immediately after a fast edit could otherwise still race a font that hasn't resolved yet. */
  async exportPng(): Promise<Blob> {
    if (!this.canvas) throw new Error('Canvas not initialized.');
    this.canvas.discardActiveObject();
    this.canvas.requestRenderAll();
    if (this.doc.fonts?.ready) await this.doc.fonts.ready;
    const el = this.canvas.getElement();
    return new Promise((resolve, reject) => {
      el.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('Canvas export failed.'))), 'image/png');
    });
  }

  addTextLayer(role: PosterLayer['role'] = null): void {
    if (!this.canvas) return;
    const layer: PosterLayer = {
      id: crypto.randomUUID(), type: 'text', x: 100, y: 100, width: 400, height: 100,
      rotation: 0, opacity: 1, zIndex: this.canvas.getObjects().length, role,
      text: 'Double-click to edit', fontFamily: 'Poppins', fontSize: 48, fontWeight: '700',
      fontStyle: 'normal', textAlign: 'left', color: '#111827', lineHeight: 1.16, letterSpacing: 0,
    };
    this.ensureFontsLoaded(new Set(['Poppins']));
    this.addLayer(layer);
  }

  addImageLayer(imageUrl: string, role: PosterLayer['role'] = null): void {
    if (!this.canvas) return;
    this.addLayer({
      id: crypto.randomUUID(), type: 'image', x: 100, y: 100, width: 400, height: 400,
      rotation: 0, opacity: 1, zIndex: this.canvas.getObjects().length, role, imageUrl, fit: 'cover',
    });
  }

  addShapeLayer(shapeKind: 'rect' | 'ellipse' | 'line'): void {
    if (!this.canvas) return;
    this.addLayer({
      id: crypto.randomUUID(), type: 'shape', x: 100, y: 100, width: 300, height: shapeKind === 'line' ? 4 : 200,
      rotation: 0, opacity: 1, zIndex: this.canvas.getObjects().length, shapeKind, fill: '#2563eb', strokeWidth: 0,
    });
  }

  private addLayer(layer: PosterLayer): void {
    this.buildFabricObject(layer).then((obj) => {
      if (!obj || !this.canvas) return;
      this.canvas.add(obj);
      this.canvas.setActiveObject(obj);
      this.canvas.requestRenderAll();
      this.refreshLayers();
      this.commit();
    });
  }

  /** Selects a layer by id — for the layers panel, where the row clicked isn't necessarily Fabric's
   *  own idea of "active" yet. */
  selectLayer(id: string): void {
    if (!this.canvas) return;
    const obj = this.findObject(id);
    if (!obj) return;
    this.canvas.setActiveObject(obj);
    this.canvas.requestRenderAll();
    this.selection.set(obj.layerMeta ? fabricObjectToLayer(obj, obj.layerMeta) : null);
  }

  deleteSelected(): void {
    const active = this.canvas?.getActiveObject() as TaggedFabricObject | undefined;
    if (active?.layerMeta) this.deleteLayer(active.layerMeta.id);
  }

  /** Swaps an image layer's URL in place — a placeholder Rect and a real FabricImage are different
   *  Fabric classes, so this can't just be a property patch like setLayerProp; it rebuilds the object
   *  at the SAME geometry/z-order/id and drops it back into the same stacking position, rather than
   *  the much simpler-looking (but position/size-losing) "delete then add fresh" alternative. */
  async setLayerImage(id: string, imageUrl: string): Promise<void> {
    if (!this.canvas) return;
    const obj = this.findObject(id);
    if (!obj?.layerMeta) return;
    const index = this.canvas.getObjects().indexOf(obj);
    const updatedLayer: PosterLayer = { ...obj.layerMeta, imageUrl };
    const newObj = await this.buildFabricObject(updatedLayer);
    if (!newObj) return;
    this.canvas.remove(obj);
    this.canvas.add(newObj);
    if (this.canvas.moveObjectTo) this.canvas.moveObjectTo(newObj, index);
    this.canvas.setActiveObject(newObj);
    this.canvas.requestRenderAll();
    this.refreshLayers();
    this.selection.set(updatedLayer);
    this.commit();
  }

  deleteLayer(id: string): void {
    if (!this.canvas) return;
    const obj = this.findObject(id);
    if (!obj) return;
    this.canvas.remove(obj);
    this.canvas.discardActiveObject();
    this.canvas.requestRenderAll();
    if (this.selection()?.id === id) this.selection.set(null);
    this.refreshLayers();
    this.commit();
  }

  /** Moves a layer to a new position in the stacking order — the layers panel's drag-reorder. Index 0
   *  is the back-most layer, matching the panel listing back-to-front top-to-bottom would invert; the
   *  panel is responsible for whatever visual order it wants to present and translating clicks back to
   *  a raw stacking index. */
  reorderLayer(id: string, newIndex: number): void {
    if (!this.canvas?.moveObjectTo) return;
    const obj = this.findObject(id);
    if (!obj) return;
    this.canvas.moveObjectTo(obj, newIndex);
    this.canvas.requestRenderAll();
    this.refreshLayers();
    this.commit();
  }

  setLayerProp(id: string, patch: Partial<PosterLayer>): void {
    if (!this.canvas) return;
    const obj = this.findObject(id);
    if (!obj?.layerMeta) return;
    const merged: PosterLayer = { ...obj.layerMeta, ...patch };
    obj.layerMeta = merged;
    obj.set?.(layerToFabricOptions(merged));
    this.canvas.requestRenderAll();
    this.refreshLayers();
    if (this.selection()?.id === id) this.selection.set(merged);
    this.commitDebounced();   // a properties-panel field firing on every keystroke/drag shouldn't
                               // create one undo step per keystroke — settles shortly after input stops
  }

  undo(): void {
    if (this.historyIndex <= 0) return;
    this.historyIndex--;
    void this.renderDocument(this.history[this.historyIndex]);
    this.updateHistoryFlags();
  }

  redo(): void {
    if (this.historyIndex >= this.history.length - 1) return;
    this.historyIndex++;
    void this.renderDocument(this.history[this.historyIndex]);
    this.updateHistoryFlags();
  }

  destroy(): void {
    if (this.commitTimer) clearTimeout(this.commitTimer);
    void this.canvas?.dispose();
    this.canvas = null;
    this.fabricNs = null;
  }

  private findObject(id: string): TaggedFabricObject | undefined {
    return (this.canvas?.getObjects() as TaggedFabricObject[] | undefined)?.find((o) => o.layerMeta?.id === id);
  }

  private onSelectionChanged(e: unknown): void {
    const obj = (e as { selected?: TaggedFabricObject[] })?.selected?.[0];
    this.selection.set(obj?.layerMeta ? fabricObjectToLayer(obj, obj.layerMeta) : null);
  }

  private onObjectModified(e: unknown): void {
    const obj = (e as { target?: TaggedFabricObject })?.target;
    if (!obj?.layerMeta) return;
    obj.layerMeta = fabricObjectToLayer(obj, obj.layerMeta);
    this.refreshLayers();
    if (this.selection()?.id === obj.layerMeta.id) this.selection.set(obj.layerMeta);
    this.commit();   // a drag/resize/rotate release is already a single settled action, no debounce needed
  }

  private refreshLayers(): void {
    if (!this.canvas) return;
    const objects = this.canvas.getObjects() as TaggedFabricObject[];
    this.layers.set(objects.filter((o) => o.layerMeta).map((o) => o.layerMeta!));
  }

  private commit(): void {
    if (this.commitTimer) { clearTimeout(this.commitTimer); this.commitTimer = null; }
    if (!this.canvas || !this.docMeta) return;
    const snapshot = this.toDocument();
    this.history = this.history.slice(0, this.historyIndex + 1);
    this.history.push(snapshot);
    this.historyIndex++;
    if (this.history.length > MAX_HISTORY) { this.history.shift(); this.historyIndex--; }
    this.updateHistoryFlags();
  }

  private commitDebounced(): void {
    if (this.commitTimer) clearTimeout(this.commitTimer);
    this.commitTimer = setTimeout(() => this.commit(), COMMIT_DEBOUNCE_MS);
  }

  private updateHistoryFlags(): void {
    this.canUndo.set(this.historyIndex > 0);
    this.canRedo.set(this.historyIndex < this.history.length - 1);
  }

  private async buildFabricObject(layer: PosterLayer): Promise<TaggedFabricObject | null> {
    if (!this.fabricNs) return null;
    const options = layerToFabricOptions(layer);
    let obj: TaggedFabricObject;

    if (layer.type === 'text') {
      obj = new this.fabricNs.Textbox(layer.text ?? '', options) as unknown as TaggedFabricObject;
    } else if (layer.type === 'shape') {
      if (layer.shapeKind === 'ellipse') {
        obj = new this.fabricNs.Ellipse({ ...options, rx: (options['width'] as number) / 2, ry: (options['height'] as number) / 2 }) as unknown as TaggedFabricObject;
      } else if (layer.shapeKind === 'line') {
        const w = options['width'] as number;
        obj = new this.fabricNs.Line([0, 0, w, 0], options) as unknown as TaggedFabricObject;
      } else {
        obj = new this.fabricNs.Rect(options) as unknown as TaggedFabricObject;
      }
    } else if (!layer.imageUrl) {
      // No photo set yet — a dashed placeholder in the layer's own position/shape, instead of silently
      // omitting the whole layer. Without this, an empty photo slot (the common case: a template's
      // starting document never ships with a photo already chosen) just leaves a hole in the canvas
      // with no indication anything belongs there. Still tagged with the real "image" layerMeta, so it
      // round-trips correctly and can have an image assigned via the properties panel's "Replace image".
      obj = new this.fabricNs.Rect({
        ...options, fill: 'rgba(255,255,255,0.12)', stroke: 'rgba(255,255,255,0.65)',
        strokeWidth: 2, strokeDashArray: [10, 8], strokeUniform: true,
        rx: layer.cornerRadius ?? 0, ry: layer.cornerRadius ?? 0,
      }) as unknown as TaggedFabricObject;
    } else {
      // image — width/height in `options` set the drawn box directly; Fabric has no built-in
      // object-fit, so "cover" scales up-and-crops via clipPath while "contain" (the default) just
      // uses the box as-is, matching the aspect ratio the layer was authored/resized to.
      const img = await this.fabricNs.FabricImage.fromURL(layer.imageUrl, { crossOrigin: 'anonymous' });
      img.set(options);
      if (layer.fit === 'cover') {
        const naturalW = img.width ?? 1, naturalH = img.height ?? 1;
        const boxW = options['width'] as number, boxH = options['height'] as number;
        const scale = Math.max(boxW / naturalW, boxH / naturalH);
        img.set({ scaleX: scale, scaleY: scale, width: naturalW, height: naturalH });
      }
      obj = img as unknown as TaggedFabricObject;
    }

    obj.layerMeta = layer;
    return obj;
  }

  private ensureFontsLoaded(families: Set<string>): void {
    for (const family of families) {
      if (this.loadedFontLinks.has(family)) continue;
      this.loadedFontLinks.add(family);
      const href = `https://fonts.googleapis.com/css2?family=${encodeURIComponent(family)}:wght@${HEADLINE_WEIGHTS}&display=swap`;
      if (this.doc.querySelector(`link[href="${href}"]`)) continue;
      const link = this.doc.createElement('link');
      link.rel = 'stylesheet';
      link.href = href;
      this.doc.head.appendChild(link);
    }
  }
}
