// The signed-in session, shared by the API client, live updates and screens. Persisted on the device.
import AsyncStorage from '@react-native-async-storage/async-storage';
import type { Account, Role } from '@/api/types';

export interface StoredSession {
  accessToken: string;
  refreshToken: string;
  account: Account;
}

const KEY = 'omniops:session';
let current: StoredSession | null = null;
const listeners = new Set<(session: StoredSession | null) => void>();

export const getSession = () => current;

export function onSessionChange(listener: (session: StoredSession | null) => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export async function setSession(next: StoredSession | null) {
  current = next;
  if (next) await AsyncStorage.setItem(KEY, JSON.stringify(next));
  else await AsyncStorage.removeItem(KEY);
  listeners.forEach((listener) => listener(current));
}

export async function loadSession(): Promise<StoredSession | null> {
  try {
    const raw = await AsyncStorage.getItem(KEY);
    current = raw ? (JSON.parse(raw) as StoredSession) : null;
  } catch {
    current = null;
  }
  return current;
}

/** Keeps only what we store from a sign-in response. */
export const fromResponse = (body: { accessToken: string; refreshToken: string; account: Account }): StoredSession => ({
  accessToken: body.accessToken,
  refreshToken: body.refreshToken,
  account: body.account,
});

export const roleOf = (session: StoredSession | null) => session?.account.role as Role | undefined;
export const isDriver = (session: StoredSession | null) => roleOf(session) === 'Driver';
export const isOwner = (session: StoredSession | null) => roleOf(session) === 'Owner';
