import { QueryClient } from '@tanstack/react-query';
import { ApiError } from './client';

/** Retries only what may succeed later (no connection, server trouble), never a refusal. */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 10_000,
      retry: (failures, error) => failures < 2 && !(error instanceof ApiError && error.status < 500),
    },
    mutations: { retry: false },
  },
});

/** Query keys in one place, so screens invalidate exactly what changed. */
export const keys = {
  jobs: (scope: string) => ['jobs', scope] as const,
  allJobs: ['jobs'] as const,
  job: (id: string) => ['job', id] as const,
  jobOptions: ['job-options'] as const,
  vehicles: ['vehicles'] as const,
  drivers: ['drivers'] as const,
  invoices: ['invoices'] as const,
  invoice: (id: string) => ['invoice', id] as const,
  billingProfile: ['billing-profile'] as const,
  team: ['team'] as const,
  audit: ['audit'] as const,
  demoStatus: ['demo-status'] as const,
};
