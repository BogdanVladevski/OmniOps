/**
 * @jest-environment jsdom
 */
import { clearPendingLink, pendingLink } from '../utils/links';

jest.mock('react-native', () => ({ Platform: { OS: 'web' } }));

describe('pendingLink', () => {
  const setUrl = (path) => window.history.replaceState(null, '', path);

  it('reads an invitation or reset token from the address bar', () => {
    setUrl('/?join=abc-123');
    expect(pendingLink()).toEqual({ kind: 'join', token: 'abc-123' });
    setUrl('/?reset=xyz_9');
    expect(pendingLink()).toEqual({ kind: 'reset', token: 'xyz_9' });
    setUrl('/');
    expect(pendingLink()).toBeNull();
  });

  it('removes the token but keeps everything else', () => {
    setUrl('/app?join=abc&tab=jobs#top');
    clearPendingLink();
    expect(window.location.pathname + window.location.search + window.location.hash).toBe('/app?tab=jobs#top');
    setUrl('/?reset=abc');
    clearPendingLink();
    expect(window.location.search).toBe('');
  });
});
