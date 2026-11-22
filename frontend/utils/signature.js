// The receiver's signature, drawn with a finger, becomes a JPEG like any other paperwork photo.
import { bytesToBase64, encodeJpeg } from './jpeg';

export const OUTPUT_WIDTH = 600;
export const OUTPUT_HEIGHT = 240;

/** Strokes are arrays of {x, y} in the pad's coordinates (padWidth × padHeight). */
export function hasInk(strokes) {
  return strokes.some((s) => s.length > 1) || strokes.reduce((n, s) => n + s.length, 0) >= 3;
}

/** Draws the strokes, black on white, scaled to fit the output. Returns RGBA pixels. */
export function rasterize(strokes, padWidth, padHeight, width = OUTPUT_WIDTH, height = OUTPUT_HEIGHT, thickness = 3) {
  const rgba = new Uint8Array(width * height * 4).fill(255);
  const scale = Math.min(width / padWidth, height / padHeight);
  const offX = (width - padWidth * scale) / 2;
  const offY = (height - padHeight * scale) / 2;
  const r = thickness / 2;
  const r2 = r * r;

  const stamp = (cx, cy) => {
    for (let y = Math.floor(cy - r); y <= Math.ceil(cy + r); y++) {
      if (y < 0 || y >= height) continue;
      for (let x = Math.floor(cx - r); x <= Math.ceil(cx + r); x++) {
        if (x < 0 || x >= width) continue;
        if ((x - cx) ** 2 + (y - cy) ** 2 <= r2) {
          const p = (y * width + x) * 4;
          rgba[p] = 20; rgba[p + 1] = 24; rgba[p + 2] = 29;
        }
      }
    }
  };

  strokes.forEach((stroke) => {
    const pts = stroke.map((pt) => ({ x: offX + pt.x * scale, y: offY + pt.y * scale }));
    if (pts.length === 1) stamp(pts[0].x, pts[0].y);
    for (let i = 1; i < pts.length; i++) {
      const a = pts[i - 1];
      const b = pts[i];
      const steps = Math.max(1, Math.ceil(Math.hypot(b.x - a.x, b.y - a.y) * 2));
      for (let s = 0; s <= steps; s++) stamp(a.x + ((b.x - a.x) * s) / steps, a.y + ((b.y - a.y) * s) / steps);
    }
  });
  return rgba;
}

/** A data: URI of the signature as a JPEG, ready to upload like a photo. */
export function signatureDataUri(strokes, padWidth, padHeight) {
  const rgba = rasterize(strokes, padWidth, padHeight);
  return `data:image/jpeg;base64,${bytesToBase64(encodeJpeg(rgba, OUTPUT_WIDTH, OUTPUT_HEIGHT, 90))}`;
}
