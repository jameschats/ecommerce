import { PosterLayer } from './marketing-studio.service';

/**
 * Pure `PosterLayer <-> Fabric object` conversion — no Fabric import, no DOM, no canvas instance. This
 * is deliberately the one piece of the canvas subsystem cheap to unit-test in plain Node: everything
 * else (`PosterCanvasService`) is inherently stateful/SSR-unsafe (it owns a live Fabric `Canvas`).
 *
 * Fabric represents a resize as `scaleX`/`scaleY` multiplied against the object's original
 * `width`/`height`, not by mutating `width`/`height` directly. Our schema stores an absolute pixel
 * `Width`/`Height` instead (simpler to validate/reason about server-side) — `fabricObjectToLayer` reads
 * the *scaled* size back (`getScaledWidth`/`getScaledHeight`) and `layerToFabricOptions` always sets
 * `scaleX`/`scaleY` to 1 alongside the raw `width`/`height`, so a layer's `Width`/`Height` means the
 * same thing whether it came from a drag-handle resize or a properties-panel numeric field.
 */

/** A minimal structural shape of what we read off a live Fabric object — avoids importing Fabric's
 *  runtime module just for types (an `import type` would erase at compile time and be safe too, but
 *  this keeps the file with zero coupling to Fabric's exact version at all). */
export interface FabricObjectLike {
  left?: number; top?: number;
  width?: number; height?: number;
  scaleX?: number; scaleY?: number;
  angle?: number; opacity?: number;
  fill?: unknown; stroke?: unknown; strokeWidth?: number;
  text?: string;
  fontFamily?: string; fontSize?: number; fontWeight?: string | number; fontStyle?: string;
  textAlign?: string; lineHeight?: number; charSpacing?: number;
  rx?: number; ry?: number;
  getScaledWidth?: () => number;
  getScaledHeight?: () => number;
  set?: (props: Record<string, unknown>) => unknown;
}

/** Fabric constructor options for one layer — the canvas service picks the right class
 *  (`Textbox`/`Rect`/`Ellipse`/`Line`/`FabricImage`) based on `layer.type`/`layer.shapeKind` and passes
 *  this straight through as the second constructor argument (or first, for shapes/images). */
export function layerToFabricOptions(layer: PosterLayer): Record<string, unknown> {
  const base: Record<string, unknown> = {
    // Fabric defaults every object's origin to 'center' (left/top mean the object's CENTER, not its
    // top-left corner) — explicit here because this schema's X/Y are documented as top-left throughout
    // (PosterDocument.cs, the mapper's own tests, every authored template). Without this, every object
    // renders shifted left/up by half its own width/height: a 1080-wide "full bleed" shape ends up
    // exactly half off-canvas, a headline's leading characters land off-canvas to the left, etc. — this
    // was the actual root cause of every "layout looks wrong" report traced through this file.
    originX: 'left', originY: 'top',
    left: layer.x, top: layer.y,
    width: layer.width, height: layer.height, scaleX: 1, scaleY: 1,
    angle: layer.rotation, opacity: layer.opacity,
  };

  if (layer.type === 'text') {
    return {
      ...base,
      fontFamily: layer.fontFamily ?? 'Poppins',
      fontSize: layer.fontSize ?? 48,
      fontWeight: layer.fontWeight ?? '400',
      fontStyle: layer.fontStyle ?? 'normal',
      textAlign: layer.textAlign ?? 'left',
      fill: layer.color ?? '#111827',
      lineHeight: layer.lineHeight ?? 1.16,
      charSpacing: layer.letterSpacing ? layer.letterSpacing * 1000 : 0,   // Fabric's charSpacing is in 1/1000 em
    };
  }

  if (layer.type === 'shape') {
    const shape: Record<string, unknown> = {
      ...base,
      fill: layer.fill ?? '#111827',
      stroke: layer.stroke ?? undefined,
      strokeWidth: layer.strokeWidth ?? 0,
    };
    if (layer.shapeKind === 'rect' && layer.cornerRadius) {
      shape['rx'] = layer.cornerRadius;
      shape['ry'] = layer.cornerRadius;
    }
    return shape;
  }

  // image — width/height on a FabricImage constrain the drawn size directly (cover/contain handled by
  // the canvas service via scaleX/scaleY set to fill that box, since Fabric has no built-in object-fit).
  return base;
}

/** Reads a live Fabric object's current geometry/style back into our schema. `existing` supplies the
 *  fields Fabric doesn't track at all (id, type, role, shapeKind, imageUrl, fit) so callers only need
 *  to pass the object plus what created it. */
export function fabricObjectToLayer(obj: FabricObjectLike, existing: PosterLayer): PosterLayer {
  const width = obj.getScaledWidth ? obj.getScaledWidth() : (obj.width ?? existing.width) * (obj.scaleX ?? 1);
  const height = obj.getScaledHeight ? obj.getScaledHeight() : (obj.height ?? existing.height) * (obj.scaleY ?? 1);

  const updated: PosterLayer = {
    ...existing,
    x: obj.left ?? existing.x,
    y: obj.top ?? existing.y,
    width, height,
    rotation: obj.angle ?? existing.rotation,
    opacity: obj.opacity ?? existing.opacity,
  };

  if (existing.type === 'text') {
    updated.text = obj.text ?? existing.text;
    updated.fontFamily = obj.fontFamily ?? existing.fontFamily;
    updated.fontSize = obj.fontSize ?? existing.fontSize;
    updated.fontWeight = obj.fontWeight != null ? String(obj.fontWeight) : existing.fontWeight;
    updated.fontStyle = obj.fontStyle ?? existing.fontStyle;
    updated.textAlign = (obj.textAlign as PosterLayer['textAlign']) ?? existing.textAlign;
    updated.color = typeof obj.fill === 'string' ? obj.fill : existing.color;
    updated.lineHeight = obj.lineHeight ?? existing.lineHeight;
    updated.letterSpacing = obj.charSpacing != null ? obj.charSpacing / 1000 : existing.letterSpacing;
  } else if (existing.type === 'shape') {
    updated.fill = typeof obj.fill === 'string' ? obj.fill : existing.fill;
    updated.stroke = typeof obj.stroke === 'string' ? obj.stroke : existing.stroke;
    updated.strokeWidth = obj.strokeWidth ?? existing.strokeWidth;
    if (existing.shapeKind === 'rect' && obj.rx) updated.cornerRadius = obj.rx;
  }

  return updated;
}
