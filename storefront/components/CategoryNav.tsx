import Link from "next/link";
import { CategoryNode } from "@/lib/api";

/**
 * Category navigation bar with a CSS-hover mega-menu built from the
 * category tree. Rendered on the server; no client JS needed.
 */
export default function CategoryNav({ tree }: { tree: CategoryNode[] }) {
  if (!tree.length) {
    return (
      <nav className="border-t border-white/10 bg-blue-950">
        <div className="mx-auto max-w-7xl px-4 py-2 text-xs text-white/50">
          Categories unavailable
        </div>
      </nav>
    );
  }

  return (
    <nav className="border-t border-white/10 bg-blue-950">
      <div className="mx-auto flex max-w-7xl items-stretch gap-1 overflow-x-auto px-4">
        {tree.map((cat) => (
          <div key={cat.id} className="group relative">
            <Link
              href={`/category/${cat.slug}`}
              className="flex items-center gap-1 whitespace-nowrap px-3 py-2.5 text-sm font-medium text-white/90 transition hover:bg-white/10 hover:text-orange-400"
            >
              {cat.name}
              {cat.children.length > 0 && (
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  viewBox="0 0 20 20"
                  fill="currentColor"
                  className="h-3.5 w-3.5 opacity-60"
                >
                  <path
                    fillRule="evenodd"
                    d="M5.22 8.22a.75.75 0 0 1 1.06 0L10 11.94l3.72-3.72a.75.75 0 1 1 1.06 1.06l-4.25 4.25a.75.75 0 0 1-1.06 0L5.22 9.28a.75.75 0 0 1 0-1.06Z"
                    clipRule="evenodd"
                  />
                </svg>
              )}
            </Link>

            {cat.children.length > 0 && (
              <div className="invisible absolute left-0 top-full z-40 min-w-[520px] rounded-b-lg border border-slate-200 bg-white opacity-0 shadow-xl transition-all duration-150 group-hover:visible group-hover:opacity-100">
                <div className="grid grid-cols-2 gap-x-8 gap-y-4 p-5 lg:grid-cols-3">
                  {cat.children.map((child) => (
                    <div key={child.id}>
                      <Link
                        href={`/category/${child.slug}`}
                        className="block text-sm font-semibold text-blue-950 hover:text-orange-600"
                      >
                        {child.name}
                      </Link>
                      {child.children.length > 0 && (
                        <ul className="mt-1.5 space-y-1">
                          {child.children.map((grand) => (
                            <li key={grand.id}>
                              <Link
                                href={`/category/${grand.slug}`}
                                className="block text-xs text-slate-600 hover:text-orange-600"
                              >
                                {grand.name}
                              </Link>
                            </li>
                          ))}
                        </ul>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        ))}
      </div>
    </nav>
  );
}
