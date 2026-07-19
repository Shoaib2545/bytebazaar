"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { FormEvent, useEffect, useRef, useState } from "react";
import { useCart } from "@/components/Providers";
import { Address, getAddresses } from "@/lib/account";
import { useAuth } from "@/lib/auth-client";
import {
  ShippingOption,
  getShippingOptions,
  placeOrder,
  storeOrderSummary,
} from "@/lib/checkout";
import { formatPrice } from "@/lib/format";

const MAJOR_CITIES = [
  "Karachi",
  "Lahore",
  "Islamabad",
  "Rawalpindi",
  "Faisalabad",
  "Multan",
  "Peshawar",
  "Quetta",
  "Sialkot",
  "Gujranwala",
  "Hyderabad",
  "Bahawalpur",
  "Sargodha",
  "Abbottabad",
];

const OTHER_CITY = "__other__";

const REGIONS = [
  "Punjab",
  "Sindh",
  "Khyber Pakhtunkhwa",
  "Balochistan",
  "Islamabad Capital Territory",
  "Gilgit-Baltistan",
  "Azad Jammu & Kashmir",
];

const inputCls =
  "w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-orange-500 focus:outline-none focus:ring-1 focus:ring-orange-500";

export default function CheckoutPage() {
  const router = useRouter();
  const { cart, loading: cartLoading, refresh } = useCart();
  const { status, user } = useAuth();

  const [fullName, setFullName] = useState("");
  const [phone, setPhone] = useState("");
  const [email, setEmail] = useState("");
  const [addressLine, setAddressLine] = useState("");
  const [citySelect, setCitySelect] = useState(MAJOR_CITIES[0]);
  const [otherCity, setOtherCity] = useState("");
  const [region, setRegion] = useState(REGIONS[0]);
  const [notes, setNotes] = useState("");

  const [options, setOptions] = useState<ShippingOption[]>([]);
  const [optionsError, setOptionsError] = useState(false);
  const [shippingCode, setShippingCode] = useState("");

  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [placed, setPlaced] = useState(false);
  const prefilledRef = useRef(false);

  // Shipping options
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const opts = await getShippingOptions();
        if (cancelled) return;
        setOptions(opts);
        setOptionsError(false);
        if (opts.length > 0) {
          setShippingCode((prev) =>
            prev && opts.some((o) => o.code === prev) ? prev : opts[0].code
          );
        }
      } catch {
        if (!cancelled) setOptionsError(true);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  // Prefill for authenticated users (profile + default address), once.
  useEffect(() => {
    if (status !== "authenticated" || !user || prefilledRef.current) return;
    prefilledRef.current = true;
    (async () => {
      let preferred: Address | null = null;
      try {
        const addresses = await getAddresses();
        preferred = addresses.find((a) => a.isDefault) ?? addresses[0] ?? null;
      } catch {
        // prefill is best-effort
      }
      setFullName((v) => v || preferred?.fullName || user.fullName);
      setEmail((v) => v || user.email);
      const pref = preferred;
      if (!pref) return;
      setPhone((v) => v || pref.phone);
      setAddressLine((v) => v || pref.addressLine);
      setRegion((v) => (REGIONS.includes(pref.region) ? pref.region : v));
      if (MAJOR_CITIES.includes(pref.city)) {
        setCitySelect(pref.city);
      } else if (pref.city) {
        setCitySelect(OTHER_CITY);
        setOtherCity(pref.city);
      }
    })();
  }, [status, user]);

  const selectedOption = options.find((o) => o.code === shippingCode) ?? null;
  const shippingFee = selectedOption?.fee ?? 0;
  // cart.total = subtotal - discount (server-computed)
  const total = cart.total + shippingFee;
  const city = citySelect === OTHER_CITY ? otherCity.trim() : citySelect;

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (busy) return;
    setError(null);

    if (!city) {
      setError("Please enter your city.");
      return;
    }
    if (!selectedOption) {
      setError("Please select a shipping option.");
      return;
    }

    setBusy(true);
    try {
      const result = await placeOrder({
        fullName: fullName.trim(),
        phone: phone.trim(),
        email: email.trim(),
        addressLine: addressLine.trim(),
        city,
        region,
        shippingCode: selectedOption.code,
        paymentMethod: "COD",
        notes: notes.trim() || undefined,
      });
      setPlaced(true);
      storeOrderSummary({
        orderNumber: result.orderNumber,
        total: result.total,
        status: result.status,
        email: email.trim(),
        fullName: fullName.trim(),
        placedAt: new Date().toISOString(),
        couponCode: result.couponCode ?? null,
        discount: result.discount ?? 0,
      });
      refresh(); // server cleared the cart
      router.push(`/order-confirmation/${encodeURIComponent(result.orderNumber)}`);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Could not place the order. Please try again."
      );
      setBusy(false);
    }
  }

  if (!cartLoading && cart.items.length === 0 && !placed) {
    return (
      <div className="mx-auto max-w-4xl px-4 py-16 text-center">
        <h1 className="text-2xl font-bold text-blue-950">Checkout</h1>
        <p className="mt-3 text-sm text-slate-500">
          Your cart is empty — add some products before checking out.
        </p>
        <Link
          href="/"
          className="mt-6 inline-block rounded-md bg-orange-500 px-6 py-2.5 text-sm font-semibold text-white transition hover:bg-orange-600"
        >
          Continue Shopping
        </Link>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-8">
      <h1 className="text-2xl font-bold text-blue-950">Checkout</h1>
      {status === "guest" && (
        <p className="mt-1 text-sm text-slate-500">
          Checking out as guest.{" "}
          <Link
            href="/login?next=/checkout"
            className="font-semibold text-orange-600 hover:underline"
          >
            Sign in
          </Link>{" "}
          to track your order in your account.
        </p>
      )}

      <form onSubmit={onSubmit} className="mt-6 grid gap-8 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          {/* Delivery details */}
          <section className="rounded-lg border border-slate-200 bg-white p-6">
            <h2 className="text-sm font-bold uppercase tracking-wide text-blue-950">
              Delivery Details
            </h2>
            <div className="mt-4 grid gap-4 sm:grid-cols-2">
              <div>
                <label htmlFor="fullName" className="mb-1 block text-sm font-medium text-slate-700">
                  Full name *
                </label>
                <input
                  id="fullName"
                  type="text"
                  required
                  autoComplete="name"
                  value={fullName}
                  onChange={(e) => setFullName(e.target.value)}
                  className={inputCls}
                />
              </div>
              <div>
                <label htmlFor="phone" className="mb-1 block text-sm font-medium text-slate-700">
                  Phone *
                </label>
                <input
                  id="phone"
                  type="tel"
                  required
                  autoComplete="tel"
                  placeholder="03xx-xxxxxxx"
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  className={inputCls}
                />
              </div>
              <div className="sm:col-span-2">
                <label htmlFor="email" className="mb-1 block text-sm font-medium text-slate-700">
                  Email *
                </label>
                <input
                  id="email"
                  type="email"
                  required
                  autoComplete="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className={inputCls}
                />
              </div>
              <div className="sm:col-span-2">
                <label htmlFor="addressLine" className="mb-1 block text-sm font-medium text-slate-700">
                  Street address *
                </label>
                <input
                  id="addressLine"
                  type="text"
                  required
                  autoComplete="street-address"
                  placeholder="House #, street, area"
                  value={addressLine}
                  onChange={(e) => setAddressLine(e.target.value)}
                  className={inputCls}
                />
              </div>
              <div>
                <label htmlFor="city" className="mb-1 block text-sm font-medium text-slate-700">
                  City *
                </label>
                <select
                  id="city"
                  value={citySelect}
                  onChange={(e) => setCitySelect(e.target.value)}
                  className={inputCls}
                >
                  {MAJOR_CITIES.map((c) => (
                    <option key={c} value={c}>
                      {c}
                    </option>
                  ))}
                  <option value={OTHER_CITY}>Other city...</option>
                </select>
                {citySelect === OTHER_CITY && (
                  <input
                    type="text"
                    required
                    placeholder="Enter your city"
                    aria-label="Other city"
                    value={otherCity}
                    onChange={(e) => setOtherCity(e.target.value)}
                    className={`${inputCls} mt-2`}
                  />
                )}
              </div>
              <div>
                <label htmlFor="region" className="mb-1 block text-sm font-medium text-slate-700">
                  Region / Province *
                </label>
                <select
                  id="region"
                  value={region}
                  onChange={(e) => setRegion(e.target.value)}
                  className={inputCls}
                >
                  {REGIONS.map((r) => (
                    <option key={r} value={r}>
                      {r}
                    </option>
                  ))}
                </select>
              </div>
              <div className="sm:col-span-2">
                <label htmlFor="notes" className="mb-1 block text-sm font-medium text-slate-700">
                  Order notes (optional)
                </label>
                <textarea
                  id="notes"
                  rows={2}
                  placeholder="Anything the rider should know?"
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  className={inputCls}
                />
              </div>
            </div>
          </section>

          {/* Shipping */}
          <section className="rounded-lg border border-slate-200 bg-white p-6">
            <h2 className="text-sm font-bold uppercase tracking-wide text-blue-950">
              Shipping Method
            </h2>
            {optionsError ? (
              <p className="mt-3 text-sm text-red-600">
                Could not load shipping options — please refresh the page.
              </p>
            ) : options.length === 0 ? (
              <p className="mt-3 text-sm text-slate-400">
                Loading shipping options...
              </p>
            ) : (
              <div className="mt-4 space-y-2">
                {options.map((opt) => (
                  <label
                    key={opt.code}
                    className={`flex cursor-pointer items-center justify-between rounded-md border px-4 py-3 text-sm transition ${
                      shippingCode === opt.code
                        ? "border-orange-500 bg-orange-50"
                        : "border-slate-200 hover:border-slate-300"
                    }`}
                  >
                    <span className="flex items-center gap-3">
                      <input
                        type="radio"
                        name="shipping"
                        value={opt.code}
                        checked={shippingCode === opt.code}
                        onChange={() => setShippingCode(opt.code)}
                        className="accent-orange-500"
                      />
                      <span className="font-medium text-slate-800">
                        {opt.name}
                      </span>
                    </span>
                    <span className="font-semibold text-blue-950">
                      {formatPrice(opt.fee)}
                    </span>
                  </label>
                ))}
              </div>
            )}
          </section>

          {/* Payment */}
          <section className="rounded-lg border border-slate-200 bg-white p-6">
            <h2 className="text-sm font-bold uppercase tracking-wide text-blue-950">
              Payment Method
            </h2>
            <label className="mt-4 flex cursor-pointer items-center gap-3 rounded-md border border-orange-500 bg-orange-50 px-4 py-3 text-sm">
              <input
                type="radio"
                name="payment"
                value="COD"
                checked
                readOnly
                className="accent-orange-500"
              />
              <span>
                <span className="font-medium text-slate-800">
                  Cash on Delivery
                </span>
                <span className="mt-0.5 block text-xs text-slate-500">
                  Pay in cash when your order arrives.
                </span>
              </span>
            </label>
          </section>
        </div>

        {/* Summary */}
        <div className="h-fit rounded-lg border border-slate-200 bg-white p-5 lg:sticky lg:top-24">
          <h2 className="text-sm font-bold uppercase tracking-wide text-blue-950">
            Order Summary
          </h2>
          <ul className="mt-4 max-h-64 space-y-3 overflow-y-auto text-sm">
            {cart.items.map((item) => (
              <li key={item.productId} className="flex justify-between gap-3">
                <span className="line-clamp-2 text-slate-600">
                  {item.name}{" "}
                  <span className="text-slate-400">× {item.quantity}</span>
                </span>
                <span className="shrink-0 font-medium text-slate-800">
                  {formatPrice(item.lineTotal)}
                </span>
              </li>
            ))}
          </ul>
          <dl className="mt-4 space-y-2 border-t border-slate-100 pt-4 text-sm">
            <div className="flex justify-between">
              <dt className="text-slate-500">Subtotal</dt>
              <dd className="font-semibold text-slate-800">
                {formatPrice(cart.subtotal)}
              </dd>
            </div>
            {cart.discount > 0 && (
              <div className="flex justify-between">
                <dt className="text-slate-500">
                  Discount{cart.couponCode ? ` (${cart.couponCode})` : ""}
                </dt>
                <dd className="font-semibold text-green-600">
                  −{formatPrice(cart.discount)}
                </dd>
              </div>
            )}
            <div className="flex justify-between">
              <dt className="text-slate-500">
                Shipping{selectedOption ? ` (${selectedOption.name})` : ""}
              </dt>
              <dd className="font-semibold text-slate-800">
                {selectedOption ? formatPrice(shippingFee) : "—"}
              </dd>
            </div>
            <div className="flex justify-between border-t border-slate-100 pt-2 text-base">
              <dt className="font-bold text-blue-950">Total</dt>
              <dd className="font-extrabold text-blue-950">
                {formatPrice(total)}
              </dd>
            </div>
          </dl>

          {error && (
            <div className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {error}
            </div>
          )}

          <button
            type="submit"
            disabled={busy || cartLoading || options.length === 0}
            className="mt-5 w-full rounded-md bg-orange-500 py-3 text-sm font-semibold text-white transition hover:bg-orange-600 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {busy ? "Placing order..." : `Place Order — ${formatPrice(total)}`}
          </button>
          <p className="mt-2 text-center text-xs text-slate-400">
            Cash on Delivery — you only pay when it arrives.
          </p>
        </div>
      </form>
    </div>
  );
}
