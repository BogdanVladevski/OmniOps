// Finding a job or invoice from anywhere (Ctrl K): by customer, order number, place, truck, driver or invoice number.
import { jobStatusMeta, jobTitle } from './jobs';
import { money } from './invoices';

const norm = (s) => String(s ?? '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');

/** Every word typed must appear somewhere in the record (so "bitola 1042" finds order PO-1042 to Bitola). */
const matches = (haystack, q) => norm(q).split(/\s+/).filter(Boolean).every((w) => haystack.includes(w));

export function searchRecords({ jobs = [], invoices = [] }, query, limit = 8) {
  const q = query.trim();
  if (q.length < 2) return [];
  const jobItems = jobs
    .filter((j) => matches(norm([j.customerName, j.reference, j.destination, j.origin, j.vehicleLabel, j.registration, j.driverName].join(' ')), q))
    .sort((a, b) => new Date(b.createdAtUtc) - new Date(a.createdAtUtc))
    .map((j) => ({
      id: `job-${j.id}`,
      label: jobTitle(j),
      subtitle: [jobStatusMeta(j.status).label, j.reference && `Order ${j.reference}`, j.vehicleLabel, j.destination].filter(Boolean).join(' · '),
      action: 'job',
      jobId: j.id,
    }));
  const invoiceItems = invoices
    .filter((i) => matches(norm([i.number, i.customerName].join(' ')), q))
    .map((i) => ({
      id: `invoice-${i.id}`,
      label: `Invoice ${i.number} · ${i.customerName}`,
      subtitle: `${money(i.total, i.currency)} · ${i.status === 'Paid' ? 'paid' : i.isOverdue ? 'overdue' : 'unpaid'}`,
      action: 'invoice',
      invoiceId: i.id,
    }));
  return [...jobItems, ...invoiceItems].slice(0, limit);
}
