export const spacing = {
  xs: 4,
  sm: 8,
  md: 12,
  lg: 16,
  xl: 24,
  xxl: 32,
};

export const radius = {
  sm: 8,
  md: 12,
  lg: 16,
  pill: 999,
};

export const typography = {
  caption: { fontSize: 11, fontWeight: '500', lineHeight: 14 },
  body: { fontSize: 14, fontWeight: '400', lineHeight: 20 },
  bodyStrong: { fontSize: 14, fontWeight: '600', lineHeight: 20 },
  title: { fontSize: 18, fontWeight: '700', lineHeight: 24 },
  headline: { fontSize: 24, fontWeight: '800', lineHeight: 30 },
  label: { fontSize: 12, fontWeight: '600', lineHeight: 16, letterSpacing: 0.4 },
};

export const lightPalette = {
  mode: 'light',
  background: '#F2F2F7',
  card: '#FFFFFF',
  elevated: '#FFFFFF',
  text: '#1C1C1E',
  textSecondary: '#636366',
  muted: '#8E8E93',
  accent: '#007AFF',
  accentMuted: '#E8F2FF',
  danger: '#FF3B30',
  warning: '#FF9500',
  success: '#34C759',
  border: '#E5E5EA',
  skeleton: '#E5E5EA',
  overlay: 'rgba(0,0,0,0.45)',
};

export const darkPalette = {
  mode: 'dark',
  background: '#000000',
  card: '#1C1C1E',
  elevated: '#2C2C2E',
  text: '#FFFFFF',
  textSecondary: '#AEAEB2',
  muted: '#8E8E93',
  accent: '#0A84FF',
  accentMuted: '#1A2A40',
  danger: '#FF453A',
  warning: '#FF9F0A',
  success: '#30D158',
  border: '#38383A',
  skeleton: '#2C2C2E',
  overlay: 'rgba(0,0,0,0.6)',
};

export function paletteForMode(isDark) {
  return isDark ? darkPalette : lightPalette;
}
