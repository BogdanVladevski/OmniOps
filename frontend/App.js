import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Platform, Pressable, StatusBar, Text, View } from 'react-native';
import { NavigationContainer, DefaultTheme, DarkTheme } from '@react-navigation/native';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { FleetProvider } from './contexts/FleetContext';
import { ThemeProvider, useTheme } from './contexts/ThemeContext';
import ErrorBoundary from './components/ui/ErrorBoundary';
import OfflineBanner from './components/ui/OfflineBanner';
import OnboardingWizard, { useOnboarding } from './components/OnboardingWizard';
import CommandPalette from './components/CommandPalette';
import AboutModal from './components/AboutModal';
import FleetMap from './components/FleetMap';
import FleetDashboard from './components/FleetDashboard';
import FleetManagement from './components/FleetManagement';
import AnalyticsPanel from './components/AnalyticsPanel';
import AlertsFeed from './components/AlertsFeed';
import AdminPanel from './components/AdminPanel';
import { spacing } from './theme/tokens';

const Tab = createBottomTabNavigator();

function AppShell() {
  const { colors, isDark, toggle } = useTheme();
  const navigationRef = useRef(null);
  const { visible: onboardingVisible, complete: completeOnboarding } = useOnboarding();
  const [paletteOpen, setPaletteOpen] = useState(false);
  const [aboutOpen, setAboutOpen] = useState(false);

  const navTheme = {
    ...(isDark ? DarkTheme : DefaultTheme),
    colors: {
      ...(isDark ? DarkTheme.colors : DefaultTheme.colors),
      background: colors.background,
      card: colors.card,
      text: colors.text,
      border: colors.border,
      primary: colors.accent,
    },
  };

  const openPalette = useCallback(() => setPaletteOpen(true), []);
  const closePalette = useCallback(() => setPaletteOpen(false), []);

  useEffect(() => {
    if (Platform.OS !== 'web' || typeof window === 'undefined') return undefined;
    const onKey = (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setPaletteOpen((v) => !v);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  return (
    <>
      <StatusBar barStyle={isDark ? 'light-content' : 'dark-content'} backgroundColor="transparent" translucent />
      <FleetProvider>
        <OfflineBanner />
        <NavigationContainer ref={navigationRef} theme={navTheme}>
          <Tab.Navigator
            screenOptions={{
              headerShown: false,
              tabBarActiveTintColor: colors.accent,
              tabBarInactiveTintColor: colors.muted,
              tabBarStyle: {
                backgroundColor: colors.card,
                borderTopColor: colors.border,
                paddingTop: 4,
                paddingBottom: Platform.OS === 'android' ? 8 : 0,
              },
              tabBarLabelStyle: { fontSize: 11, fontWeight: '600' },
            }}
          >
            <Tab.Screen name="Map" component={FleetMap} />
            <Tab.Screen name="Fleet" component={FleetDashboard} />
            <Tab.Screen name="Manage" component={FleetManagement} />
            <Tab.Screen name="Analytics" component={AnalyticsPanel} />
            <Tab.Screen name="Alerts" component={AlertsFeed} />
            <Tab.Screen
              name="Admin"
              component={AdminPanel}
              options={{
                tabBarButton: (props) => (
                  <Pressable {...props} onLongPress={toggle} delayLongPress={400} />
                ),
              }}
            />
          </Tab.Navigator>
        </NavigationContainer>

        <Pressable
          onPress={openPalette}
          style={{
            position: 'absolute',
            right: spacing.lg,
            bottom: Platform.OS === 'ios' ? 88 : 72,
            width: 48,
            height: 48,
            borderRadius: 24,
            backgroundColor: colors.accent,
            alignItems: 'center',
            justifyContent: 'center',
            shadowColor: '#000',
            shadowOpacity: 0.2,
            shadowRadius: 6,
            elevation: 4,
          }}
          accessibilityRole="button"
          accessibilityLabel="Open command palette"
        >
          <Text style={{ color: '#FFF', fontSize: 20 }}>⌕</Text>
        </Pressable>

        <OnboardingWizard visible={onboardingVisible} onComplete={completeOnboarding} />
        <CommandPalette
          visible={paletteOpen}
          onClose={closePalette}
          navigationRef={navigationRef}
          onAbout={() => setAboutOpen(true)}
        />
        <AboutModal visible={aboutOpen} onClose={() => setAboutOpen(false)} />
      </FleetProvider>
      {__DEV__ && (
        <Pressable
          onPress={toggle}
          style={{ position: 'absolute', top: 48, right: 8, opacity: 0.01, width: 24, height: 24 }}
          accessibilityLabel="Toggle dark mode"
        />
      )}
    </>
  );
}

export default function App() {
  return (
    <SafeAreaProvider>
      <ThemeProvider>
        <ErrorBoundary>
          <AppShell />
        </ErrorBoundary>
      </ThemeProvider>
    </SafeAreaProvider>
  );
}
