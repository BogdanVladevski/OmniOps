// The driver's jobs and outbox, shared by the job list and each job. Works without signal: jobs are kept on the
// phone, every action goes into the outbox first and is sent in order when there is a connection.
import AsyncStorage from '@react-native-async-storage/async-storage';
import { useQuery } from '@tanstack/react-query';
import { createContext, use, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { ApiError } from '@/api/client';
import type { Job } from '@/api/types';
import { driverApi } from './driver-api';
import { applyPending, outbox, type OutboxAction, type OutboxItem } from './offline/outbox';

const JOBS_KEY = 'omniops:driver-jobs';
const FLUSH_EVERY_MS = 20_000;

export type DriverJob = ReturnType<typeof applyPending>;

interface DriverValue {
  jobs: DriverJob[];
  loading: boolean;
  /** Showing the jobs saved on the phone because there is no connection. */
  offline: boolean;
  error: string | null;
  refresh: () => void;
  items: OutboxItem[];
  sending: boolean;
  /** Saves the action on the phone, then tries to send everything waiting. */
  act: (jobId: string, action: OutboxAction) => Promise<void>;
}

const DriverContext = createContext<DriverValue | null>(null);

export function useDriver(): DriverValue {
  const value = use(DriverContext);
  if (!value) throw new Error('useDriver must be used inside DriverProvider');
  return value;
}

async function savedJobs(): Promise<Job[] | null> {
  try {
    const raw = await AsyncStorage.getItem(JOBS_KEY);
    return raw ? (JSON.parse(raw) as Job[]) : null;
  } catch {
    return null;
  }
}

export function DriverProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<OutboxItem[]>([]);
  const [sending, setSending] = useState(false);
  const [offline, setOffline] = useState(false);

  const jobs = useQuery({
    queryKey: ['driver-jobs'],
    queryFn: async () => {
      try {
        const list = await driverApi.jobs();
        setOffline(false);
        AsyncStorage.setItem(JOBS_KEY, JSON.stringify(list)).catch(() => {});
        return list;
      } catch (error) {
        // No connection (not a refusal): keep working from the jobs saved on the phone.
        const saved = !(error instanceof ApiError) ? await savedJobs() : null;
        if (!saved) throw error;
        setOffline(true);
        return saved;
      }
    },
    refetchInterval: 60_000,
    retry: false,
  });

  const flush = useCallback(async () => {
    setSending(true);
    try {
      return await outbox.flush();
    } finally {
      setSending(false);
    }
  }, []);

  useEffect(() => {
    const unsubscribe = outbox.subscribe(setItems);
    const first = setTimeout(() => void flush(), 0);
    const timer = setInterval(() => void flush(), FLUSH_EVERY_MS);
    return () => {
      unsubscribe();
      clearTimeout(first);
      clearInterval(timer);
    };
  }, [flush]);

  const refetch = jobs.refetch;
  const act = useCallback(
    async (jobId: string, action: OutboxAction) => {
      await outbox.add(jobId, action);
      const result = await flush();
      if (result.sent) void refetch();
    },
    [flush, refetch],
  );

  const value = useMemo<DriverValue>(
    () => ({
      jobs: (jobs.data ?? []).map((j) => applyPending(j, items)),
      loading: jobs.isPending,
      offline,
      error: jobs.error?.message ?? null,
      refresh: () => void refetch(),
      items,
      sending,
      act,
    }),
    [jobs.data, jobs.isPending, jobs.error, offline, refetch, items, sending, act],
  );
  return <DriverContext value={value}>{children}</DriverContext>;
}
