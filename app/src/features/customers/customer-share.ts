// Ready-made messages for handing a customer their portal link through the channels small carriers actually use:
// WhatsApp, Viber and email. No API keys needed: each is a link the phone or browser opens with the text filled in.

export function portalMessage(customerName: string, companyName: string, url: string): string {
  return (
    `Hello ${customerName}, this link shows all your deliveries from ${companyName}: where they are now, ` +
    `proof of delivery and invoices. It stays the same, so you can bookmark it: ${url}`
  );
}

/** International number digits for wa.me ("+389 70 123 456" → "38970123456"); null if there is nothing to dial. */
export function phoneDigits(phone: string | null | undefined): string | null {
  const digits = (phone ?? '').replace(/\D/g, '').replace(/^00/, '');
  return digits.length >= 6 ? digits : null;
}

/** Opens WhatsApp with the message, in the customer's chat when the number is known. */
export const whatsappUrl = (phone: string | null | undefined, text: string) =>
  `https://wa.me/${phoneDigits(phone) ?? ''}?text=${encodeURIComponent(text)}`;

/** Viber can only prefill a message for forwarding (the user picks the contact). */
export const viberUrl = (text: string) => `viber://forward?text=${encodeURIComponent(text)}`;

export const mailtoUrl = (email: string | null | undefined, subject: string, body: string) =>
  `mailto:${encodeURIComponent(email ?? '').replace('%40', '@')}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`;
