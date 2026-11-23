import { mailtoUrl, phoneDigits, portalMessage, viberUrl, whatsappUrl } from './customer-share';

describe('sharing the portal link', () => {
  const text = portalMessage('Tinex', 'Vardar Transport', 'https://omniops.example/c/abc');

  it('says who it is from and that the link stays the same', () => {
    expect(text).toContain('Vardar Transport');
    expect(text).toContain('https://omniops.example/c/abc');
    expect(text).toMatch(/stays the same/);
  });

  it('turns phone numbers into WhatsApp digits', () => {
    expect(phoneDigits('+389 70 123 456')).toBe('38970123456');
    expect(phoneDigits('00389 (70) 123-456')).toBe('38970123456');
    expect(phoneDigits('call me')).toBeNull();
    expect(phoneDigits(null)).toBeNull();
  });

  it('opens the customer’s WhatsApp chat when the number is known, otherwise lets the user pick', () => {
    expect(whatsappUrl('+389 70 123 456', 'hi there')).toBe('https://wa.me/38970123456?text=hi%20there');
    expect(whatsappUrl(null, 'hi')).toBe('https://wa.me/?text=hi');
  });

  it('prefills Viber and email', () => {
    expect(viberUrl('a&b')).toBe('viber://forward?text=a%26b');
    expect(mailtoUrl('ap@tinex.mk', 'Your deliveries', 'a b')).toBe(
      'mailto:ap@tinex.mk?subject=Your%20deliveries&body=a%20b',
    );
  });
});
