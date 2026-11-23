import { useWindowDimensions } from 'react-native';
import { breakpoints } from '@/theme/tokens';

/** Phone < 768 · tablet 768–1023 · wide (top navigation) ≥ 1024. */
export function useLayout() {
  const { width, height } = useWindowDimensions();
  const isWide = width >= breakpoints.wide;
  return {
    width,
    height,
    isTablet: width >= breakpoints.tablet,
    isWide,
    horizontalPadding: isWide ? 32 : width < 390 ? 14 : 18,
  };
}
