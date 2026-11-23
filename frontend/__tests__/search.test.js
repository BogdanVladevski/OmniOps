import { searchRecords } from '../utils/search';

const jobs = [
  { id: '1', customerName: 'Žito Bitola', reference: 'PO-1042', destination: 'Bitola', vehicleLabel: 'Truck-001', driverName: 'Marko', status: 'InProgress', createdAtUtc: '2026-11-22T08:00:00Z' },
  { id: '2', customerName: 'Tinex', reference: 'PO-77', destination: 'Kumanovo', vehicleLabel: 'Truck-002', status: 'Completed', createdAtUtc: '2026-11-21T08:00:00Z' },
];
const invoices = [{ id: 'i1', number: 'INV-2026-0007', customerName: 'Tinex', total: 250, currency: 'EUR', status: 'Issued', isOverdue: true }];

describe('search', () => {
  it('finds a job by any mix of customer, order, place, truck or driver', () => {
    expect(searchRecords({ jobs }, 'bitola 1042').map((r) => r.jobId)).toEqual(['1']);
    expect(searchRecords({ jobs }, 'marko').map((r) => r.jobId)).toEqual(['1']);
    expect(searchRecords({ jobs }, 'zito').map((r) => r.jobId)).toEqual(['1']); // without the accent
  });

  it('finds invoices by number or customer, and says if they are overdue', () => {
    const results = searchRecords({ jobs, invoices }, 'tinex');
    expect(results.map((r) => r.action)).toEqual(['job', 'invoice']);
    expect(results[1].subtitle).toMatch(/overdue/);
    expect(searchRecords({ invoices }, '0007')[0].invoiceId).toBe('i1');
  });

  it('waits for at least two characters', () => {
    expect(searchRecords({ jobs }, 't')).toEqual([]);
  });
});
