// The office's live channel. Changes arrive as hints ("job X changed") and screens refetch what they show, so the
// server stays the single source of truth; alerts are shown as short notices.
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import { createContext, use, useEffect, useRef, useState, type ReactNode } from 'react';
import { API_URL, refreshSession } from '@/api/client';
import { keys, queryClient } from '@/api/query-client';
import { getSession } from '@/lib/session';

export interface LiveAlert {
  id: string;
  kind: string;
  title: string;
  message: string;
  jobId: string | null;
  at: string;
}

export type LiveState = 'connecting' | 'live' | 'offline';

interface LiveValue {
  state: LiveState;
  alerts: LiveAlert[];
  dismiss: (id: string) => void;
}

const LiveContext = createContext<LiveValue>({ state: 'offline', alerts: [], dismiss: () => {} });
export const useLive = () => use(LiveContext);

/** Many GPS points arrive per minute; refetching once per second is plenty. */
const REFETCH_EVERY_MS = 1000;

export function LiveUpdatesProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<LiveState>('connecting');
  const [alerts, setAlerts] = useState<LiveAlert[]>([]);
  const changed = useRef(new Set<string>());
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    const flush = () => {
      timer.current = null;
      const ids = [...changed.current];
      changed.current.clear();
      void queryClient.invalidateQueries({ queryKey: keys.allJobs });
      void queryClient.invalidateQueries({ queryKey: keys.invoices });
      ids.forEach((id) => void queryClient.invalidateQueries({ queryKey: keys.job(id) }));
    };
    const onChange = (jobId: string) => {
      changed.current.add(jobId);
      timer.current ??= setTimeout(flush, REFETCH_EVERY_MS);
    };

    const connection: HubConnection = new HubConnectionBuilder()
      .withUrl(`${API_URL}/hubs/live`, { accessTokenFactory: () => getSession()?.accessToken ?? '' })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (ctx) => Math.min(30_000, 1000 * 2 ** ctx.previousRetryCount),
      })
      .configureLogging(LogLevel.None)
      .build();

    connection.on('job-changed', (change: { jobId: string }) => onChange(change.jobId));
    connection.on('alert', (alert: Omit<LiveAlert, 'id'>) => {
      setAlerts((list) => [{ ...alert, id: `${alert.at}:${alert.kind}:${alert.jobId}` }, ...list].slice(0, 5));
      if (alert.jobId) onChange(alert.jobId);
    });
    connection.onreconnecting(() => setState('connecting'));
    connection.onreconnected(() => {
      setState('live');
      flush(); // anything may have changed while we were away
    });
    connection.onclose(() => setState('offline'));

    let stopped = false;
    const start = async (attempt = 0): Promise<void> => {
      try {
        await connection.start();
        if (!stopped) setState('live');
      } catch {
        if (stopped) return;
        setState('offline');
        // The access token may simply have expired: renew once, then back off.
        if (attempt === 0) await refreshSession();
        setTimeout(() => !stopped && void start(attempt + 1), Math.min(30_000, 2000 * 2 ** attempt));
      }
    };
    void start();

    return () => {
      stopped = true;
      if (timer.current) clearTimeout(timer.current);
      if (connection.state !== HubConnectionState.Disconnected) void connection.stop();
    };
  }, []);

  const dismiss = (id: string) => setAlerts((list) => list.filter((a) => a.id !== id));
  return <LiveContext value={{ state, alerts, dismiss }}>{children}</LiveContext>;
}
