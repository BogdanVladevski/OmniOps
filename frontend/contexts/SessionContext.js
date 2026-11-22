// Who is signed in on this device. Screens re-render when the session starts, renews or ends.
import React, { createContext, useContext, useEffect, useState } from 'react';
import { getSession, loadSession, onSessionChange } from '../utils/session';

const SessionContext = createContext({ session: null, ready: false });

export function SessionProvider({ children }) {
  const [session, setSessionState] = useState(getSession());
  const [ready, setReady] = useState(false);

  useEffect(() => {
    const off = onSessionChange(setSessionState);
    loadSession().then((s) => { setSessionState(s); setReady(true); });
    return off;
  }, []);

  return <SessionContext.Provider value={{ session, ready }}>{children}</SessionContext.Provider>;
}

export const useSession = () => useContext(SessionContext);
