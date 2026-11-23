// Design tokens. Direction: map-first and quiet. Neutral greys carry the interface; colour is reserved for status
// (red = at risk, amber = needs a look, green = fine) and one blue for selection and links.
import type { TextStyle } from 'react-native';

export const spacing = { xxs: 2, xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32, xxxl: 48 } as const;

export const radius = { sm: 6, md: 10, lg: 14, xl: 20, pill: 999 } as const;

const tabular: TextStyle = { fontVariant: ['tabular-nums'] };

export const typography = {
  caption: { fontSize: 12, fontWeight: '400', lineHeight: 16 },
  label: { fontSize: 13, fontWeight: '500', lineHeight: 18 },
  body: { fontSize: 15, fontWeight: '400', lineHeight: 21 },
  bodyStrong: { fontSize: 15, fontWeight: '600', lineHeight: 21 },
  title: { fontSize: 17, fontWeight: '600', lineHeight: 22, letterSpacing: -0.2 },
  headline: { fontSize: 24, fontWeight: '700', lineHeight: 30, letterSpacing: -0.5 },
  display: { fontSize: 30, fontWeight: '700', lineHeight: 36, letterSpacing: -0.8 },
  metric: { fontSize: 28, fontWeight: '700', lineHeight: 34, letterSpacing: -0.5, ...tabular },
  number: { fontSize: 17, fontWeight: '600', lineHeight: 22, ...tabular },
} satisfies Record<string, TextStyle>;

export const breakpoints = { tablet: 768, wide: 1024, desktop: 1280 } as const;

const light = {
  background: '#F5F5F3',
  card: '#FFFFFF',
  elevated: '#FFFFFF',
  sunken: '#EFEFEC',
  text: '#16181D',
  textSecondary: '#545A66',
  muted: '#8A8F99',
  border: '#E4E4E0',
  accent: '#2E5BDB',
  accentMuted: '#EAF0FD',
  primary: '#16181D',
  onPrimary: '#FFFFFF',
  critical: '#C8322B',
  criticalSoft: '#FBEDEC',
  warning: '#A85C0A',
  warningSoft: '#FBF1E4',
  ok: '#1C7A45',
  okSoft: '#EAF5EE',
  overlay: 'rgba(22,24,29,0.45)',
};

export type Palette = typeof light;

const dark: Palette = {
  background: '#0F1012',
  card: '#17181B',
  elevated: '#1D1F23',
  sunken: '#202227',
  text: '#ECEDEF',
  textSecondary: '#A3A8B2',
  muted: '#6F747E',
  border: '#2A2C31',
  accent: '#7A9BFF',
  accentMuted: '#1D2540',
  primary: '#ECEDEF',
  onPrimary: '#0F1012',
  critical: '#F0645C',
  criticalSoft: '#33191A',
  warning: '#E0A04A',
  warningSoft: '#33261A',
  ok: '#4CC285',
  okSoft: '#16291F',
  overlay: 'rgba(0,0,0,0.6)',
};

export const palettes = { light, dark };

export type Tone = 'neutral' | 'accent' | 'ok' | 'warning' | 'critical';

/** Status keyword → colours. */
export function toneColors(colors: Palette, tone: Tone): { fg: string; bg: string } {
  switch (tone) {
    case 'critical':
      return { fg: colors.critical, bg: colors.criticalSoft };
    case 'warning':
      return { fg: colors.warning, bg: colors.warningSoft };
    case 'ok':
      return { fg: colors.ok, bg: colors.okSoft };
    case 'accent':
      return { fg: colors.accent, bg: colors.accentMuted };
    default:
      return { fg: colors.textSecondary, bg: colors.sunken };
  }
}

/** Floating surfaces (map panels) get a soft shadow in light mode and a border in dark mode. */
export function floating(isDark: boolean) {
  return isDark ? { borderWidth: 1, borderColor: dark.border } : { boxShadow: '0 8px 24px rgba(0,0,0,0.12)' };
}
