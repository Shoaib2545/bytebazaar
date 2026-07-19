import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import Link from "next/link";
import "./globals.css";
import { getCategoryTree } from "@/lib/api";
import CategoryNav from "@/components/CategoryNav";
import SearchBar from "@/components/SearchBar";
import UserMenu from "@/components/UserMenu";

// The header nav is built from live API data — render dynamically so
// `next build` succeeds even when the API is down.
export const dynamic = "force-dynamic";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: {
    default: "ByteBazaar — PC Hardware & Electronics Store",
    template: "%s | ByteBazaar",
  },
  description:
    "ByteBazaar — Pakistan's PC hardware and electronics store. Graphics cards, processors, laptops and more at the best prices in PKR.",
};

export default async function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const tree = await getCategoryTree();

  return (
    <html
      lang="en"
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
    >
      <body className="flex min-h-full flex-col bg-slate-100 text-slate-900">
        <header className="sticky top-0 z-50 shadow-md">
          <div className="bg-blue-950">
            <div className="mx-auto flex max-w-7xl items-center gap-4 px-4 py-3">
              <Link
                href="/"
                className="shrink-0 text-2xl font-extrabold tracking-tight text-white"
              >
                Byte<span className="text-orange-500">Bazaar</span>
              </Link>

              <div className="hidden flex-1 justify-center md:flex">
                <SearchBar />
              </div>

              <div className="ml-auto flex items-center gap-3">
                {/* Cart placeholder */}
                <button
                  type="button"
                  aria-label="Cart (coming soon)"
                  title="Cart (coming soon)"
                  className="relative rounded-md p-2 text-white/90 transition hover:bg-white/10"
                >
                  <svg
                    xmlns="http://www.w3.org/2000/svg"
                    fill="none"
                    viewBox="0 0 24 24"
                    strokeWidth={1.5}
                    stroke="currentColor"
                    className="h-6 w-6"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      d="M2.25 3h1.386c.51 0 .955.343 1.087.835l.383 1.437M7.5 14.25a3 3 0 0 0-3 3h15.75m-12.75-3h11.218c1.121-2.3 2.1-4.684 2.924-7.138a60.114 60.114 0 0 0-16.536-1.84M7.5 14.25 5.106 5.272M6 20.25a.75.75 0 1 1-1.5 0 .75.75 0 0 1 1.5 0Zm12.75 0a.75.75 0 1 1-1.5 0 .75.75 0 0 1 1.5 0Z"
                    />
                  </svg>
                  <span className="absolute -right-0.5 -top-0.5 flex h-4 w-4 items-center justify-center rounded-full bg-orange-500 text-[10px] font-bold text-white">
                    0
                  </span>
                </button>
                <UserMenu />
              </div>
            </div>

            {/* Mobile search */}
            <div className="px-4 pb-3 md:hidden">
              <SearchBar />
            </div>
          </div>

          <CategoryNav tree={tree} />
        </header>

        <main className="flex-1">{children}</main>

        <footer className="mt-12 bg-blue-950 text-white">
          <div className="mx-auto grid max-w-7xl gap-8 px-4 py-10 sm:grid-cols-2 lg:grid-cols-4">
            <div>
              <div className="text-xl font-extrabold">
                Byte<span className="text-orange-500">Bazaar</span>
              </div>
              <p className="mt-2 text-sm text-white/70">
                Pakistan&apos;s destination for PC hardware, components and
                electronics — genuine products at honest prices.
              </p>
            </div>
            <div>
              <h4 className="mb-3 text-sm font-semibold uppercase tracking-wide text-orange-400">
                Shop
              </h4>
              <ul className="space-y-2 text-sm text-white/70">
                {tree.slice(0, 5).map((c) => (
                  <li key={c.id}>
                    <Link
                      href={`/category/${c.slug}`}
                      className="hover:text-white"
                    >
                      {c.name}
                    </Link>
                  </li>
                ))}
                {tree.length === 0 && <li>Categories coming soon</li>}
              </ul>
            </div>
            <div>
              <h4 className="mb-3 text-sm font-semibold uppercase tracking-wide text-orange-400">
                Account
              </h4>
              <ul className="space-y-2 text-sm text-white/70">
                <li>
                  <Link href="/login" className="hover:text-white">
                    Login
                  </Link>
                </li>
                <li>
                  <Link href="/register" className="hover:text-white">
                    Create account
                  </Link>
                </li>
              </ul>
            </div>
            <div>
              <h4 className="mb-3 text-sm font-semibold uppercase tracking-wide text-orange-400">
                Contact
              </h4>
              <ul className="space-y-2 text-sm text-white/70">
                <li>support@bytebazaar.local</li>
                <li>Mon–Sat, 10am–8pm PKT</li>
              </ul>
            </div>
          </div>
          <div className="border-t border-white/10 py-4 text-center text-xs text-white/50">
            © {new Date().getFullYear()} ByteBazaar. All prices in PKR.
          </div>
        </footer>
      </body>
    </html>
  );
}
