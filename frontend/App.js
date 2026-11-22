import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Platform, StatusBar } from 'react-native';
import { NavigationContainer, DefaultTheme, DarkTheme } from '@react-navigation/native';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { FleetProvider, useFleet } from './contexts/FleetContext';
import { ThemeProvider, useTheme } from './contexts/ThemeContext';
import { SessionProvider, useSession } from './contexts/SessionContext';
import AuthScreens from './components/AuthScreens';
import { pendingLink } from './utils/links';
import { isDriver } from './utils/session';
import ErrorBoundary from './components/ui/ErrorBoundary';
import OfflineBanner from './components/ui/OfflineBanner';
import OnboardingWizard, { useOnboarding } from './components/OnboardingWizard';
import CommandPalette from './components/CommandPalette';
import AboutModal from './components/AboutModal';
import AppNav, { TAB_META } from './components/AppNav';
import LiveView from './components/LiveView';
import FleetManagement from './components/FleetManagement';
import InvoicesBoard from './components/InvoicesBoard';
import AlertsFeed from './components/AlertsFeed';
import AdminPanel from './components/AdminPanel';
import JobsBoard from './components/JobsBoard';
import DriverApp from './components/driver/DriverApp';
import { alertMeta } from './utils/alerts';
import { useLayout } from './utils/layout';

const Tab = createBottomTabNavigator();

function Navigator({ onSearch }) {
  const { isWide } = useLayout();
  const { alerts } = useFleet();
  const criticalCount = alerts.filter((a) => alertMeta(a).severity === 'critical').length;

  return (
    <Tab.Navigator
      initialRouteName="Live"
      tabBar={(props) => <AppNav {...props} onSearch={onSearch} alertCount={criticalCount} />}
      screenOptions={({ route }) => ({
        headerShown: false,
        tabBarPosition: isWide ? 'top' : 'bottom',
        title: `${TAB_META[route.name]?.label ?? route.name} · OmniOps`,
      })}
    >
      <Tab.Screen name="Live" component={LiveView} />
      <Tab.Screen name="Jobs" component={JobsBoard} />
      <Tab.Screen name="Invoices" component={InvoicesBoard} />
      <Tab.Screen name="Alerts" component={AlertsFeed} />
      <Tab.Screen name="Fleet" component={FleetManagement} />
      <Tab.Screen name="Admin" component={AdminPanel} />
    </Tab.Navigator>
  );
}

function AppShell() {
  const { session, ready } = useSession();
  const { isDark } = useTheme();
  // An invitation or password-reset link wins over whoever is signed in on this device.
  const [link, setLink] = useState(pendingLink);
  if (!ready) return null;
  const bar = <StatusBar barStyle={isDark ? 'light-content' : 'dark-content'} backgroundColor="transparent" translucent />;
  if (link) return <>{bar}<AuthScreens link={link} onLinkDone={() => setLink(null)} /></>;
  if (!session) return <>{bar}<AuthScreens /></>;
  if (isDriver(session)) return <>{bar}<DriverApp /></>;
  // Keyed by account so switching accounts starts from a clean slate (no other company's data on screen).
  return <OfficeShell key={session.account.userId} />;
}

function OfficeShell() {
  const { colors, isDark } = useTheme();
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
          <Navigator onSearch={openPalette} />
        </NavigationContainer>

        <OnboardingWizard visible={onboardingVisible} onComplete={completeOnboarding} />
        <CommandPalette
          visible={paletteOpen}
          onClose={closePalette}
          navigationRef={navigationRef}
          onAbout={() => setAboutOpen(true)}
        />
        <AboutModal visible={aboutOpen} onClose={() => setAboutOpen(false)} />
      </FleetProvider>
    </>
  );
}

export default function App() {
  return (
    <SafeAreaProvider>
      <ThemeProvider>
        <ErrorBoundary>
          <SessionProvider>
            <AppShell />
          </SessionProvider>
        </ErrorBoundary>
      </ThemeProvider>
    </SafeAreaProvider>
  );
}
