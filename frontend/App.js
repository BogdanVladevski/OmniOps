import React from 'react';
import { Platform, StatusBar } from 'react-native';
import { NavigationContainer } from '@react-navigation/native';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { FleetProvider } from './context/FleetContext';
import FleetMap from './components/FleetMap';
import FleetDashboard from './components/FleetDashboard';
import AlertsFeed from './components/AlertsFeed';

const Tab = createBottomTabNavigator();

export default function App() {
  return (
    <SafeAreaProvider>
      <FleetProvider>
        <StatusBar barStyle="dark-content" backgroundColor="transparent" translucent />
        <NavigationContainer>
          <Tab.Navigator
            screenOptions={{
              headerShown: false,
              tabBarActiveTintColor: '#007AFF',
              tabBarInactiveTintColor: '#8E8E93',
              tabBarLabelStyle: { fontSize: 12, fontWeight: '600' },
              tabBarStyle: {
                paddingTop: 4,
                paddingBottom: Platform.OS === 'android' ? 8 : 0,
              },
            }}
          >
            <Tab.Screen name="Map" component={FleetMap} />
            <Tab.Screen name="Fleet" component={FleetDashboard} />
            <Tab.Screen name="Alerts" component={AlertsFeed} />
          </Tab.Navigator>
        </NavigationContainer>
      </FleetProvider>
    </SafeAreaProvider>
  );
}
