// The office's "Today": what needs someone now, said in plain words, each with one next step.
import { formatCelsius, formatRange, isOutOfRange, isPositionStale, isRefrigerated, jobTitle } from './jobs';
import { formatDuration, minutesSince } from './waiting';

const clock = (iso) => new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
const active = (job) => job.status === 'Planned' || job.status === 'InProgress';

/** Within this long before free waiting time ends, the office gets a heads-up. */
export const WAIT_WARNING_MINUTES = 15;

/**
 * Items for the "Needs you" list, most urgent first. Each: { key, jobId, tone, title, detail, action }
 * where action is 'call' (phone the driver), 'open' (look at the job) or 'link' (send the customer the tracking link).
 */
export function attentionItems(jobs, { now = Date.now(), freeWaitingMinutes = 120 } = {}) {
  const items = [];
  const add = (job, rank, tone, title, detail, action) =>
    items.push({ key: `${job.id}:${rank}`, jobId: job.id, rank, tone, title, detail, action });

  for (const job of jobs ?? []) {
    const name = jobTitle(job);
    const truck = job.vehicleLabel || 'The truck';

    if (active(job) && isRefrigerated(job) && job.temperatureOutOfRangeCount > 0) {
      const last = job.lastTemperatureCelsius;
      add(job, 0, 'critical', `Load outside its temperature range — ${name}`,
        isOutOfRange(job, last)
          ? `Last reading ${formatCelsius(last)} at ${clock(job.lastTemperatureAtUtc)}; must be ${formatRange(job)}.`
          : `A reading was outside ${formatRange(job)}. The latest, ${formatCelsius(last)}, is back in range.`,
        'call');
    } else if (job.status === 'InProgress' && isRefrigerated(job) && job.temperatureCheckDueUtc
      && new Date(job.temperatureCheckDueUtc).getTime() <= now) {
      add(job, 3, 'warning', `Temperature check overdue — ${name}`,
        job.lastTemperatureAtUtc ? `Last logged ${clock(job.lastTemperatureAtUtc)}. Remind the driver to log it.` : 'Nothing logged since the trip started.',
        'call');
    }

    if (isPositionStale(job, now)) {
      add(job, 1, 'warning', `No signal from ${truck}`,
        `Last position at ${clock(job.lastPositionAtUtc)}, on the way to ${job.destination ?? name}. The app may be closed on the phone.`,
        'call');
    }

    const waits = [
      job.status === 'Planned' && job.pickupArrivedAtUtc && { at: job.pickupArrivedAtUtc, where: job.origin ?? 'the pickup', what: 'loading' },
      job.status === 'InProgress' && job.deliveryArrivedAtUtc && { at: job.deliveryArrivedAtUtc, where: job.destination ?? name, what: 'unloading' },
    ].filter(Boolean);
    for (const w of waits) {
      const waited = minutesSince(w.at, now);
      const endsAt = new Date(new Date(w.at).getTime() + freeWaitingMinutes * 60000).toISOString();
      if (waited >= freeWaitingMinutes) {
        add(job, 2, 'warning', `${truck} waiting at ${w.where} for ${formatDuration(waited)}`,
          `Free ${w.what} time ended at ${clock(endsAt)}; waiting is chargeable from then. Let ${name} know.`, 'open');
      } else if (waited >= freeWaitingMinutes - WAIT_WARNING_MINUTES) {
        add(job, 5, 'neutral', `Free ${w.what} time ends at ${clock(endsAt)} — ${w.where}`,
          `${truck} arrived at ${clock(w.at)}.`, 'open');
      }
    }

    if (job.exceptionCount > 0 && job.status !== 'Cancelled' && !job.invoiceId) {
      add(job, 4, 'warning', `Problem reported — ${name}`,
        `The driver reported ${job.exceptionCount === 1 ? 'a problem' : `${job.exceptionCount} problems`} with photos. Check before invoicing.`, 'open');
    }

    const planned = job.plannedDeliveryUtc ? new Date(job.plannedDeliveryUtc).getTime() : null;
    if (active(job) && planned && planned < now && !job.deliveryArrivedAtUtc) {
      const eta = job.etaUtc && new Date(job.etaUtc).getTime() > planned ? ` Now expected around ${clock(job.etaUtc)}.` : '';
      add(job, 6, 'warning', `Late — ${name}`, `Due at ${clock(job.plannedDeliveryUtc)}.${eta} Tell the customer.`, 'link');
    } else if (job.status === 'Planned' && planned && planned - now < 2 * 3600000) {
      add(job, 7, 'neutral', `Not started yet — ${name}`, `Due at ${clock(job.plannedDeliveryUtc)}.`, 'call');
    }

    if (job.status === 'Planned' && !job.driverId) {
      add(job, 8, 'neutral', `No driver for ${name}`, 'Assign a driver so it appears on their phone.', 'open');
    }
  }

  return items.sort((a, b) => a.rank - b.rank);
}

/** Moving trucks for the panel: late ones and silent ones first, then by arrival. */
export function movingJobs(jobs, now = Date.now()) {
  const late = (j) => !!j.plannedDeliveryUtc && new Date(j.plannedDeliveryUtc).getTime() < now;
  const eta = (j) => (j.etaUtc ? new Date(j.etaUtc).getTime() : Number.MAX_SAFE_INTEGER);
  return (jobs ?? []).filter((j) => j.status === 'InProgress')
    .sort((a, b) => Number(late(b)) - Number(late(a))
      || Number(isPositionStale(b, now)) - Number(isPositionStale(a, now))
      || eta(a) - eta(b));
}

/** The first-week checklist for a new company: done when each thing exists. */
export function setupSteps({ vehicles = 0, drivers = 0, jobs = [], invoiceSettingsDone = false }) {
  const started = jobs.some((j) => j.status !== 'Planned');
  return [
    { key: 'truck', title: 'Add your trucks', done: vehicles > 0, screen: 'Fleet' },
    { key: 'driver', title: 'Add your drivers', done: drivers > 0, screen: 'Fleet' },
    { key: 'job', title: 'Create the first delivery job', done: jobs.length > 0, screen: 'Jobs' },
    { key: 'phone', title: "Connect a driver's phone and start a trip", done: started, screen: 'Admin' },
    { key: 'invoice', title: 'Fill in invoice settings (bank account, VAT)', done: invoiceSettingsDone, screen: 'Invoices' },
  ];
}
