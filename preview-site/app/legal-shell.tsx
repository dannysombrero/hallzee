import Link from 'next/link';
import Image from 'next/image';
export function LegalShell({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <>
      <header className="site-header">
        <div className="header-inner">
          <Link className="brand" href="/" aria-label="Hallzee home">
            <Image
              unoptimized
              src="/hallzee-logo.png"
              alt=""
              width={29}
              height={32}
            />
            <span>
              hallzee<span className="brand-dot">.</span>
            </span>
          </Link>
          <Link className="text-link" href="/">
            Back to Hallzee
          </Link>
        </div>
      </header>
      <main className="legal-page">
        <p className="eyebrow">HALLZEE · FLORIDA, US</p>
        <h1>{title}</h1>
        {children}
      </main>
      <footer className="legal-footer section-width">
        <span>© {new Date().getFullYear()} Hallzee</span>
        <nav aria-label="Footer">
          <Link href="/privacy">Privacy</Link>
          <Link href="/terms">Website terms</Link>
          <Link href="/leave-waitlist">Leave the waiting list</Link>
        </nav>
      </footer>
    </>
  );
}
