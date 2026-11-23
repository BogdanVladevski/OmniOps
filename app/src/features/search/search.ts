// Finding a job or invoice from anywhere: by customer, order number, place, truck, driver or invoice number.
import type { InvoiceSummary, Job } from '@/api/types';
import { money } from '../invoices/invoice-format';
import { jobStatusMeta, jobTitle } from '../jobs/job-format';

export interface SearchResult {
  id: string;
  label: string;
  subtitle: string;
  href: { pathname: '/jobs' | '/invoices'; params: Record<string, string> };
}

const norm = (s: string | null | undefined) => (s ?? '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');

/** Every word typed must appear somewhere (so "bitola 1042" finds order PO-1042 to Bitola). */
const matches = (haystack: string, query: string) =>
  norm(query)
    .split(/\s+/)
    .filter(Boolean)
    .every((w) => haystack.includes(w));

export function searchRecords(
  { jobs = [] as Job[], invoices = [] as InvoiceSummary[] },
  query: string,
  limit = 8,
): SearchResult[] {
  const q = query.trim();
  if (q.length < 2) return [];
  const jobResults = jobs
    .filter((j) =>
      matches(
        norm(
          [j.customerName, j.reference, j.destination, j.origin, j.vehicleName, j.registration, j.driverName].join(' '),
        ),
        q,
      ),
    )
    .sort((a, b) => new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime())
    .map((j) => ({
      id: `job-${j.id}`,
      label: jobTitle(j),
      subtitle: [jobStatusMeta(j.status).label, j.reference && `Order ${j.reference}`, j.vehicleName, j.destination]
        .filter(Boolean)
        .join(' · '),
      href: { pathname: '/jobs' as const, params: { open: j.id } },
    }));
  const invoiceResults = invoices
    .filter((i) => matches(norm(`${i.number} ${i.customerName}`), q))
    .map((i) => ({
      id: `invoice-${i.id}`,
      label: `Invoice ${i.number} · ${i.customerName}`,
      subtitle: `${money(i.total, i.currency)} · ${i.status === 'Paid' ? 'paid' : i.isOverdue ? 'overdue' : 'unpaid'}`,
      href: { pathname: '/invoices' as const, params: { open: i.id } },
    }));
  return [...jobResults, ...invoiceResults].slice(0, limit);
}
