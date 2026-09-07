import type { Metadata } from 'next';
import { LegalShell } from '../legal-shell';
import { RemovalForm } from './removal-form';
export const metadata: Metadata = {
  title: 'Leave the waiting list - Hallzee',
  description:
    'Remove your email address and signup record from the Hallzee waiting list.',
};
export default function LeaveWaitlist() {
  return (
    <LegalShell title="Leave the waiting list">
      <p>
        You can remove your email and signup record here. You don’t need an
        account or a password, and the form won’t reveal whether an address was
        on the list.
      </p>
      <RemovalForm />
    </LegalShell>
  );
}
