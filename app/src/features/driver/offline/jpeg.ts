// JPEG encode/decode in plain JavaScript (the same on phones and the web).
import { Buffer } from 'buffer';
import jpeg from 'jpeg-js';

// jpeg-js's encoder returns a Node Buffer; React Native and browsers don't have one built in.
if (typeof globalThis.Buffer === 'undefined') (globalThis as { Buffer?: typeof Buffer }).Buffer = Buffer;

export const decodeJpeg = (bytes: Uint8Array) =>
  jpeg.decode(bytes, { useTArray: true, formatAsRGBA: true, maxResolutionInMP: 30 });

/** rgba: width × height × 4 bytes. Returns JPEG bytes. */
export const encodeJpeg = (rgba: Uint8Array, width: number, height: number, quality = 85) =>
  new Uint8Array(jpeg.encode({ data: rgba, width, height }, quality).data);

export const bytesToBase64 = (bytes: Uint8Array) => Buffer.from(bytes).toString('base64');
export const base64ToBytes = (b64: string) => new Uint8Array(Buffer.from(b64, 'base64'));
