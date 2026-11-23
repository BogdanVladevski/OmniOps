import { LANGUAGES, deviceLanguage, translate } from '.';
import { en, type StringKey } from './en';

jest.mock('expo-localization', () => ({ getLocales: () => [] }));
jest.mock('@react-native-async-storage/async-storage', () =>
  require('@react-native-async-storage/async-storage/jest/async-storage-mock'),
);

describe('driver app languages', () => {
  const keys = Object.keys(en) as StringKey[];

  it.each(LANGUAGES.map((l) => [l.code, l.strings] as const))(
    '%s has every text, with the same placeholders',
    (_code, strings) => {
      expect(Object.keys(strings).sort()).toEqual([...keys].sort());
      for (const key of keys) {
        const names = (s: string) => (s.match(/\{\w+\}/g) ?? []).sort();
        expect([key, names(strings[key])]).toEqual([key, names(en[key])]);
        expect(strings[key].trim()).not.toBe('');
      }
    },
  );

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
