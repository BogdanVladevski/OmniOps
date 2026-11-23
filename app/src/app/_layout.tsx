// The root: providers, the stored sign-in, and which part of the app each role may see.
import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState } from 'react';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { queryClient } from '@/api/query-client';
import { LanguageProvider } from '@/i18n';
import { loadSession } from '@/lib/session';
import { useSession } from '@/lib/use-session';
import { ThemeProvider, useTheme } from '@/theme/theme-provider';

SplashScreen.preventAutoHideAsync().catch(() => {});

function Navigator() {
  const session = useSession();
  const { colors, isDark } = useTheme();
  const role = session?.account.role;
  return (
    <>
      <StatusBar style={isDark ? 'light' : 'dark'} />
      <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: colors.background } }}>
        <Stack.Protected guard={!session}>
          <Stack.Screen name="(auth)" />
        </Stack.Protected>
        <Stack.Protected guard={role === 'Driver'}>
          <Stack.Screen name="driver" />
        </Stack.Protected>
        <Stack.Protected guard={!!session && role !== 'Driver'}>
          <Stack.Screen name="(office)" />
        </Stack.Protected>
        {/* Links from emails work whoever is signed in on this device. */}
        <Stack.Screen name="join/[token]" />
        <Stack.Screen name="reset/[token]" />
      </Stack>
    </>
  );
}

export default function RootLayout() {
  const [ready, setReady] = useState(false);
  useEffect(() => {
    loadSession().finally(() => {
      setReady(true);
      SplashScreen.hideAsync().catch(() => {});
    });
  }, []);
  if (!ready) return null;
  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <SafeAreaProvider>
        <ThemeProvider>
          <LanguageProvider>
            <QueryClientProvider client={queryClient}>
              <Navigator />
            </QueryClientProvider>
          </LanguageProvider>
        </ThemeProvider>
      </SafeAreaProvider>
    </GestureHandlerRootView>
  );
}
