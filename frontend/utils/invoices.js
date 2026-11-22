// Presentation and arithmetic for invoices. Rounding matches the server: 2 decimals, half away from zero.

export function round2(n) {
  // Binary floats make 1.005 * 100 = 100.4999…; going through the decimal text (15 significant digits,
  // then exponent shift) rounds the value people actually typed, matching the server's decimal maths.
  const sign = n < 0 ? -1 : 1;
  const shifted = Number(`${Math.abs(n).toPrecision(15)}e2`);
  return sign * Number(`${Math.round(shifted)}e-2`);
}

export function money(amount, currency = 'EUR') {
  const n = Number(amount ?? 0);
  try {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(n);
  } catch {
    return `${n.toFixed(2)} ${currency}`;
  }
}

/**
 * Reads an amount the way people type it: "250", "250,50", "1.250,50" or "1,250.50".
 * When both separators appear, the last one is the decimal separator. Returns null if not a positive amount.
 */
export function parseAmount(text) {
  let s = String(text ?? '').trim().replace(/\s/g, '').replace(/[€$]|EUR|MKD/gi, '');
  if (!s) return null;
  const lastComma = s.lastIndexOf(',');
  const lastDot = s.lastIndexOf('.');
  if (lastComma > -1 && lastDot > -1) {
    const decimal = lastComma > lastDot ? ',' : '.';
    const thousands = decimal === ',' ? '.' : ',';
    s = s.split(thousands).join('').replace(decimal, '.');
  } else if (lastComma > -1) {
    // "1,250" (thousands) vs "250,5" (decimal): three digits after a single comma means thousands.
    s = /^\d{1,3}(,\d{3})+$/.test(s) ? s.replace(/,/g, '') : s.replace(',', '.');
  }
  if (!/^\d+(\.\d{1,2})?$/.test(s)) return null;
  const n = Number(s);
  return n > 0 ? n : null;
}

export function lineTotals(lines) {
  const rows = (lines ?? []).map((l) => {
    const net = round2((Number(l.quantity) || 0) * (Number(l.unitPrice) || 0));
    return { net, vat: round2((net * (Number(l.vatRatePercent) || 0)) / 100) };
  });
  const subtotal = round2(rows.reduce((s, r) => s + r.net, 0));
  const vat = round2(rows.reduce((s, r) => s + r.vat, 0));
  return { subtotal, vat, total: round2(subtotal + vat) };
}

const day = (iso) => new Date(iso).toLocaleDateString([], { day: 'numeric', month: 'short' });

/** { label, tone } for an invoice summary. */
export function invoiceStatusMeta(inv) {
  if (!inv) return { label: '', tone: 'neutral' };
  if (inv.status === 'Paid') return { label: `Paid ${inv.paidAtUtc ? day(inv.paidAtUtc) : ''}`.trim(), tone: 'ok' };
  if (inv.isOverdue) return { label: `Overdue ${inv.daysOverdue} day${inv.daysOverdue === 1 ? '' : 's'}`, tone: 'critical' };
  if (inv.status === 'Issued') return { label: 'Not sent yet', tone: 'warning' };
  return { label: `Due ${day(inv.dueDateUtc)}`, tone: 'neutral' };
}

const REMINDER_LABEL = {
  DueSoon: 'Due-soon reminder',
  DueToday: 'Due-today reminder',
  Overdue7: 'First overdue reminder',
  Overdue14: 'Second overdue reminder',
};

export function reminderText(r) {
  const what = REMINDER_LABEL[r.kind] ?? 'Reminder';
  const when = new Date(r.sentAtUtc).toLocaleDateString([], { day: 'numeric', month: 'short' });
  return r.emailed ? `${what} emailed ${when}` : `${what} due ${when} — no email address, send it yourself`;
}
