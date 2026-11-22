/**
 * @jest-environment jsdom
 */
// Regression: with sign-in required, the office's job photos were blank on the web — browsers don't send the
// token with <img>, so every photo request got 401. Photos are now downloaded with the token.
import { authorizedObjectUrl } from '../utils/api';
import { setSession } from '../utils/session';

describe('authorizedObjectUrl', () => {
  beforeEach(async () => {
    await setSession({ accessToken: 'token-1', refreshToken: 'r', account: { userId: 'u' } });
    global.URL.createObjectURL = jest.fn(() => 'blob:photo');
  });

  it('sends the sign-in token and returns a local URL for the photo', async () => {
    global.fetch = jest.fn(async () => ({ ok: true, status: 200, blob: async () => 'bytes' }));
    await expect(authorizedObjectUrl('http://api/photo')).resolves.toBe('blob:photo');
    expect(global.fetch).toHaveBeenCalledWith('http://api/photo', { headers: { Authorization: 'Bearer token-1' } });
  });

  it('fails instead of showing a broken image when the server refuses', async () => {
    global.fetch = jest.fn(async () => ({ ok: false, status: 404, blob: async () => null }));
    await expect(authorizedObjectUrl('http://api/photo')).rejects.toMatchObject({ status: 404 });
  });
});
