// Tiny in-app signal so screens can refresh as soon as data changes elsewhere (no polling delay).
const listeners = new Set();

/** Call after creating or changing delivery jobs. */
export function notifyJobsChanged() {
  listeners.forEach((fn) => {
    try { fn(); } catch { /* one bad listener must not stop the others */ }
  });
}

/** Subscribe; returns the unsubscribe function. */
export function onJobsChanged(fn) {
  listeners.add(fn);
  return () => listeners.delete(fn);
}
