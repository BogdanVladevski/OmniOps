// JPEG encode/decode in plain JavaScript (works the same on phones and the web).
import { Buffer } from 'buffer';
import jpeg from 'jpeg-js';

// jpeg-js's encoder returns a Node Buffer; React Native and browsers don't have one built in.
if (typeof globalThis.Buffer === 'undefined') globalThis.Buffer = Buffer;

export function decodeJpeg(bytes) {
  return jpeg.decode(bytes, { useTArray: true, formatAsRGBA: true, maxResolutionInMP: 30 });
}

/** rgba: Uint8Array of width*height*4. Returns JPEG bytes. */
export function encodeJpeg(rgba, width, height, quality = 85) {
  return new Uint8Array(jpeg.encode({ data: rgba, width, height }, quality).data);
}

export const bytesToBase64 = (bytes) => Buffer.from(bytes).toString('base64');
export const base64ToBytes = (b64) => new Uint8Array(Buffer.from(b64, 'base64'));
