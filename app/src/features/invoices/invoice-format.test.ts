import { anInvoice } from '@/test/factories';
import { invoiceStatusMeta, lineTotals, parseAmount, reminderText, round2 } from './invoice-format';

describe('amounts', () => {
  it('reads amounts in European and English styles', () => {
    expect(parseAmount('250')).toBe(250);
    expect(parseAmount('250,50')).toBe(250.5);
    expect(parseAmount('1.250,50')).toBe(1250.5);
    expect(parseAmount('1,250.50')).toBe(1250.5);
    expect(parseAmount('1,250')).toBe(1250);
    expect(parseAmount('€ 99,9')).toBe(99.9);
  });

  it('refuses what is not a positive amount', () => {
    ['', 'abc', '0', '-5', '12.345', '1,2,3,4'].forEach((t) => expect(parseAmount(t)).toBeNull());
  });

  it('rounds like the server: per line, half away from zero', () => {
    expect(round2(1.005)).toBe(1.01);
    expect(
      lineTotals([
        { quantity: 1, unitPrice: 10.005, vatRatePercent: 18 },
        { quantity: 3, unitPrice: 0.335, vatRatePercent: 18 },
      ]),
    ).toEqual({ subtotal: 11.02, vat: 1.98, total: 13 });
  });
});

describe('invoice status', () => {
  it('puts overdue before the due date and says how late', () => {
    expect(invoiceStatusMeta(anInvoice({ status: 'Sent', isOverdue: true, daysOverdue: 8 }))).toEqual({
      label: 'Overdue 8 days',
      tone: 'critical',
    });
  });

  it('nudges about invoices that were never sent', () => {
    expect(invoiceStatusMeta(anInvoice({ status: 'Issued', isOverdue: false })).tone).toBe('warning');
  });

  it('describes reminders that could not be emailed', () => {
    expect(reminderText({ kind: 'Overdue7', sentAtUtc: '2026-12-01T09:00:00Z', emailed: false })).toMatch(
      /send it yourself/,
    );
  });
});
