import type { InvoiceSummary, Job } from '@/api/types';
import { searchRecords } from './search';

const jobs = [
  {
    id: '1',
    customerName: 'Žito Bitola',
    reference: 'PO-1042',
    destination: 'Bitola',
    vehicleName: 'Truck-001',
    driverName: 'Marko',
    status: 'InProgress',
    createdAtUtc: '2026-11-22T08:00:00Z',
  },
  {
    id: '2',
    customerName: 'Tinex',
    reference: 'PO-77',
    destination: 'Kumanovo',
    vehicleName: 'Truck-002',
    status: 'Delivered',
    createdAtUtc: '2026-11-21T08:00:00Z',
  },
] as Job[];
const invoices = [
  {
    id: 'i1',
    number: 'INV-2026-0007',
    customerName: 'Tinex',
    total: 250,
    currency: 'EUR',
    status: 'Issued',
    isOverdue: true,
  },
] as InvoiceSummary[];

const ids = (results: ReturnType<typeof searchRecords>) => results.map((r) => r.href.params.open);

describe('search', () => {
  it('finds a job by any mix of customer, order, place, truck or driver', () => {
    expect(ids(searchRecords({ jobs }, 'bitola 1042'))).toEqual(['1']);
    expect(ids(searchRecords({ jobs }, 'marko'))).toEqual(['1']);
    expect(ids(searchRecords({ jobs }, 'zito'))).toEqual(['1']); // without the accent
  });

  it('finds invoices by number or customer, and says if they are overdue', () => {
    const results = searchRecords({ jobs, invoices }, 'tinex');
    expect(results.map((r) => r.href.pathname)).toEqual(['/jobs', '/invoices']);
    expect(results[1]!.subtitle).toMatch(/overdue/);
    expect(ids(searchRecords({ invoices }, '0007'))).toEqual(['i1']);
  });

  it('waits for at least two characters', () => {
    expect(searchRecords({ jobs }, 't')).toEqual([]);
  });
});
