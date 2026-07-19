"use client";

import { FormEvent, useEffect, useState } from "react";
import {
  Address,
  AddressInput,
  createAddress,
  deleteAddress,
  getAddresses,
  updateAddress,
} from "@/lib/account";

const EMPTY_FORM: AddressInput = {
  fullName: "",
  phone: "",
  addressLine: "",
  city: "",
  region: "",
  isDefault: false,
};

const inputCls =
  "w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-orange-500 focus:outline-none focus:ring-1 focus:ring-orange-500";

export default function AddressesPage() {
  const [addresses, setAddresses] = useState<Address[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  // form state: null = closed, "new" = creating, otherwise editing that id
  const [editing, setEditing] = useState<string | null>(null);
  const [form, setForm] = useState<AddressInput>(EMPTY_FORM);
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;
    getAddresses()
      .then((res) => {
        if (!cancelled) setAddresses(res);
      })
      .catch((err) => {
        if (!cancelled)
          setLoadError(
            err instanceof Error ? err.message : "Could not load addresses."
          );
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  function openNew() {
    setForm(EMPTY_FORM);
    setFormError(null);
    setEditing("new");
  }

  function openEdit(address: Address) {
    setForm({
      fullName: address.fullName,
      phone: address.phone,
      addressLine: address.addressLine,
      city: address.city,
      region: address.region,
      isDefault: address.isDefault,
    });
    setFormError(null);
    setEditing(address.id);
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (busy || !editing) return;
    setBusy(true);
    setFormError(null);
    try {
      if (editing === "new") {
        await createAddress(form);
      } else {
        await updateAddress(editing, form);
      }
      setAddresses(await getAddresses());
      setEditing(null);
    } catch (err) {
      setFormError(
        err instanceof Error ? err.message : "Could not save the address."
      );
    } finally {
      setBusy(false);
    }
  }

  async function onDelete(id: string) {
    if (!window.confirm("Delete this address?")) return;
    try {
      await deleteAddress(id);
      setAddresses((prev) => prev.filter((a) => a.id !== id));
    } catch (err) {
      setLoadError(
        err instanceof Error ? err.message : "Could not delete the address."
      );
    }
  }

  return (
    <div>
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold text-blue-950">My Addresses</h1>
        <button
          type="button"
          onClick={openNew}
          className="rounded-md bg-orange-500 px-4 py-2 text-sm font-semibold text-white transition hover:bg-orange-600"
        >
          Add Address
        </button>
      </div>

      {loadError && (
        <div className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {loadError}
        </div>
      )}

      {editing !== null && (
        <form
          onSubmit={onSubmit}
          className="mt-6 rounded-lg border border-orange-200 bg-white p-6"
        >
          <h2 className="text-sm font-bold uppercase tracking-wide text-blue-950">
            {editing === "new" ? "New Address" : "Edit Address"}
          </h2>
          {formError && (
            <div className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {formError}
            </div>
          )}
          <div className="mt-4 grid gap-4 sm:grid-cols-2">
            <div>
              <label htmlFor="a-fullName" className="mb-1 block text-sm font-medium text-slate-700">
                Full name *
              </label>
              <input
                id="a-fullName"
                type="text"
                required
                value={form.fullName}
                onChange={(e) => setForm({ ...form, fullName: e.target.value })}
                className={inputCls}
              />
            </div>
            <div>
              <label htmlFor="a-phone" className="mb-1 block text-sm font-medium text-slate-700">
                Phone *
              </label>
              <input
                id="a-phone"
                type="tel"
                required
                value={form.phone}
                onChange={(e) => setForm({ ...form, phone: e.target.value })}
                className={inputCls}
              />
            </div>
            <div className="sm:col-span-2">
              <label htmlFor="a-addressLine" className="mb-1 block text-sm font-medium text-slate-700">
                Street address *
              </label>
              <input
                id="a-addressLine"
                type="text"
                required
                value={form.addressLine}
                onChange={(e) =>
                  setForm({ ...form, addressLine: e.target.value })
                }
                className={inputCls}
              />
            </div>
            <div>
              <label htmlFor="a-city" className="mb-1 block text-sm font-medium text-slate-700">
                City *
              </label>
              <input
                id="a-city"
                type="text"
                required
                value={form.city}
                onChange={(e) => setForm({ ...form, city: e.target.value })}
                className={inputCls}
              />
            </div>
            <div>
              <label htmlFor="a-region" className="mb-1 block text-sm font-medium text-slate-700">
                Region / Province *
              </label>
              <input
                id="a-region"
                type="text"
                required
                value={form.region}
                onChange={(e) => setForm({ ...form, region: e.target.value })}
                className={inputCls}
              />
            </div>
            <label className="flex items-center gap-2 text-sm text-slate-700 sm:col-span-2">
              <input
                type="checkbox"
                checked={form.isDefault}
                onChange={(e) =>
                  setForm({ ...form, isDefault: e.target.checked })
                }
                className="accent-orange-500"
              />
              Set as default address
            </label>
          </div>
          <div className="mt-5 flex gap-3">
            <button
              type="submit"
              disabled={busy}
              className="rounded-md bg-blue-950 px-5 py-2 text-sm font-semibold text-white transition hover:bg-blue-900 disabled:opacity-60"
            >
              {busy ? "Saving..." : "Save Address"}
            </button>
            <button
              type="button"
              onClick={() => setEditing(null)}
              className="rounded-md border border-slate-300 px-5 py-2 text-sm font-semibold text-slate-600 transition hover:bg-slate-50"
            >
              Cancel
            </button>
          </div>
        </form>
      )}

      {loading ? (
        <div className="mt-6 rounded-lg border border-slate-200 bg-white p-10 text-center text-sm text-slate-400">
          Loading addresses...
        </div>
      ) : addresses.length === 0 && editing === null ? (
        <div className="mt-6 rounded-lg border border-slate-200 bg-white p-10 text-center">
          <p className="text-sm font-semibold text-slate-700">
            No saved addresses
          </p>
          <p className="mt-1 text-sm text-slate-500">
            Save an address for faster checkout next time.
          </p>
        </div>
      ) : (
        <div className="mt-6 grid gap-4 sm:grid-cols-2">
          {addresses.map((address) => (
            <div
              key={address.id}
              className="flex flex-col rounded-lg border border-slate-200 bg-white p-5"
            >
              <div className="flex items-start justify-between gap-2">
                <p className="text-sm font-bold text-slate-800">
                  {address.fullName}
                </p>
                {address.isDefault && (
                  <span className="rounded-full bg-blue-100 px-2 py-0.5 text-xs font-semibold text-blue-700">
                    Default
                  </span>
                )}
              </div>
              <p className="mt-2 text-sm text-slate-600">
                {address.addressLine}
              </p>
              <p className="text-sm text-slate-600">
                {address.city}, {address.region}
              </p>
              <p className="mt-1 text-sm text-slate-400">{address.phone}</p>
              <div className="mt-4 flex gap-3 border-t border-slate-100 pt-3 text-sm">
                <button
                  type="button"
                  onClick={() => openEdit(address)}
                  className="font-semibold text-blue-950 hover:text-orange-600"
                >
                  Edit
                </button>
                <button
                  type="button"
                  onClick={() => onDelete(address.id)}
                  className="font-semibold text-slate-400 hover:text-red-600"
                >
                  Delete
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
