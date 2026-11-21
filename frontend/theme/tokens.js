// OmniOps design tokens — one source for spacing, type, colour and elevation.
// Palette: deep "ink" neutrals with an icy teal brand colour (cold chain), plus clear semantic
// colours so status is readable at a glance: critical (red), warning (amber), ok (green), info (blue).

export const spacing = {
  xxs: 2,
  xs: 4,
  sm: 8,
  md: 12,
  lg: 16,
  xl: 24,
  xxl: 32,
  xxxl: 48,
};

export const radius = {
  sm: 8,
  md: 12,
  lg: 16,
  xl: 22,
  pill: 999,
};

export const typography = {
  overline: { fontSize: 11, fontWeight: '700', lineHeight: 14, letterSpacing: 0.8, textTransform: 'uppercase' },
  caption: { fontSize: 12, fontWeight: '500', lineHeight: 16 },
  body: { fontSize: 14, fontWeight: '400', lineHeight: 21 },
  bodyStrong: { fontSize: 14, fontWeight: '600', lineHeight: 21 },
  title: { fontSize: 17, fontWeight: '700', lineHeight: 23 },
  headline: { fontSize: 26, fontWeight: '800', lineHeight: 32, letterSpacing: -0.4 },
  display: { fontSize: 34, fontWeight: '800', lineHeight: 40, letterSpacing: -0.8 },
  metric: { fontSize: 28, fontWeight: '800', lineHeight: 34, letterSpacing: -0.5 },
  label: { fontSize: 12, fontWeight: '600', lineHeight: 16, letterSpacing: 0.2 },
};

export const breakpoints = {
  tablet: 768,
  wide: 1024, // sidebar navigation from here up
  desktop: 1280,
};

const semanticLight = {
  critical: '#E5484D',
  criticalSoft: '#FDECEC',
  warning: '#D97706',
  warningSoft: '#FEF3E2',
  ok: '#16A34A',
  okSoft: '#E7F6EC',
  info: '#3B82F6',
  infoSoft: '#E8F0FE',
};

const semanticDark = {
  critical: '#FF6369',
  criticalSoft: '#3A1D21',
  warning: '#F5A524',
  warningSoft: '#3A2C14',
  ok: '#3DD68C',
  okSoft: '#16322A',
  info: '#70A5FF',
  infoSoft: '#18284A',
};

export const lightPalette = {
  mode: 'light',
  background: '#F4F6FA',
  card: '#FFFFFF',
  elevated: '#FFFFFF',
  sunken: '#EEF1F6',
  sidebar: '#0F1B2D',
  sidebarText: '#C9D4E5',
  sidebarActive: '#1C2D47',
  text: '#0F1B2D',
  textSecondary: '#4A5A70',
  muted: '#7C8AA0',
  accent: '#0EA5B7',
  accentStrong: '#0B8494',
  accentMuted: '#E2F6F8',
  onAccent: '#FFFFFF',
  border: '#E3E8F0',
  skeleton: '#E6EAF1',
  overlay: 'rgba(15,27,45,0.55)',
  ...semanticLight,
  danger: semanticLight.critical,
  success: semanticLight.ok,
};

export const darkPalette = {
  mode: 'dark',
  background: '#0A1220',
  card: '#111C2E',
  elevated: '#16243A',
  sunken: '#0D1728',
  sidebar: '#08101C',
  sidebarText: '#9FB0C8',
  sidebarActive: '#16243A',
  text: '#EAF0F8',
  textSecondary: '#A9B7CB',
  muted: '#73839B',
  accent: '#22C3D6',
  accentStrong: '#4FD8E8',
  accentMuted: '#10303A',
  onAccent: '#04121A',
  border: '#1F2E45',
  skeleton: '#1A2840',
  overlay: 'rgba(0,0,0,0.6)',
  ...semanticDark,
  danger: semanticDark.critical,
  success: semanticDark.ok,
};

export function paletteForMode(isDark) {
  return isDark ? darkPalette : lightPalette;
}

/** Soft card elevation that works on iOS, Android and web. */
export function elevation(isDark, level = 1) {
  if (isDark) return { borderWidth: 1, borderColor: darkPalette.border };
  return {
    shadowColor: '#0F1B2D',
    shadowOpacity: level === 1 ? 0.06 : 0.12,
    shadowRadius: level === 1 ? 12 : 24,
    shadowOffset: { width: 0, height: level === 1 ? 4 : 10 },
    elevation: level === 1 ? 2 : 6,
  };
}

/** Maps a status keyword to its foreground/background colours. */
export function toneColors(colors, tone) {
  switch (tone) {
    case 'critical':
      return { fg: colors.critical, bg: colors.criticalSoft };
    case 'warning':
      return { fg: colors.warning, bg: colors.warningSoft };
    case 'ok':
      return { fg: colors.ok, bg: colors.okSoft };
    case 'info':
      return { fg: colors.info, bg: colors.infoSoft };
    case 'accent':
      return { fg: colors.accentStrong, bg: colors.accentMuted };
    default:
      return { fg: colors.textSecondary, bg: colors.sunken };
  }
}
