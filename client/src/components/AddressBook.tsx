import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { createAddress, deleteAddress, listAddresses, updateAddress } from '../api/auth'
import type { CustomerAddress } from '../api/types'

const empty = {
  label: '',
  name: '',
  line1: '',
  line2: '',
  city: '',
  region: '',
  postalCode: '',
  country: 'US',
  isDefault: false,
}

export default function AddressBook() {
  const queryClient = useQueryClient()
  const [form, setForm] = useState(empty)

  const addressesQuery = useQuery({
    queryKey: ['account-addresses'],
    queryFn: ({ signal }) => listAddresses(signal),
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['account-addresses'] })

  const createMutation = useMutation({
    mutationFn: () =>
      createAddress({
        label: form.label || undefined,
        isDefault: form.isDefault,
        address: {
          name: form.name,
          line1: form.line1,
          line2: form.line2 || undefined,
          city: form.city,
          region: form.region || undefined,
          postalCode: form.postalCode,
          country: form.country,
        },
      }),
    onSuccess: () => {
      invalidate()
      setForm(empty)
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteAddress(id),
    onSuccess: invalidate,
  })

  const defaultMutation = useMutation({
    mutationFn: (addr: CustomerAddress) =>
      updateAddress(addr.id, {
        label: addr.label ?? undefined,
        isDefault: true,
        address: {
          name: addr.address.name,
          line1: addr.address.line1,
          line2: addr.address.line2 ?? undefined,
          city: addr.address.city,
          region: addr.address.region ?? undefined,
          postalCode: addr.address.postalCode,
          country: addr.address.country,
        },
      }),
    onSuccess: invalidate,
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    createMutation.mutate()
  }

  const set = (patch: Partial<typeof empty>) => setForm((f) => ({ ...f, ...patch }))

  return (
    <section className="address-book">
      <h2>Address book</h2>

      {addressesQuery.data && addressesQuery.data.length === 0 && <p>No saved addresses yet.</p>}
      {addressesQuery.data && addressesQuery.data.length > 0 && (
        <ul className="address-list">
          {addressesQuery.data.map((a) => (
            <li key={a.id} className="address-card" data-testid="address-card">
              <div>
                <strong>{a.label ?? 'Address'}</strong>
                {a.isDefault && <span className="badge"> Default</span>}
                <address className="order-address">
                  {a.address.name}<br />
                  {a.address.line1}{a.address.line2 ? `, ${a.address.line2}` : ''}<br />
                  {a.address.city}{a.address.region ? `, ${a.address.region}` : ''} {a.address.postalCode}<br />
                  {a.address.country}
                </address>
              </div>
              <div className="row-actions">
                {!a.isDefault && (
                  <button type="button" onClick={() => defaultMutation.mutate(a)}>Make default</button>
                )}
                <button type="button" onClick={() => deleteMutation.mutate(a.id)}>Delete</button>
              </div>
            </li>
          ))}
        </ul>
      )}

      <form className="admin-form" onSubmit={submit}>
        <h3>Add an address</h3>
        <label>Label<input value={form.label} onChange={(e) => set({ label: e.target.value })} placeholder="Home" /></label>
        <label>Full name<input value={form.name} onChange={(e) => set({ name: e.target.value })} required /></label>
        <label>Address line 1<input value={form.line1} onChange={(e) => set({ line1: e.target.value })} required /></label>
        <label>Address line 2<input value={form.line2} onChange={(e) => set({ line2: e.target.value })} /></label>
        <label>City<input value={form.city} onChange={(e) => set({ city: e.target.value })} required /></label>
        <label>Region / State<input value={form.region} onChange={(e) => set({ region: e.target.value })} /></label>
        <label>Postal code<input value={form.postalCode} onChange={(e) => set({ postalCode: e.target.value })} required /></label>
        <label>Country (2-letter)
          <input value={form.country} onChange={(e) => set({ country: e.target.value.toUpperCase() })} maxLength={2} required />
        </label>
        <label className="checkbox">
          <input type="checkbox" checked={form.isDefault} onChange={(e) => set({ isDefault: e.target.checked })} />
          Set as default
        </label>
        <button type="submit" className="primary" disabled={createMutation.isPending}>Save address</button>
      </form>
    </section>
  )
}
