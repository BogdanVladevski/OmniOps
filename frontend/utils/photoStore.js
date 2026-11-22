// Keeps photos taken without signal until they are uploaded. The camera's own file may be cleaned up by the
// phone at any time, so each photo is copied somewhere that survives an app restart:
// phones — the app's documents folder; web — the browser's IndexedDB.
import { Platform } from 'react-native';
import * as FileSystem from 'expo-file-system/legacy';

const DB = 'omniops-outbox';
const STORE = 'photos';

function idb() {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB, 1);
    req.onupgradeneeded = () => req.result.createObjectStore(STORE);
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

async function idbRun(mode, fn) {
  const db = await idb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, mode);
    const req = fn(tx.objectStore(STORE));
    tx.oncomplete = () => resolve(req?.result);
    tx.onerror = () => reject(tx.error);
  });
}

const folder = () => `${FileSystem.documentDirectory}outbox/`;

/** Stores the photo at `uri` (file, blob: or data: URI) under `id`; returns a reference for load/remove. */
export async function savePhoto(id, uri) {
  if (Platform.OS === 'web') {
    const blob = await (await fetch(uri)).blob();
    await idbRun('readwrite', (s) => s.put(blob, id));
    return `idb:${id}`;
  }
  await FileSystem.makeDirectoryAsync(folder(), { intermediates: true }).catch(() => {});
  const target = `${folder()}${id}.jpg`;
  if (uri.startsWith('data:')) {
    await FileSystem.writeAsStringAsync(target, uri.split(',')[1], { encoding: FileSystem.EncodingType.Base64 });
  } else {
    await FileSystem.copyAsync({ from: uri, to: target });
  }
  return target;
}

/** A uri the uploader and <Image> can use, or null if the photo is gone. */
export async function loadPhoto(ref) {
  if (ref?.startsWith('idb:')) {
    const blob = await idbRun('readonly', (s) => s.get(ref.slice(4)));
    return blob ? URL.createObjectURL(blob) : null;
  }
  const info = await FileSystem.getInfoAsync(ref).catch(() => null);
  return info?.exists ? ref : null;
}

export async function removePhoto(ref) {
  if (!ref) return;
  if (ref.startsWith('idb:')) {
    await idbRun('readwrite', (s) => s.delete(ref.slice(4))).catch(() => {});
    return;
  }
  await FileSystem.deleteAsync(ref, { idempotent: true }).catch(() => {});
}
