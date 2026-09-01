import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "Hallzee Preview",
  description: "A browser simulation of the Hallzee desktop sync client.",
  openGraph: {
    title: "Hallzee Preview",
    description: "A browser simulation of the Hallzee desktop sync client.",
    images: ["/og.png"],
  },
  twitter: {
    card: "summary_large_image",
    title: "Hallzee Preview",
    description: "A browser simulation of the Hallzee desktop sync client.",
    images: ["/og.png"],
  },
  icons: {
    icon: "/favicon.svg",
    shortcut: "/favicon.svg",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body
        className={`${geistSans.variable} ${geistMono.variable} antialiased`}
      >
        {children}
      </body>
    </html>
  );
}
