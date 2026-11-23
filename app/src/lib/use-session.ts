import { useSyncExternalStore } from 'react';
import { getSession, onSessionChange } from './session';

/** The current session; re-renders when someone signs in or out. */
export const useSession = () => useSyncExternalStore(onSessionChange, getSession, getSession);
