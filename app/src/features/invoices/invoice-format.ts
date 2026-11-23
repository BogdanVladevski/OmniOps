// Invoice arithmetic and wording. Rounding matches the server: 2 decimals, half away from zero.
import type { InvoiceSummary } from '@/api/types';
import type { Tone } from '@/theme/tokens';

export function round2(n: number): number {
  // Binary floats make 1.005 * 100 = 100.4999…; going through the decimal text (15 significant digits, then an
  // exponent shift) rounds the value people actually typed, matching the server's decimal maths.
  const sign = n < 0 ? -1 : 1;
  const shifted = Number(`${Math.abs(n).toPrecision(15)}e2`);
  return sign * Number(`${Math.round(shifted)}e-2`);
}

export function money(amount: number | null | undefined, currency = 'EUR'): string {
  const n = Number(amount ?? 0);
  try {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(n);
  } catch {
    return `${n.toFixed(2)} ${currency}`;
  }
}

/**
 * Reads an amount the way people type it: "250", "250,50", "1.250,50" or "1,250.50". When both separators appear,
 * the last one is the decimal separator. Null if not a positive amount.
 */
export function parseAmount(text: string | null | undefined): number | null {
  let s = String(text ?? '')
    .trim()
    .replace(/\s/g, '')
    .replace(/[€$]|EUR|MKD/gi, '');
  if (!s) return null;
  const lastComma = s.lastIndexOf(',');
  const lastDot = s.lastIndexOf('.');
  if (lastComma > -1 && lastDot > -1) {
    const decimal = lastComma > lastDot ? ',' : '.';
    s = s
      .split(decimal === ',' ? '.' : ',')
      .join('')
      .replace(decimal, '.');
  } else if (lastComma > -1) {
    // "1,250" (thousands) vs "250,5" (decimal): three digits after a single comma means thousands.
    s = /^\d{1,3}(,\d{3})+$/.test(s) ? s.replace(/,/g, '') : s.replace(',', '.');
  }
  if (!/^\d+(\.\d{1,2})?$/.test(s)) return null;
  const n = Number(s);
  return n > 0 ? n : null;
}

export interface LineInput {
  quantity: number;
  unitPrice: number;
  vatRatePercent: number;
}

export function lineTotals(lines: LineInput[]) {
  const rows = lines.map((l) => {
    const net = round2((l.quantity || 0) * (l.unitPrice || 0));
    return { net, vat: round2((net * (l.vatRatePercent || 0)) / 100) };
  });
  const subtotal = round2(rows.reduce((s, r) => s + r.net, 0));
  const vat = round2(rows.reduce((s, r) => s + r.vat, 0));
  return { subtotal, vat, total: round2(subtotal + vat) };
}

const day = (iso: string) => new Date(iso).toLocaleDateString([], { day: 'numeric', month: 'short' });

export function invoiceStatusMeta(inv: InvoiceSummary): { label: string; tone: Tone } {
  if (inv.status === 'Paid') return { label: `Paid ${inv.paidAtUtc ? day(inv.paidAtUtc) : ''}`.trim(), tone: 'ok' };
  if (inv.isOverdue)
    return { label: `Overdue ${inv.daysOverdue} day${inv.daysOverdue === 1 ? '' : 's'}`, tone: 'critical' };
  if (inv.status === 'Issued') return { label: 'Not sent yet', tone: 'warning' };
  return { label: `Due ${day(inv.dueDateUtc)}`, tone: 'neutral' };
}

const REMINDER_LABEL: Record<string, string> = {
  DueSoon: 'Due-soon reminder',
  DueToday: 'Due-today reminder',
  Overdue7: 'First overdue reminder',
  Overdue14: 'Second overdue reminder',
};

export function reminderText(r: { kind: string; sentAtUtc: string; emailed: boolean }): string {
  const what = REMINDER_LABEL[r.kind] ?? 'Reminder';
  return r.emailed
    ? `${what} emailed ${day(r.sentAtUtc)}`
    : `${what} due ${day(r.sentAtUtc)}: no email address, send it yourself`;
}
