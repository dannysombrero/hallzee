'use client';
import Link from 'next/link';

import { useEffect, useState } from 'react';
import Image from 'next/image';
import {
  ThemeHeaderToggle,
  ThemeFloatingDock,
  type ThemeMode,
  type BackgroundPreset,
} from './components/ThemeControlBar';
import {
  ArrowDown,
  ArrowRight,
  ArrowUpRight,
  Bluetooth,
  CheckCircle2,
  Clock3,
  Users,
} from 'lucide-react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

const features = [
  {
    id: 'now',
    label: 'Who’s out',
    number: '01',
    title: 'The answer is already on your screen.',
    text: 'When a student signs out, their name and a running timer appear in the desktop app. When they return, the trip moves into your history.',
    detail:
      'If a student forgets to check back in, you can end the trip from your desk. Hallzee marks it as a manual check-in.',
  },
  {
    id: 'mini',
    label: 'During your lesson',
    number: '02',
    title: 'Keep it in view.',
    text: 'The Mini Window stays on top, even when the main app is minimized.',
    detail: '',
  },
  {
    id: 'history',
    label: 'Trip history',
    number: '03',
    title: 'Look it up without piecing it together.',
    text: 'Find a student’s trips by name or ID. See when they left, when they returned, and how long they were out. Filter the history by date, duration, or how the trip ended.',
    detail:
      'Export trip records to CSV when you need them in a spreadsheet or for a conversation.',
  },
  {
    id: 'patterns',
    label: 'Longer trips',
    number: '04',
    title: 'Notice when a long trip becomes a pattern.',
    text: 'Choose a duration to look at. The Exceeded Time panel groups longer trips by student, shows how often they happen, and lets you see the dates and durations.',
    detail:
      'Bring specific examples to a check-in with a student instead of relying on “it seems like you’ve been gone a lot.”',
  },
];
function Brand() {
  return (
    <Link className="brand" href="#top" aria-label="Hallzee home">
      <Image
        unoptimized
        src="/hallzee-logo.png"
        alt=""
        width="29"
        height="32"
      />
      <span>
        hallzee<span className="brand-dot">.</span>
      </span>
    </Link>
  );
}
const clientViews: Record<
  string,
  { file: string; alt: string; width: number; height: number }
> = {
  now: {
    file: 'whos-out.png',
    alt: 'Hallzee’s live trip card: Alex Morgan is out of class, with a running elapsed timer and a Check In button.',
    width: 2000,
    height: 260,
  },
  mini: {
    file: 'mini-window.png',
    alt: 'The Hallzee Mini Window showing Period 3, a live clock, Pass Unavailable, and the countdown until the pass window closes.',
    width: 1040,
    height: 560,
  },
  history: {
    file: 'trip-history.png',
    alt: 'Hallzee’s Hall Pass Trip History window, with search, date, duration and status filters, student trip records, and an Export button.',
    width: 2080,
    height: 1104,
  },
  patterns: {
    file: 'longer-trips.png',
    alt: 'Hallzee’s Exceeded Time panel with a duration threshold, search, sorting, and counts of longer trips for each student.',
    width: 850,
    height: 600,
  },
};
function ClientView({ type }: { type: string }) {
  const view = clientViews[type];
  return (
    <figure className={`client-view client-view-${type}`}>
      <a
        className="client-view-image"
        href={`/client/${view.file}`}
        target="_blank"
        rel="noopener noreferrer"
        aria-label={`View full-size image: ${features.find((f) => f.id === type)?.label}`}
      >
        <Image
          unoptimized
          src={`/client/${view.file}`}
          alt={view.alt}
          width={view.width}
          height={view.height}
        />
      </a>
      <figcaption>
        <span>Actual client view · Sample student data</span>
        <a
          href={`/client/${view.file}`}
          target="_blank"
          rel="noopener noreferrer"
        >
          View full size <ArrowUpRight size={13} />
        </a>
      </figcaption>
    </figure>
  );
}
function Signup() {
  const [status, setStatus] = useState<
    'idle' | 'sending' | 'success' | 'error'
  >('idle');
  const [message, setMessage] = useState('');
  async function submit(event: {
    preventDefault: () => void;
    currentTarget: HTMLFormElement;
  }) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setStatus('sending');
    try {
      const response = await fetch('/api/waitlist', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          email: form.get('email'),
          website: form.get('website'),
        }),
      });
      const data = (await response.json()) as { error?: string };
      if (!response.ok)
        throw new Error(
          data.error || 'Your email wasn’t saved. Please try again.',
        );
      setStatus('success');
    } catch (error) {
      setStatus('error');
      setMessage(
        error instanceof Error
          ? error.message
          : 'Your email wasn’t saved. Please try again.',
      );
    }
  }
  return (
    <section
      className="signup-section"
      id="waitlist"
      aria-labelledby="signup-title"
    >
      <div className="signup-copy">
        <p className="eyebrow">THE WAITING LIST</p>
        <h2 id="signup-title">
          Want to try Hallzee
          <br />
          in your classroom?
        </h2>
        <p>
          Leave your email to hear when Hallzee is ready to try. Joining the
          list doesn’t commit you to buying anything.
        </p>
      </div>
      <div className="signup-form-wrap">
        {status === 'success' ? (
          <div className="signup-success" aria-live="polite">
            <CheckCircle2 size={32} />
            <h3>You’re on the list.</h3>
            <p>We’ll email you when there’s news about trying Hallzee.</p>
          </div>
        ) : (
          <form onSubmit={submit}>
            <label htmlFor="email">Email address</label>
            <div className="signup-controls">
              <input
                id="email"
                name="email"
                type="email"
                placeholder="you@school.edu"
                autoComplete="email"
                required
                maxLength={254}
                aria-describedby="email-note signup-error"
                disabled={status === 'sending'}
              />
              <button
                className="button button-lime"
                type="submit"
                disabled={status === 'sending'}
              >
                {status === 'sending' ? 'Adding you…' : 'Join the waiting list'}
                <ArrowRight size={18} />
              </button>
            </div>
            <div className="honeypot" aria-hidden="true">
              <label htmlFor="website">Leave this empty</label>
              <input
                id="website"
                name="website"
                type="text"
                autoComplete="off"
                tabIndex={-1}
              />
            </div>
            <p id="email-note">
              By joining, you agree to receive Hallzee availability emails. You
              can <Link href="/leave-waitlist">leave the list</Link> at any
              time. Read our <Link href="/privacy">privacy notice</Link>.
            </p>
            <p className="form-error" id="signup-error" role="alert">
              {status === 'error' ? message : ''}
            </p>
          </form>
        )}
      </div>
    </section>
  );
}
export default function Home() {
  const [theme, setTheme] = useState<ThemeMode>('logo-vibrant');
  const [bgPreset, setBgPreset] = useState<BackgroundPreset>('waves');

  useEffect(() => {
    try {
      const params = new URLSearchParams(window.location.search);
      const urlTheme = params.get('theme') as ThemeMode | null;
      const urlBg = params.get('bg') as BackgroundPreset | null;

      const savedTheme = (urlTheme || localStorage.getItem('hallzee_site_theme') || 'logo-vibrant') as ThemeMode;
      const savedBg = (urlBg || localStorage.getItem('hallzee_site_bg') || 'waves') as BackgroundPreset;

      setTheme(savedTheme);
      setBgPreset(savedBg);
      document.documentElement.setAttribute('data-theme', savedTheme);
      document.documentElement.setAttribute('data-bg', savedBg);
    } catch {}
  }, []);

  const handleThemeChange = (newTheme: ThemeMode) => {
    setTheme(newTheme);
    document.documentElement.setAttribute('data-theme', newTheme);
    try {
      localStorage.setItem('hallzee_site_theme', newTheme);
    } catch {}
  };

  const handleBgChange = (newBg: BackgroundPreset) => {
    setBgPreset(newBg);
    document.documentElement.setAttribute('data-bg', newBg);
    try {
      localStorage.setItem('hallzee_site_bg', newBg);
    } catch {}
  };

  return (
    <>
      <Link className="skip-link" href="#main">
        Skip to content
      </Link>
      <header className="site-header">
        <div className="header-inner">
          <Brand />
          <nav aria-label="Main navigation" style={{ display: 'flex', alignItems: 'center', gap: '20px' }}>
            <Link href="#how-it-works">How it works</Link>
            <Link href="#software">The software</Link>
            <ThemeHeaderToggle currentTheme={theme} onThemeChange={handleThemeChange} />
            <Link className="nav-cta" href="#waitlist">
              Join the waiting list <ArrowUpRight size={16} />
            </Link>
          </nav>
        </div>
      </header>
      <main id="main">
        <section className="hero section-width" id="top">
          <div className="hero-copy">
            <p className="eyebrow">
              <span className="eyebrow-line" /> FOR THE CLASSROOM
            </p>
            <h1>The hall pass system that handles everything for you.</h1>
            <p className="hero-description">
              Students sign themselves out and back in at the keypad. Hallzee
              shows you who’s out, how long they’ve been gone, and the trip
              history beside your class schedule.
            </p>
            <div className="hero-actions">
              <Link href="#waitlist" className="button button-primary">
                Join the waiting list <ArrowRight size={18} />
              </Link>
              <Link href="#software" className="text-link">
                Take a closer look <ArrowDown size={16} />
              </Link>
            </div>
            <p className="development-note">
              <span /> In development · A terminal + a desktop app
            </p>
          </div>
          <div className="hero-visual">
            <div className="hardware-image-slot">
              <div className="hardware-label">
                <span>01 / THE TERMINAL</span>
                <Bluetooth size={18} />
              </div>
              <Image
                unoptimized
                className="hardware-image"
                src="/hallzee-terminal.png"
                alt="A blue Hallzee terminal mounted on an interior classroom wall, with a color display showing Available, a 12-key keypad, and a side-connected power cable."
                width="896"
                height="1200"
                fetchPriority="high"
              />
            </div>
            <div className="hero-visual-caption">
              <span>At the door, for students.</span>
              <span>In development</span>
            </div>
          </div>
        </section>
        <section className="student-flow section-width" id="how-it-works">
          <div className="flow-intro">
            <p className="eyebrow">AT THE KEYPAD</p>
            <h2>A routine students can do themselves.</h2>
          </div>
          <ol className="photo-steps">
            <li>
              <Image
                unoptimized
                className="step-photo"
                src="/sequence/sign-out.png"
                alt="A student presses the keypad on a blue Hallzee terminal mounted against the classroom wall and connected by cable."
                width={1448}
                height={1086}
              />
              <div className="step-caption">
                <span className="step-number">1</span>
                <div>
                  <h3>Enter an ID. Press #.</h3>
                  <p>Hallzee checks your classroom’s pass rules.</p>
                </div>
              </div>
            </li>
            <li>
              <Image
                unoptimized
                className="step-photo"
                src="/sequence/away.png"
                alt="The full Hallzee dashboard on a laptop, including the sidebar, active trip timer, recent trips, pass rules, and longer trips."
                width={1448}
                height={1086}
              />
              <div className="step-caption">
                <span className="step-number">2</span>
                <div>
                  <h3>The trip appears in the app.</h3>
                  <p>The timer runs while the student is out.</p>
                </div>
              </div>
            </li>
            <li>
              <Image
                unoptimized
                className="step-photo"
                src="/sequence/check-back-in.png"
                alt="A student checks back in at the wall-mounted, cable-connected Hallzee terminal while its display still shows Occupied."
                width={1449}
                height={1086}
              />
              <div className="step-caption">
                <span className="step-number">3</span>
                <div>
                  <h3>Back? Enter the same ID.</h3>
                  <p>Press # to check back in.</p>
                </div>
              </div>
            </li>
          </ol>
          <div className="flow-footnotes">
            <p>Students don’t need a phone, an app, or a login.</p>
            <span>Illustrated classroom scenes · Sample student data</span>
          </div>
        </section>
        <section
          className="student-feedback section-width"
          aria-labelledby="feedback-title"
        >
          <div>
            <h2 id="feedback-title">Immediate feedback for students</h2>
            <p>
              When students check back in, the screen shows exactly how long
              they were away - without you having to point it out.
            </p>
          </div>
          <figure className="return-screen">
            <Image
              unoptimized
              src="/terminal/checked-in-orange.png"
              alt="A blue Hallzee terminal mounted on a wall, with its screen showing CHECKED IN, Time away, and an example duration of 6m 07s."
              width={1448}
              height={1086}
            />
            <figcaption>Terminal return screen · Example duration</figcaption>
          </figure>
        </section>
        <section className="software-section" id="software">
          <div className="section-width">
            <div className="section-heading">
              <div>
                <p className="eyebrow">ON YOUR COMPUTER</p>
                <h2>Today’s trips and the patterns over time.</h2>
              </div>
              <p>
                During a lesson, between periods, or when you need to follow up
                with a student.
              </p>
            </div>
            <Tabs defaultValue="now" className="feature-tabs">
              <TabsList
                className="feature-tab-list"
                aria-label="Explore Hallzee’s software"
              >
                {features.map((f) => (
                  <TabsTrigger className="feature-tab" key={f.id} value={f.id}>
                    {f.label}
                  </TabsTrigger>
                ))}
              </TabsList>
              {features.map((f) => (
                <TabsContent
                  key={f.id}
                  value={f.id}
                  className="feature-panel"
                  data-view={f.id}
                >
                  <div className="feature-copy">
                    <span className="feature-number">{f.number}</span>
                    <h3>{f.title}</h3>
                    <p>{f.text}</p>
                    {f.detail && <p className="feature-detail">{f.detail}</p>}
                  </div>
                  <ClientView type={f.id} />
                </TabsContent>
              ))}
            </Tabs>
          </div>
        </section>
        <section className="practical section-width" id="details">
          <div className="section-heading">
            <div>
              <p className="eyebrow">A FEW PRACTICAL DETAILS</p>
              <h2>
                Made for a classroom,
                <br />
                down to the setup.
              </h2>
            </div>
          </div>
          <div className="practical-grid">
            <article>
              <Bluetooth size={26} strokeWidth={1.5} />
              <h3>No school Wi-Fi setup.</h3>
              <p>
                The terminal connects to your computer over Bluetooth. It keeps
                recording trips if the connection drops, then copies them over
                when you reconnect.
              </p>
            </article>
            <article>
              <Users size={26} strokeWidth={1.5} />
              <h3>Your roster stays on your computer.</h3>
              <p>
                Import a CSV with student IDs and names. Hallzee matches names
                to trips in the desktop app; the terminal uses IDs. The core
                pass system doesn’t need a cloud account.
              </p>
            </article>
            <article>
              <Clock3 size={26} strokeWidth={1.5} />
              <h3>Set the rules for your classroom.</h3>
              <p>
                Choose how many students may be out at once, set a daily trip
                limit, and schedule no-pass windows around your bell times.
                Hallzee handles those checks when a student signs out.
              </p>
            </article>
          </div>
          <div className="faq-heading">
            <p className="eyebrow">COMMON QUESTIONS</p>
            <h2>Before you bring it into class.</h2>
          </div>
          <div className="faq-list">
            <details>
              <summary>What does the hardware look like?</summary>
              <p>
                Hallzee has a 3D-printed enclosure, a color display, and a flat
                12-key membrane keypad. Students use the physical keys, and the
                screen shows their check-out confirmation and time away. Hallzee
                is in development; the final enclosure may differ from the
                illustration shown here.
              </p>
            </details>
            <details>
              <summary>What computer do I need?</summary>
              <p>
                Hallzee’s desktop app will be available for Windows and Mac.
                Both connect to the terminal over Bluetooth. Students only use
                the terminal; they don’t need their own computer or phone.
              </p>
            </details>
            <details>
              <summary>Can Hallzee follow my classroom’s pass rules?</summary>
              <p>
                Yes. Set how many students can be out at once, a daily trip
                limit, and no-pass windows tied to your bell schedule. Hallzee
                applies those rules when students request a pass, including
                during the opening and closing minutes of class.
              </p>
            </details>
            <details>
              <summary>What if a student forgets to check back in?</summary>
              <p>
                You can end the trip from the desktop app. Hallzee marks it as a
                manual check-in so you can distinguish it from a student
                entering their ID at the terminal.
              </p>
            </details>
            <details>
              <summary>Can students use the IDs they already know?</summary>
              <p>
                Yes, Hallzee uses numeric student IDs. Students enter the same
                ID when leaving and returning. Import a roster to see the
                matching names in the desktop app, or use student IDs on their
                own.
              </p>
            </details>
            <details>
              <summary>
                Do I have to enter my roster one student at a time?
              </summary>
              <p>
                No. Import a CSV roster with student IDs and names. Hallzee
                previews the columns so you can check the match before
                importing. If your roster is in Excel, Numbers, or another
                school system, export it as a CSV first.
              </p>
            </details>
            <details>
              <summary>What happens if my computer disconnects?</summary>
              <p>
                The terminal keeps recording trips locally. When Bluetooth
                reconnects, those records copy into the desktop app. The live
                desktop view needs a connection to stay current.
              </p>
            </details>
            <details>
              <summary>Does Hallzee track students around the school?</summary>
              <p>
                No. It records when students sign out and back in at your
                classroom terminal. It does not use GPS or track where they go
                between those two moments.
              </p>
            </details>
            <details>
              <summary>How much will it cost, and when can I try it?</summary>
              <p>
                Hallzee is in development. Pricing and availability haven’t been
                announced yet. Join the waiting list to hear when there is an
                opportunity to try it.
              </p>
            </details>
            <details>
              <summary>Can I take my records with me?</summary>
              <p>
                Yes. Trip records are stored locally, and the desktop app can
                export a CSV for a spreadsheet. You can search your history by
                student, narrow it by date or duration, and distinguish
                completed trips from trips ended with a manual check-in or
                reset.
              </p>
            </details>
          </div>
        </section>
        <div className="section-width">
          <Signup />
        </div>
      </main>
      <section
        className="site-notice section-width"
        aria-label="Privacy and website information"
      >
        <div>
          <strong>Privacy &amp; website information</strong>
          <p>
            This waiting list is for adults interested in Hallzee. It doesn’t
            collect student records.
          </p>
        </div>
        <nav aria-label="Website information">
          <Link href="/privacy">Privacy</Link>
          <Link href="/terms">Website terms</Link>
          <Link href="/leave-waitlist">Leave the waiting list</Link>
        </nav>
      </section>
      <footer className="site-footer section-width">
        <Brand />
        <p>© 2026 Hallzee · Florida, US</p>
        <Link href="#top">
          Back to top <ArrowUpRight size={15} />
        </Link>
      </footer>
      <ThemeFloatingDock
        currentTheme={theme}
        onThemeChange={handleThemeChange}
        currentBg={bgPreset}
        onBgChange={handleBgChange}
      />
    </>
  );
}
