import { fromResponse, getSession, isDriver, loadSession, onSessionChange, setSession } from '../utils/session';

describe('session', () => {
  it('persists, notifies and clears', async () => {
    const seen = [];
    const off = onSessionChange((s) => seen.push(s?.account?.role ?? null));
    await setSession(fromResponse({ accessToken: 'a', refreshToken: 'r', account: { role: 'Driver' }, extra: 1 }));
    expect(isDriver(getSession())).toBe(true);
    expect(await loadSession()).toEqual({ accessToken: 'a', refreshToken: 'r', account: { role: 'Driver' } });

    await setSession(null);
    expect(await loadSession()).toBeNull();
    off();
    expect(seen).toEqual(['Driver', null]);
  });
});
