import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  icons: { icon: '/hallzee-logo.png' },
  title: 'Hallzee - The hall pass system that handles everything for you',
  description:
    'A classroom terminal and desktop app for student checkouts, live pass timers, bell schedules, and searchable trip history. Join the Hallzee waiting list.',
  openGraph: {
    title: 'Hallzee - The hall pass system that handles everything for you',
    description:
      'A classroom terminal and desktop app for student checkouts, live pass timers, bell schedules, and searchable trip history. Join the Hallzee waiting list.',
    images: ['/hallzee-logo.png'],
  },
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
