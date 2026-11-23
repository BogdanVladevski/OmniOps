import { assessPixels } from './photo-quality';
import { rasterize, hasInk, signatureDataUri } from './signature';
import { base64ToBytes, decodeJpeg } from './jpeg';

jest.mock('expo-image-manipulator', () => ({
  ImageManipulator: { manipulate: jest.fn() },
  SaveFormat: { JPEG: 'jpeg' },
}));

const W = 160;
const H = 120;

function image(fn: (x: number, y: number) => number) {
  const rgba = new Uint8Array(W * H * 4);
  for (let y = 0; y < H; y++) {
    for (let x = 0; x < W; x++) {
      const v = fn(x, y);
      const p = (y * W + x) * 4;
      rgba[p] = v;
      rgba[p + 1] = v;
      rgba[p + 2] = v;
      rgba[p + 3] = 255;
    }
  }
  return rgba;
}

// A photographed page: grey print on off-white paper (phone photos rarely reach pure black and white).
const page = (x: number, y: number) => ((y % 12 < 2 && x % 40 > 4) || (x % 7 === 0 && y % 12 < 9) ? 120 : 200);

function boxBlur(rgba: Uint8Array, radius: number) {
  const out = new Uint8Array(rgba.length);
  for (let y = 0; y < H; y++) {
    for (let x = 0; x < W; x++) {
      let s = 0;
      let n = 0;
      for (let dy = -radius; dy <= radius; dy++) {
        for (let dx = -radius; dx <= radius; dx++) {
          const xx = Math.min(W - 1, Math.max(0, x + dx));
          const yy = Math.min(H - 1, Math.max(0, y + dy));
          s += rgba[(yy * W + xx) * 4]!;
          n++;
        }
      }
      const p = (y * W + x) * 4;
      out[p] = out[p + 1] = out[p + 2] = s / n;
      out[p + 3] = 255;
    }
  }
  return out;
}

describe('assessPixels', () => {
  it('passes a sharp, well-lit document', () => {
    expect(assessPixels(image(page), W, H).verdict).toBe('ok');
  });

  it('flags a blurred document', () => {
    const blurred = boxBlur(image(page), 4);
    const { verdict, sharpness } = assessPixels(blurred, W, H);
    expect(verdict).toBe('blurry');
    expect(sharpness).toBeLessThan(assessPixels(image(page), W, H).sharpness / 10);
  });

  it('flags a photo taken in the dark', () => {
    expect(
      assessPixels(
        image((x: number, y: number) => page(x, y) * 0.12),
        W,
        H,
      ).verdict,
    ).toBe('dark');
  });
});

describe('signature', () => {
  const stroke = [
    { x: 10, y: 50 },
    { x: 60, y: 20 },
    { x: 120, y: 80 },
    { x: 190, y: 40 },
  ];

  it('needs some ink', () => {
    expect(hasInk([])).toBe(false);
    expect(hasInk([[{ x: 1, y: 1 }]])).toBe(false);
    expect(hasInk([stroke])).toBe(true);
  });

  it('draws the strokes dark on white', () => {
    const rgba = rasterize([stroke], 200, 100, 200, 100);
    let dark = 0;
    for (let p = 0; p < rgba.length; p += 4) if (rgba[p]! < 100) dark++;
    expect(dark).toBeGreaterThan(200);
    expect(dark).toBeLessThan(200 * 100 * 0.1);
  });

  it('becomes a JPEG that decodes to the expected size', () => {
    const uri = signatureDataUri([stroke], 300, 120);
    expect(uri.startsWith('data:image/jpeg;base64,')).toBe(true);
    const { width, height } = decodeJpeg(base64ToBytes(uri.split(',')[1]!));
    expect([width, height]).toEqual([600, 240]);
  });
});
