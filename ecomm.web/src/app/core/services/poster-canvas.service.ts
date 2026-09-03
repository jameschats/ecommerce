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
  sendObjectToBack?(obj: FabricObjectLike): void;
  bringObjectToFront?(obj: FabricObjectLike): void;
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

/**
 * A thin, testable-by-construction wrapper around one live Fabric.js `Canvas` instance — deliberately
 * component-scoped (provided on `PosterCanvasComponent`, not root) so its Fabric instance/DOM element
 * lifecycle is tied 1:1 to one editor session, never leaking across navigations. Fabric is dynamically
 * imported only inside `init()`, which is only ever called from `afterNextRender` (see
 * `poster-canvas.component.ts`) — this class itself has no top-level Fabric import, so merely importing
 * this file (e.g. for DI wiring) never pulls Fabric into a server-rendered bundle path.
 */
@Injectable()
export class PosterCanvasService {
  private readonly doc = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly loadedFontLinks = new Set<string>();

  private canvas: FabricCanvasLike | null = null;
  private fabricNs: typeof import('fabric') | null = null;
  private docMeta: Omit<PosterDocument, 'specVersion' | 'layers'> | null = null;

  readonly selection = signal<PosterLayer | null>(null);
  readonly layers = signal<PosterLayer[]>([]);

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
  }

  async loadDocument(doc: PosterDocument): Promise<void> {
    if (!this.canvas || !this.fabricNs) return;
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
    this.canvas.requestRenderAll();
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
    if (!this.canvas || !this.fabricNs) return;
    const layer: PosterLayer = {
      id: crypto.randomUUID(), type: 'text', x: 100, y: 100, width: 400, height: 100,
      rotation: 0, opacity: 1, zIndex: this.canvas.getObjects().length, role,
      text: 'Double-click to edit', fontFamily: 'Poppins', fontSize: 48, fontWeight: '700',
      fontStyle: 'normal', textAlign: 'left', color: '#111827', lineHeight: 1.16, letterSpacing: 0,
    };
    this.ensureFontsLoaded(new Set(['Poppins']));
    this.buildFabricObject(layer).then((obj) => {
      if (!obj || !this.canvas) return;
      this.canvas.add(obj);
      this.canvas.setActiveObject(obj);
      this.canvas.requestRenderAll();
      this.refreshLayers();
    });
  }

  deleteSelected(): void {
    if (!this.canvas) return;
    const active = this.canvas.getActiveObject();
    if (!active) return;
    this.canvas.remove(active);
    this.canvas.discardActiveObject();
    this.canvas.requestRenderAll();
    this.selection.set(null);
    this.refreshLayers();
  }

  setLayerProp(id: string, patch: Partial<PosterLayer>): void {
    if (!this.canvas) return;
    const obj = (this.canvas.getObjects() as TaggedFabricObject[]).find((o) => o.layerMeta?.id === id);
    if (!obj?.layerMeta) return;
    const merged: PosterLayer = { ...obj.layerMeta, ...patch };
    obj.layerMeta = merged;
    obj.set?.(layerToFabricOptions(merged));
    this.canvas.requestRenderAll();
    this.refreshLayers();
    if (this.selection()?.id === id) this.selection.set(merged);
  }

  /** Raw live Fabric object state, for on-page debugging when something renders differently than its
   *  authored geometry — read directly off the objects, not through the mapper, so this can't itself
   *  hide the bug it's meant to help find. */
  debugObjects(): Array<Record<string, unknown>> {
    if (!this.canvas) return [];
    const el = this.canvas.getElement();
    return [
      { _canvasElement: `${el.width}x${el.height} native, ${el.style.width}x${el.style.height} css, clientWidth=${el.clientWidth}` },
      ...this.canvas.getObjects().map((o: any) => ({
        id: (o as TaggedFabricObject).layerMeta?.id, ctor: o.constructor?.name,
        left: o.left, top: o.top, width: o.width, height: o.height,
        scaleX: o.scaleX, scaleY: o.scaleY, angle: o.angle, opacity: o.opacity,
        fill: o.fill, rx: o.rx, ry: o.ry,
        scaledW: o.getScaledWidth?.(), scaledH: o.getScaledHeight?.(),
      })),
    ];
  }

  destroy(): void {
    void this.canvas?.dispose();
    this.canvas = null;
    this.fabricNs = null;
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
  }

  private refreshLayers(): void {
    if (!this.canvas) return;
    const objects = this.canvas.getObjects() as TaggedFabricObject[];
    this.layers.set(objects.filter((o) => o.layerMeta).map((o) => o.layerMeta!));
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
      // round-trips correctly and (once the properties panel ships) can have an image assigned to it.
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
