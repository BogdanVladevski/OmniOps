// The office: Today, Jobs, Invoices, Fleet, Settings. Live updates and Ctrl K search are available on every tab.
import { Tabs } from 'expo-router/tabs';
import { useEffect, useState } from 'react';
import { Platform, View } from 'react-native';
import { CommandPalette } from '@/components/shell/command-palette';
import { OfficeNav, TABS } from '@/components/shell/office-nav';
import { AlertToasts } from '@/features/realtime/alert-toasts';
import { LiveUpdatesProvider } from '@/features/realtime/live-updates';
import { useLayout } from '@/hooks/use-layout';
import { useSession } from '@/lib/use-session';

export default function OfficeLayout() {
  const { isWide } = useLayout();
  const session = useSession();
  const [searching, setSearching] = useState(false);

  useEffect(() => {
    if (Platform.OS !== 'web') return;
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setSearching((open) => !open);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  return (
    // Keyed by account: switching accounts starts from a clean slate, with no other company's data on screen.
    <LiveUpdatesProvider key={session?.account.userId}>
      <View style={{ flex: 1 }}>
        <Tabs
          tabBar={(props) => <OfficeNav {...props} onSearch={() => setSearching(true)} />}
          screenOptions={({ route }) => ({
            headerShown: false,
            tabBarPosition: isWide ? 'top' : 'bottom',
            title: `${TABS[route.name]?.label ?? route.name} · OmniOps`,
          })}
        >
          <Tabs.Screen name="index" />
          <Tabs.Screen name="jobs" />
          <Tabs.Screen name="customers" />
          <Tabs.Screen name="invoices" />
          <Tabs.Screen name="fleet" />
          <Tabs.Screen name="settings" />
        </Tabs>
        <AlertToasts />
        <CommandPalette visible={searching} onClose={() => setSearching(false)} />
      </View>
    </LiveUpdatesProvider>
  );
}
