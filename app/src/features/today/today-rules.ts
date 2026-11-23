// The office's "Today": what needs someone now, in plain words, each with one next step.
import type { Job } from '@/api/types';
import { clock } from '@/lib/time';
import type { Tone } from '@/theme/tokens';
import {
  formatCelsius,
  formatRange,
  isOutOfRange,
  isPositionStale,
  isRefrigerated,
  jobTitle,
} from '../jobs/job-format';
import { formatDuration, minutesSince } from '../jobs/waiting';

/** Within this long before free waiting time ends, the office gets a heads-up. */
export const WAIT_WARNING_MINUTES = 15;

export type AttentionAction = 'call' | 'open' | 'link';

export interface AttentionItem {
  key: string;
  jobId: string;
  rank: number;
  tone: Tone;
  title: string;
  detail: string;
  action: AttentionAction;
}

const active = (job: Job) => job.status === 'Planned' || job.status === 'InProgress';

/** Items for "Needs you", most urgent first. */
export function attentionItems(jobs: Job[], { now = Date.now(), freeWaitingMinutes = 120 } = {}): AttentionItem[] {
  const items: AttentionItem[] = [];
  const add = (job: Job, rank: number, tone: Tone, title: string, detail: string, action: AttentionAction) =>
    items.push({ key: `${job.id}:${rank}`, jobId: job.id, rank, tone, title, detail, action });

  for (const job of jobs) {
    const name = jobTitle(job);
    const truck = job.vehicleName || 'The truck';

    if (active(job) && isRefrigerated(job) && job.temperatureOutOfRangeCount > 0) {
      const last = job.lastTemperatureCelsius;
      add(
        job,
        0,
        'critical',
        `Load outside its temperature range: ${name}`,
        isOutOfRange(job, last) && job.lastTemperatureAtUtc
          ? `Last reading ${formatCelsius(last)} at ${clock(job.lastTemperatureAtUtc)}; must be ${formatRange(job)}.`
          : `A reading was outside ${formatRange(job)}. The latest, ${formatCelsius(last)}, is back in range.`,
        'call',
      );
    } else if (
      job.status === 'InProgress' &&
      isRefrigerated(job) &&
      job.temperatureCheckDueUtc &&
      new Date(job.temperatureCheckDueUtc).getTime() <= now
    ) {
      add(
        job,
        3,
        'warning',
        `Temperature check overdue: ${name}`,
        job.lastTemperatureAtUtc
          ? `Last logged ${clock(job.lastTemperatureAtUtc)}. Remind the driver to log it.`
          : 'Nothing logged since the job started.',
        'call',
      );
    }

    if (isPositionStale(job, now) && job.lastPositionAtUtc) {
      add(
        job,
        1,
        'warning',
        `No signal from ${truck}`,
        `Last position at ${clock(job.lastPositionAtUtc)}, on the way to ${job.destination ?? name}. The app may be closed on the phone.`,
        'call',
      );
    }

    const waits = [
      job.status === 'Planned' && job.pickupArrivedAtUtc
        ? { at: job.pickupArrivedAtUtc, where: job.origin ?? 'the pickup', what: 'loading' }
        : null,
      job.status === 'InProgress' && job.deliveryArrivedAtUtc
        ? { at: job.deliveryArrivedAtUtc, where: job.destination ?? name, what: 'unloading' }
        : null,
    ].filter((w): w is { at: string; where: string; what: string } => w !== null);
    for (const w of waits) {
      const waited = minutesSince(w.at, now);
      const endsAt = new Date(new Date(w.at).getTime() + freeWaitingMinutes * 60_000).toISOString();
      if (waited >= freeWaitingMinutes) {
        add(
          job,
          2,
          'warning',
          `${truck} waiting at ${w.where} for ${formatDuration(waited)}`,
          `Free ${w.what} time ended at ${clock(endsAt)}; waiting is chargeable from then. Let ${name} know.`,
          'open',
        );
      } else if (waited >= freeWaitingMinutes - WAIT_WARNING_MINUTES) {
        add(
          job,
          5,
          'neutral',
          `Free ${w.what} time ends at ${clock(endsAt)}: ${w.where}`,
          `${truck} arrived at ${clock(w.at)}.`,
          'open',
        );
      }
    }

    if (job.exceptionCount > 0 && job.status !== 'Cancelled' && !job.invoiceId) {
      add(
        job,
        4,
        'warning',
        `Problem reported: ${name}`,
        `The driver reported ${job.exceptionCount === 1 ? 'a problem' : `${job.exceptionCount} problems`} with photos. Check before invoicing.`,
        'open',
      );
    }

    const planned = job.plannedDeliveryUtc ? new Date(job.plannedDeliveryUtc).getTime() : null;
    if (active(job) && planned != null && planned < now && !job.deliveryArrivedAtUtc) {
      const eta =
        job.etaUtc && new Date(job.etaUtc).getTime() > planned ? ` Now expected around ${clock(job.etaUtc)}.` : '';
      add(
        job,
        6,
        'warning',
        `Late: ${name}`,
        `Due at ${clock(job.plannedDeliveryUtc!)}.${eta} Tell the customer.`,
        'link',
      );
    } else if (job.status === 'Planned' && planned != null && planned - now < 2 * 3_600_000) {
      add(job, 7, 'neutral', `Not started yet: ${name}`, `Due at ${clock(job.plannedDeliveryUtc!)}.`, 'call');
    }

    if (job.status === 'Planned' && !job.driverId) {
      add(job, 8, 'neutral', `No driver for ${name}`, 'Assign a driver so it appears on their phone.', 'open');
    }
  }

  return items.sort((a, b) => a.rank - b.rank);
}

/** Trucks on the road: late and silent ones first, then by arrival. */
export function movingJobs(jobs: Job[], now = Date.now()): Job[] {
  const late = (j: Job) => !!j.plannedDeliveryUtc && new Date(j.plannedDeliveryUtc).getTime() < now;
  const eta = (j: Job) => (j.etaUtc ? new Date(j.etaUtc).getTime() : Number.MAX_SAFE_INTEGER);
  return jobs
    .filter((j) => j.status === 'InProgress')
    .sort(
      (a, b) =>
        Number(late(b)) - Number(late(a)) ||
        Number(isPositionStale(b, now)) - Number(isPositionStale(a, now)) ||
        eta(a) - eta(b),
    );
}

export interface SetupStep {
  key: string;
  title: string;
  done: boolean;
  href: '/fleet' | '/jobs' | '/settings' | '/invoices';
}

/** The first-week checklist for a new company: ticked off as each thing exists. */
export function setupSteps({
  vehicles = 0,
  drivers = 0,
  jobs = [] as Pick<Job, 'status'>[],
  invoiceSettingsDone = false,
}): SetupStep[] {
  return [
    { key: 'truck', title: 'Add your trucks', done: vehicles > 0, href: '/fleet' },
    { key: 'driver', title: 'Add your drivers', done: drivers > 0, href: '/fleet' },
    { key: 'job', title: 'Create the first delivery job', done: jobs.length > 0, href: '/jobs' },
    {
      key: 'phone',
      title: "Connect a driver's phone and start a job",
      done: jobs.some((j) => j.status !== 'Planned'),
      href: '/fleet',
    },
    {
      key: 'invoice',
      title: 'Fill in invoice settings (bank account, VAT)',
      done: invoiceSettingsDone,
      href: '/invoices',
    },
  ];
}
