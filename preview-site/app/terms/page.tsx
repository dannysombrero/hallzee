import Link from 'next/link';
import type { Metadata } from 'next';
import { LegalShell } from '../legal-shell';
export const metadata: Metadata = {
  title: 'Website terms - Hallzee',
  description:
    'Terms for using the Hallzee information website and waiting list.',
};
export default function Terms() {
  return (
    <LegalShell title="Website terms">
      <p className="legal-updated">Last updated September 7, 2026</p>
      <p>
        These terms apply to the Hallzee information website and waiting list.
        Hallzee is based in Florida, United States.
      </p>
      <section>
        <h2>About this website</h2>
        <p>
          The site introduces Hallzee and describes the product being prepared
          for release. Hallzee is in development. Product details, availability,
          and pricing may change before release. Client views use sample data;
          they don’t show live activity in a school.
        </p>
      </section>
      <section>
        <h2>Joining the waiting list</h2>
        <p>
          Only submit an email address you control. Joining requests Hallzee
          availability updates. It is not an order, payment, contract to buy, or
          guarantee of a place in a trial. You can leave at any time using the{' '}
          <Link href="/leave-waitlist">removal form</Link>.
        </p>
      </section>
      <section>
        <h2>Using the site</h2>
        <p>
          You may read and share links to the site. Please don’t interfere with
          its operation, attempt to access other people’s information, or submit
          someone else’s email without their permission. Hallzee’s name,
          branding, and content remain the property of their respective owners.
        </p>
      </section>
      <section>
        <h2>Classroom use</h2>
        <p>
          This website is not a product-use agreement or a student-data
          agreement. Any terms for trials, purchases, or use of Hallzee in a
          school will be provided separately. Schools and teachers remain
          responsible for appropriate permissions, student accommodations, and
          decisions about classroom use.
        </p>
      </section>
      <section>
        <h2>Privacy and changes</h2>
        <p>
          The <Link href="/privacy">privacy notice</Link> explains what the
          waiting list stores and how to remove your information. These website
          terms may be updated as Hallzee develops; the date above identifies
          this version.
        </p>
      </section>
    </LegalShell>
  );
}
