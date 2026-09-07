import Link from 'next/link';
import type { Metadata } from 'next';
import { LegalShell } from '../legal-shell';
export const metadata: Metadata = {
  title: 'Privacy - Hallzee',
  description:
    'How the Hallzee website uses waiting-list information and how to remove your email.',
};
export default function Privacy() {
  return (
    <LegalShell title="Privacy">
      <p className="legal-updated">Last updated September 7, 2026</p>
      <p>
        This notice covers the Hallzee website and its waiting list. Hallzee is
        based in Florida, United States. It does not cover student records held
        in a classroom’s Hallzee terminal or desktop app.
      </p>
      <section>
        <h2>What the waiting list stores</h2>
        <p>
          When you join, we save your email address, the date you joined, and
          the version of the signup notice shown at the time. Entries made
          before this notice was added contain an email address and signup date
          only. We don’t ask for your name, school, phone number, or payment
          information.
        </p>
      </section>
      <section>
        <h2>How we use your email</h2>
        <p>
          We use it to keep the waiting list and send updates about Hallzee’s
          availability and opportunities to try it. Joining is optional. We
          don’t sell or rent the waiting list, use it for targeted advertising,
          or share it with schools or advertisers.
        </p>
      </section>
      <section>
        <h2>Hosting and technical information</h2>
        <p>
          The site is hosted through Sites, with waiting-list records stored in
          a Cloudflare database. Hosting providers process information needed to
          deliver and protect the site, which may include IP addresses, browser
          information, request logs, and essential cookies. A private preview
          may also require the hosting platform’s sign-in.
        </p>
        <p>
          Hallzee has not added advertising trackers or third-party analytics to
          this site. The website does not upload classroom rosters or trip
          records.
        </p>
      </section>
      <section>
        <h2>Keeping and removing your information</h2>
        <p>
          We keep your signup while it is needed for the waiting list and the
          updates you requested. You can{' '}
          <Link href="/leave-waitlist">leave the waiting list</Link> at any
          time. The form removes your address and associated signup record from
          the active website database. Technical logs and provider backups may
          follow the hosting provider’s separate retention schedules.
        </p>
        <p>
          If you want to use a different address, remove the old one and join
          again with the new one. We will use the current list when preparing
          updates and remove records when the waiting list is no longer needed.
        </p>
      </section>
      <section>
        <h2>For adults, not student signups</h2>
        <p>
          This website and waiting list are intended for teachers and other
          adults interested in Hallzee. Students don’t need to join this list to
          use a classroom terminal. Please don’t submit student information
          here.
        </p>
      </section>
      <section>
        <h2>Changes to this notice</h2>
        <p>
          We’ll update this page if the website’s data practices change. The
          date at the top identifies the current notice. A new use beyond the
          availability updates described here would require a separate choice
          from you.
        </p>
      </section>
      <Link className="button button-primary" href="/leave-waitlist">
        Remove my email from the waiting list
      </Link>
    </LegalShell>
  );
}
