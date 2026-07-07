import AsyncStorage from '@react-native-async-storage/async-storage';

const KEYS = {
  fleetVehicles: 'omniops:fleet:vehicles',
  alerts: 'omniops:alerts',
  lastSync: 'omniops:lastSync',
  syncQueue: 'omniops:syncQueue',
};

export async function loadCachedVehicles() {
  const raw = await AsyncStorage.getItem(KEYS.fleetVehicles);
  return raw ? JSON.parse(raw) : {};
}

export async function saveCachedVehicles(vehicles) {
  await AsyncStorage.setItem(KEYS.fleetVehicles, JSON.stringify(vehicles));
}

export async function loadCachedAlerts() {
  const raw = await AsyncStorage.getItem(KEYS.alerts);
  return raw ? JSON.parse(raw) : [];
}

export async function saveCachedAlerts(alerts) {
  await AsyncStorage.setItem(KEYS.alerts, JSON.stringify(alerts.slice(0, 100)));
}

export async function getLastSyncUtc() {
  return AsyncStorage.getItem(KEYS.lastSync);
}

export async function setLastSyncUtc(iso) {
  await AsyncStorage.setItem(KEYS.lastSync, iso);
}

export async function loadSyncQueue() {
  const raw = await AsyncStorage.getItem(KEYS.syncQueue);
  return raw ? JSON.parse(raw) : [];
}

export async function saveSyncQueue(queue) {
  await AsyncStorage.setItem(KEYS.syncQueue, JSON.stringify(queue));
}

export async function enqueueSyncAction(action) {
  const queue = await loadSyncQueue();
  queue.push({ ...action, id: `${Date.now()}-${Math.random().toString(36).slice(2)}`, createdAt: new Date().toISOString() });
  await saveSyncQueue(queue);
  return queue;
}

export async function clearSyncQueue() {
  await AsyncStorage.removeItem(KEYS.syncQueue);
}
