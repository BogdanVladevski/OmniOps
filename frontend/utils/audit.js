// The audit trail in plain words: "Marko invited ana@… as dispatcher" instead of "InviteTeammate · AccountLink".
const TEXT = {
  'Create:Job': 'Created a job',
  'Start:Job': 'Driver started a trip',
  'Arrived:Job': 'Driver arrived',
  'Deliver:Job': 'Delivery confirmed',
  'Issue:Invoice': 'Issued an invoice',
  'MarkPaid:Invoice': 'Marked an invoice paid',
  'Update:BillingProfile': 'Changed invoice settings',
  'UpdateCompany:Organization': 'Renamed the company',
  'CreatePairingCode:Driver': "Created a code for a driver's phone",
  'InviteTeammate:AccountLink': 'Invited someone to the team',
  'RevokeInvitation:AccountLink': 'Withdrew an invitation',
  'AcceptInvitation:UserAccount': 'Joined the team',
  'ChangeTeammateRole:UserAccount': "Changed someone's role",
  'RemoveTeammate:UserAccount': 'Removed someone from the team',
  'CreatePasswordResetLink:UserAccount': 'Made a password reset link',
  'ResetPassword:UserAccount': 'Reset a password',
  'ChangePassword:UserAccount': 'Changed their password',
  'Assign:Incident': 'Assigned an alert',
  'Resolve:Incident': 'Resolved an alert',
};

/** { title, detail } for an audit entry; unknown actions fall back to "Action · Thing" so nothing is hidden. */
export function describeAudit(log) {
  const title = TEXT[`${log.action}:${log.entityType}`] ?? `${log.action} · ${log.entityType}`;
  let detail = log.details ?? null;
  if (log.action === 'Arrived' && detail) detail = detail === 'pickup' ? 'At the pickup' : 'At the customer';
  if (log.action === 'ChangeTeammateRole' && detail) detail = `Now ${detail.toLowerCase()}`;
  if (log.action === 'InviteTeammate' && detail) detail = detail.replace(/ as (\w+)$/, (m, role) => ` as ${role.toLowerCase()}`);
  return { title, detail };
}
