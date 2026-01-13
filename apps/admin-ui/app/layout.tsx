import type { Metadata } from "next";
import { Fraunces, Space_Grotesk } from "next/font/google";
import { AdminSettingsProvider } from "@/lib/admin-settings";
import "./globals.css";

const display = Fraunces({
  subsets: ["latin"],
  weight: ["600", "700", "800"],
  variable: "--font-display",
});

const body = Space_Grotesk({
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
  variable: "--font-body",
});

export const metadata: Metadata = {
  title: "Embeddra Kontrol Merkezi",
  description: "Embeddra platform ve kiracı panelleri",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="tr" data-theme="light">
      <body className={`${body.variable} ${display.variable} antialiased`}>
        <AdminSettingsProvider>
          {children}
        </AdminSettingsProvider>
      </body>
    </html>
  );
}
