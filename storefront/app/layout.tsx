import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import Link from "next/link";
import "./globals.css";
import { getCategoryTree } from "@/lib/api";
import CategoryNav from "@/components/CategoryNav";
import SearchBar from "@/components/SearchBar";
import UserMenu from "@/components/UserMenu";
import CartButton from "@/components/CartButton";
import Providers from "@/components/Providers";

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
        <Providers>
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
                <CartButton />
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
                <li>
                  <Link href="/account/orders" className="hover:text-white">
                    My orders
                  </Link>
                </li>
                <li>
                  <Link href="/account/wishlist" className="hover:text-white">
                    Wishlist
                  </Link>
                </li>
                <li>
                  <Link href="/cart" className="hover:text-white">
                    Cart
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
        </Providers>
      </body>
    </html>
  );
}
