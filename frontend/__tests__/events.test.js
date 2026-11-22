import { notifyJobsChanged, onJobsChanged } from '../utils/events';

describe('jobs changed signal', () => {
  it('reaches subscribers until they unsubscribe, even if another listener throws', () => {
    const seen = [];
    const offBad = onJobsChanged(() => { throw new Error('boom'); });
    const off = onJobsChanged(() => seen.push('refresh'));
    notifyJobsChanged();
    off();
    offBad();
    notifyJobsChanged();
    expect(seen).toEqual(['refresh']);
  });
});
