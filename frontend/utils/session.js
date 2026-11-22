// The signed-in session, shared by the API client, live updates and screens. Persisted on the device.
import AsyncStorage from '@react-native-async-storage/async-storage';

const KEY = 'omniops:session';
let current = null; // { accessToken, refreshToken, account }
const listeners = new Set();

export function getSession() {
  return current;
}

export function onSessionChange(fn) {
  listeners.add(fn);
  return () => listeners.delete(fn);
}

export async function setSession(next) {
  current = next ?? null;
  if (current) await AsyncStorage.setItem(KEY, JSON.stringify(current));
  else await AsyncStorage.removeItem(KEY);
  listeners.forEach((fn) => {
    try { fn(current); } catch { /* keep notifying others */ }
  });
}

export async function loadSession() {
  try {
    const raw = await AsyncStorage.getItem(KEY);
    current = raw ? JSON.parse(raw) : null;
  } catch {
    current = null;
  }
  return current;
}

/** Turns the API's session response ({ accessToken, refreshToken, account }) into what we store. */
export function fromResponse(body) {
  return { accessToken: body.accessToken, refreshToken: body.refreshToken, account: body.account };
}

export const isDriver = (session) => session?.account?.role === 'Driver';
export const isOwner = (session) => session?.account?.role === 'Owner';
