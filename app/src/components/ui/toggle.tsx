import { Platform, Switch } from 'react-native';
import { useTheme } from '@/theme/theme-provider';

// react-native-web colours the "on" thumb with its own prop (activeThumbColor, not in React Native's types);
// without it the thumb turns teal on the web.
const webOnThumb = Platform.OS === 'web' ? ({ activeThumbColor: '#FFFFFF' } as object) : {};

/** An on/off switch in the app's colours: accent when on, a neutral track when off, always a white thumb. */
export function Toggle({
  value,
  onChange,
  label,
}: {
  value: boolean;
  onChange: (value: boolean) => void;
  label: string;
}) {
  const { colors } = useTheme();
  return (
    <Switch
      value={value}
      onValueChange={onChange}
      accessibilityLabel={label}
      trackColor={{ true: colors.accent, false: colors.border }}
      thumbColor="#FFFFFF"
      {...webOnThumb}
    />
  );
}
