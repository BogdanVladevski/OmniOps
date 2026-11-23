// Keeps photos taken without signal until they are uploaded. The camera's own file may be cleaned up by the phone at
// any time, so each photo is copied somewhere that survives a restart: the app's documents folder, or IndexedDB on the web.
import { Directory, File, Paths } from 'expo-file-system';
import { Platform } from 'react-native';
import { base64ToBytes } from './jpeg';

const DB = 'omniops-outbox';
const STORE = 'photos';

function idb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB, 1);
    request.onupgradeneeded = () => request.result.createObjectStore(STORE);
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

async function idbRun<T>(mode: IDBTransactionMode, work: (store: IDBObjectStore) => IDBRequest<T>): Promise<T> {
  const db = await idb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, mode);
    const request = work(tx.objectStore(STORE));
    tx.oncomplete = () => resolve(request.result);
    tx.onerror = () => reject(tx.error);
  });
}

const folder = () => new Directory(Paths.document, 'outbox');

/** Stores the photo at `uri` (file, blob: or data: URI) under `id`; returns a reference for load/remove. */
export async function savePhoto(id: string, uri: string): Promise<string> {
  if (Platform.OS === 'web') {
    const blob = await (await fetch(uri)).blob();
    await idbRun('readwrite', (s) => s.put(blob, id));
    return `idb:${id}`;
  }
  const dir = folder();
  if (!dir.exists) dir.create({ intermediates: true });
  const target = new File(dir, `${id}.jpg`);
  if (uri.startsWith('data:')) await target.write(base64ToBytes(uri.split(',')[1] ?? ''));
  else await new File(uri).copy(target);
  return target.uri;
}

/** A uri the uploader and <Image> can use, or null if the photo is gone. */
export async function loadPhoto(ref: string): Promise<string | null> {
  if (ref.startsWith('idb:')) {
    const blob = await idbRun<Blob | undefined>('readonly', (s) => s.get(ref.slice(4)));
    return blob ? URL.createObjectURL(blob) : null;
  }
  return new File(ref).exists ? ref : null;
}

export async function removePhoto(ref: string | null): Promise<void> {
  if (!ref) return;
  try {
    if (ref.startsWith('idb:')) await idbRun('readwrite', (s) => s.delete(ref.slice(4)));
    else new File(ref).delete();
  } catch {
    // Already gone.
  }
}
