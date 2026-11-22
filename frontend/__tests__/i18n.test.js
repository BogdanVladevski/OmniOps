import { LANGUAGES, deviceLanguage, translate } from '../i18n';
import en from '../i18n/en';

jest.mock('expo-localization', () => ({ getLocales: () => [] }));

describe('driver app languages', () => {
  const keys = Object.keys(en);

  it.each(LANGUAGES.map((l) => [l.code, l]))('%s has every text, with the same placeholders', (code, { strings }) => {
    expect(Object.keys(strings).sort()).toEqual([...keys].sort());
    for (const key of keys) {
      const names = (s) => (s.match(/\{\w+\}/g) ?? []).sort();
      expect([key, names(strings[key])]).toEqual([key, names(en[key])]);
      expect(strings[key].trim()).not.toBe('');
    }
  });

  it('fills in placeholders and falls back to English', () => {
    expect(translate('de', 'order', { ref: 'PO-1' })).toBe('Auftrag PO-1');
    expect(translate('xx', 'order', { ref: 'PO-1' })).toBe('Order PO-1');
  });

  it("follows the phone's language, treating Croatian and Bosnian as the shared Latin text", () => {
    expect(deviceLanguage([{ languageCode: 'mk' }])).toBe('mk');
    expect(deviceLanguage([{ languageCode: 'hr' }])).toBe('sr');
    expect(deviceLanguage([{ languageCode: 'bs' }])).toBe('sr');
    expect(deviceLanguage([{ languageCode: 'ja' }, { languageCode: 'de' }])).toBe('de');
    expect(deviceLanguage([{ languageCode: 'ja' }])).toBe('en');
  });
});
