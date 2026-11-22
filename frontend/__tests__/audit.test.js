import { describeAudit } from '../utils/audit';

describe('audit trail wording', () => {
  it('says what happened in plain words', () => {
    expect(describeAudit({ action: 'InviteTeammate', entityType: 'AccountLink', details: 'ana@x.mk as Dispatcher' }))
      .toEqual({ title: 'Invited someone to the team', detail: 'ana@x.mk as dispatcher' });
    expect(describeAudit({ action: 'Arrived', entityType: 'Job', details: 'delivery' })).toEqual({ title: 'Driver arrived', detail: 'At the customer' });
  });

  it('never hides an action it does not know', () => {
    expect(describeAudit({ action: 'Frobnicate', entityType: 'Thing', details: null })).toEqual({ title: 'Frobnicate · Thing', detail: null });
  });
});
