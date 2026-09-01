import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Hallzee Client Preview",
  description: "A browser simulation of the Hallzee classroom terminal client.",
  openGraph: {
    title: "Hallzee Client Preview",
    description: "A browser simulation of the Hallzee classroom terminal client.",
    images: ["/og.png"],
  },
  twitter: {
    card: "summary_large_image",
    title: "Hallzee Client Preview",
    description: "A browser simulation of the Hallzee classroom terminal client.",
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
      <body className={`${geistSans.variable} ${geistMono.variable} antialiased`}>
        {children}
      </body>
    </html>
  );
}
