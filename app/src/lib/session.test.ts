import type { Account } from '@/api/types';
import { fromResponse, getSession, isDriver, loadSession, onSessionChange, setSession } from './session';

const account: Account = {
  userId: 'u1',
  displayName: 'Ana',
  email: null,
  role: 'Driver',
  organizationId: 'o1',
  organizationName: 'Vardar',
  driverId: 'd1',
};

describe('session', () => {
  it('persists, notifies and clears', async () => {
    const seen: (string | null)[] = [];
    const off = onSessionChange((s) => seen.push(s?.account.role ?? null));
    await setSession(
      fromResponse({ accessToken: 'a', refreshToken: 'r', account, expiresIn: 1800 } as Parameters<
        typeof fromResponse
      >[0]),
    );
    expect(isDriver(getSession())).toBe(true);
    expect(await loadSession()).toEqual({ accessToken: 'a', refreshToken: 'r', account });

    await setSession(null);
    expect(await loadSession()).toBeNull();
    off();
    expect(seen).toEqual(['Driver', null]);
  });
});
