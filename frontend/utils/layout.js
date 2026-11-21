import { useWindowDimensions } from 'react-native';
import { breakpoints } from '../theme/tokens';

/**
 * Responsive layout facts for the current window.
 * Phone: < 768 · Tablet: 768–1023 · Wide (sidebar nav): >= 1024 · Desktop: >= 1280.
 */
export function useLayout() {
  const { width, height } = useWindowDimensions();
  const compact = width < 390;
  const isTablet = width >= breakpoints.tablet;
  const isWide = width >= breakpoints.wide;
  const isDesktop = width >= breakpoints.desktop;

  return {
    width,
    height,
    compact,
    isTablet,
    isWide,
    isDesktop,
    /** Number of columns for card grids. */
    columns: isDesktop ? 4 : isTablet ? 2 : 1,
    horizontalPadding: isWide ? 32 : compact ? 14 : 18,
    contentMaxWidth: 1240,
    titleSize: isWide ? 30 : compact ? 24 : 26,
    bodySize: compact ? 13 : 14,
    cardPadding: isWide ? 20 : compact ? 14 : 16,
  };
}
