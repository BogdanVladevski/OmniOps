// Invitation and password-reset links open the app as ?join=… or ?reset=… (web).
import { Platform } from 'react-native';

/** { kind: 'join' | 'reset', token } from the address bar, or null. */
export function pendingLink() {
  if (Platform.OS !== 'web' || typeof window === 'undefined') return null;
  const params = new URLSearchParams(window.location.search);
  for (const kind of ['join', 'reset']) {
    const token = params.get(kind);
    if (token) return { kind, token };
  }
  return null;
}

/** Removes the token from the address bar so it isn't reused or bookmarked. */
export function clearPendingLink() {
  if (Platform.OS !== 'web' || typeof window === 'undefined') return;
  const url = new URL(window.location.href);
  url.searchParams.delete('join');
  url.searchParams.delete('reset');
  window.history.replaceState(null, '', url.pathname + (url.search === '?' ? '' : url.search) + url.hash);
}
