import { PosterLayer } from './marketing-studio.service';
import { fabricObjectToLayer, layerToFabricOptions } from './poster-document.mapper';

function textLayer(overrides: Partial<PosterLayer> = {}): PosterLayer {
  return {
    id: 'headline', type: 'text', x: 72, y: 200, width: 500, height: 200,
    rotation: 0, opacity: 1, zIndex: 1, role: 'headline',
    text: 'Hello', fontFamily: 'Poppins', fontSize: 80, fontWeight: '900',
    fontStyle: 'normal', textAlign: 'left', color: '#ffffff', lineHeight: 1.05, letterSpacing: 0,
    ...overrides,
  };
}

function shapeLayer(overrides: Partial<PosterLayer> = {}): PosterLayer {
  return {
    id: 'pill', type: 'shape', x: 72, y: 940, width: 300, height: 88,
    rotation: 0, opacity: 1, zIndex: 8, shapeKind: 'rect', fill: '#ffffff', cornerRadius: 44,
    ...overrides,
  };
}

describe('layerToFabricOptions', () => {
  it('maps a text layer to Fabric Textbox options', () => {
    const opts = layerToFabricOptions(textLayer());
    expect(opts).toMatchObject({
      left: 72, top: 200, width: 500, height: 200, scaleX: 1, scaleY: 1,
      angle: 0, opacity: 1, fontFamily: 'Poppins', fontSize: 80, fontWeight: '900', fill: '#ffffff',
    });
  });

  it('converts letterSpacing from em to Fabric charSpacing (1/1000 em)', () => {
    const opts = layerToFabricOptions(textLayer({ letterSpacing: 0.02 }));
    expect(opts['charSpacing']).toBe(20);
  });

  it('maps a rounded-rect shape layer with rx/ry from cornerRadius', () => {
    const opts = layerToFabricOptions(shapeLayer());
    expect(opts).toMatchObject({ fill: '#ffffff', rx: 44, ry: 44 });
  });

  it('omits rx/ry when a rect has no cornerRadius', () => {
    const opts = layerToFabricOptions(shapeLayer({ cornerRadius: undefined }));
    expect(opts['rx']).toBeUndefined();
  });

  it('always normalizes scale to 1 regardless of layer size', () => {
    const opts = layerToFabricOptions(textLayer({ width: 999, height: 111 }));
    expect(opts).toMatchObject({ width: 999, height: 111, scaleX: 1, scaleY: 1 });
  });
});

describe('fabricObjectToLayer', () => {
  it('reads back position, scaled size, rotation and opacity', () => {
    const existing = textLayer();
    const obj = {
      left: 100, top: 150, angle: 15, opacity: 0.8,
      getScaledWidth: () => 620, getScaledHeight: () => 240,
    };
    const updated = fabricObjectToLayer(obj, existing);
    expect(updated).toMatchObject({ x: 100, y: 150, width: 620, height: 240, rotation: 15, opacity: 0.8 });
  });

  it('falls back to width*scaleX when getScaledWidth is unavailable', () => {
    const existing = textLayer({ width: 500, height: 200 });
    const obj = { width: 500, height: 200, scaleX: 1.5, scaleY: 2 };
    const updated = fabricObjectToLayer(obj, existing);
    expect(updated.width).toBe(750);
    expect(updated.height).toBe(400);
  });

  it('reads text-specific fields back for a text layer', () => {
    const existing = textLayer();
    const obj = { text: 'Updated', fontSize: 64, fontWeight: 700, fill: '#000000', charSpacing: 30 };
    const updated = fabricObjectToLayer(obj, existing);
    expect(updated.text).toBe('Updated');
    expect(updated.fontSize).toBe(64);
    expect(updated.fontWeight).toBe('700');
    expect(updated.color).toBe('#000000');
    expect(updated.letterSpacing).toBe(0.03);
  });

  it('does not touch text fields for a shape layer', () => {
    const existing = shapeLayer();
    const obj = { fill: '#123456', rx: 20 };
    const updated = fabricObjectToLayer(obj, existing);
    expect(updated.fill).toBe('#123456');
    expect(updated.cornerRadius).toBe(20);
    expect(updated.text).toBeUndefined();
  });

  it('preserves fields Fabric does not track (id, role, type, imageUrl)', () => {
    const existing: PosterLayer = {
      id: 'photo', type: 'image', x: 0, y: 0, width: 100, height: 100,
      rotation: 0, opacity: 1, zIndex: 3, role: 'photo', imageUrl: 'https://cdn/x.png', fit: 'cover',
    };
    const updated = fabricObjectToLayer({ left: 10, top: 20 }, existing);
    expect(updated.id).toBe('photo');
    expect(updated.role).toBe('photo');
    expect(updated.imageUrl).toBe('https://cdn/x.png');
    expect(updated.fit).toBe('cover');
  });

  it('round-trips a text layer through options and back with no drift', () => {
    const original = textLayer();
    const opts = layerToFabricOptions(original);
    const fauxObject = {
      ...opts,
      getScaledWidth: () => original.width,
      getScaledHeight: () => original.height,
    };
    const roundTripped = fabricObjectToLayer(fauxObject, original);
    expect(roundTripped.text).toBe(original.text);
    expect(roundTripped.fontFamily).toBe(original.fontFamily);
    expect(roundTripped.fontSize).toBe(original.fontSize);
    expect(roundTripped.color).toBe(original.color);
    expect(roundTripped.width).toBe(original.width);
    expect(roundTripped.height).toBe(original.height);
  });
});
