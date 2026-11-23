// Office data, one hook per resource. Live updates invalidate these keys; screens never poll on their own.
import { useQuery } from '@tanstack/react-query';
import { API_URL, api, unwrap } from '@/api/client';
import { keys } from '@/api/query-client';
import type { components } from '@/api/schema';
import type { Job } from '@/api/types';
import { shareLink } from '@/lib/share';

export type JobScope = NonNullable<components['schemas']['JobScope']>;

export const useJobs = (scope: JobScope = 'Active') =>
  useQuery({
    queryKey: keys.jobs(scope),
    queryFn: () => unwrap(api.GET('/api/v1/jobs', { params: { query: { scope } } })),
  });

export const useJob = (jobId: string | null | undefined) =>
  useQuery({
    queryKey: keys.job(jobId ?? ''),
    queryFn: () => unwrap(api.GET('/api/v1/jobs/{jobId}', { params: { path: { jobId: jobId! } } })),
    enabled: !!jobId,
  });

export const useJobOptions = () =>
  useQuery({ queryKey: keys.jobOptions, queryFn: () => unwrap(api.GET('/api/v1/jobs/options')) });

export const useInvoiceOverview = () =>
  useQuery({ queryKey: keys.invoices, queryFn: () => unwrap(api.GET('/api/v1/invoices')), refetchInterval: 60_000 });

export const useBillingProfile = () =>
  useQuery({ queryKey: keys.billingProfile, queryFn: () => unwrap(api.GET('/api/v1/billing/profile')), retry: false });

export const useDemoStatus = () =>
  useQuery({ queryKey: keys.demoStatus, queryFn: () => unwrap(api.GET('/api/v1/demo/status')), staleTime: Infinity });

/** The customer's tracking page for a job, shared through the phone's share sheet or copied on the web. */
export function shareTrackingLink(job: Pick<Job, 'trackingPath' | 'reference' | 'customerName'>) {
  if (!job.trackingPath) return Promise.resolve('This job has no tracking link.');
  const what = job.reference ? `order ${job.reference}` : 'your delivery';
  return shareLink(`${API_URL}${job.trackingPath}`, `Follow ${what} live:`);
}

export const callNumber = (phone: string) => `tel:${phone.replace(/[^+\d]/g, '')}`;
