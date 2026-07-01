import { useWindowDimensions } from 'react-native';

export function useLayout() {
  const { width, height } = useWindowDimensions();
  const compact = width < 390;

  return {
    width,
    height,
    compact,
    horizontalPadding: compact ? 12 : 16,
    titleSize: compact ? 24 : 28,
    bodySize: compact ? 13 : 14,
    cardPadding: compact ? 12 : 16,
  };
}
