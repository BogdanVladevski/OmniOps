// "Is this photo readable?" Checked on the phone before a CMR or delivery photo is saved, so a blurry or dark picture
// is retaken at the dock instead of being discovered when the customer disputes the invoice.
import { ImageManipulator, SaveFormat } from 'expo-image-manipulator';
import { base64ToBytes, decodeJpeg } from './jpeg';

// Measured on a 320 px wide copy. Deliberately lenient: this only ever warns; the driver can keep the photo.
export const DARK_BELOW = 45; // mean brightness 0–255
export const BLURRY_BELOW = 60; // variance of the Laplacian (edge strength)
const SAMPLE_WIDTH = 320;

export type PhotoVerdict = 'ok' | 'dark' | 'blurry';

/** Brightness and sharpness of an RGBA image, and a verdict. */
export function assessPixels(rgba: Uint8Array, width: number, height: number) {
  const gray = new Float32Array(width * height);
  let sum = 0;
  for (let i = 0, p = 0; i < gray.length; i++, p += 4) {
    const g = 0.299 * rgba[p]! + 0.587 * rgba[p + 1]! + 0.114 * rgba[p + 2]!;
    gray[i] = g;
    sum += g;
  }
  const brightness = sum / gray.length;

  // Laplacian: how much each pixel differs from its four neighbours. Sharp text and edges give large values.
  let n = 0;
  let mean = 0;
  let m2 = 0;
  for (let y = 1; y < height - 1; y++) {
    for (let x = 1; x < width - 1; x++) {
      const i = y * width + x;
      const lap = gray[i - 1]! + gray[i + 1]! + gray[i - width]! + gray[i + width]! - 4 * gray[i]!;
      n += 1;
      const d = lap - mean;
      mean += d / n;
      m2 += d * (lap - mean);
    }
  }
  const sharpness = n > 1 ? m2 / (n - 1) : 0;
  const verdict: PhotoVerdict = brightness < DARK_BELOW ? 'dark' : sharpness < BLURRY_BELOW ? 'blurry' : 'ok';
  return { brightness, sharpness, verdict };
}

/** Checks a photo by its uri. Never throws: if the check itself fails, the photo is treated as fine. */
export async function checkPhoto(uri: string): Promise<{ verdict: PhotoVerdict }> {
  try {
    const image = await ImageManipulator.manipulate(uri).resize({ width: SAMPLE_WIDTH }).renderAsync();
    const small = await image.saveAsync({ base64: true, compress: 0.95, format: SaveFormat.JPEG });
    if (!small.base64) return { verdict: 'ok' };
    const { data, width, height } = decodeJpeg(base64ToBytes(small.base64));
    return assessPixels(data, width, height);
  } catch {
    return { verdict: 'ok' };
  }
}
