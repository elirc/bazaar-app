import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { createAdminDiscount, deleteAdminDiscount, listAdminDiscounts } from '../../api/orders'
import { ApiError } from '../../api/client'

export default function AdminDiscountsPage() {
  const queryClient = useQueryClient()
  const [code, setCode] = useState('')
  const [type, setType] = useState('Percentage')
  const [value, setValue] = useState('')

  const discountsQuery = useQuery({
    queryKey: ['admin-discounts'],
    queryFn: ({ signal }) => listAdminDiscounts(signal),
  })

  const createMutation = useMutation({
    mutationFn: () =>
      createAdminDiscount({ code, type, value: Number(value), isActive: true }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-discounts'] })
      setCode('')
      setValue('')
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteAdminDiscount(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin-discounts'] }),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    createMutation.mutate()
  }

  const message = createMutation.error instanceof ApiError ? createMutation.error.message : null

  return (
    <section>
      <h1>Discount codes</h1>

      <form className="admin-form inline" onSubmit={submit}>
        <input
          aria-label="Discount code"
          placeholder="CODE"
          value={code}
          onChange={(e) => setCode(e.target.value.toUpperCase())}
          required
        />
        <select aria-label="Discount type" value={type} onChange={(e) => setType(e.target.value)}>
          <option value="Percentage">Percentage</option>
          <option value="FixedAmount">Fixed amount</option>
        </select>
        <input
          aria-label="Discount value"
          type="number"
          step="0.01"
          min="0"
          placeholder={type === 'Percentage' ? '% off' : '$ off'}
          value={value}
          onChange={(e) => setValue(e.target.value)}
          required
        />
        <button type="submit" className="primary" disabled={createMutation.isPending}>Add</button>
      </form>
      {message && <p className="error">{message}</p>}

      {discountsQuery.isLoading && <p>Loading…</p>}
      {discountsQuery.data && (
        <table>
          <thead>
            <tr><th>Code</th><th>Type</th><th>Value</th><th>Used</th><th>Active</th><th aria-label="Actions" /></tr>
          </thead>
          <tbody>
            {discountsQuery.data.map((d) => (
              <tr key={d.id} data-testid="discount-row">
                <td>{d.code}</td>
                <td>{d.type}</td>
                <td>{d.type === 'Percentage' ? `${d.value}%` : `${d.value} ${d.currency}`}</td>
                <td>{d.timesUsed}{d.usageLimit != null ? ` / ${d.usageLimit}` : ''}</td>
                <td>{d.isActive ? 'Yes' : 'No'}</td>
                <td>
                  <button type="button" onClick={() => deleteMutation.mutate(d.id)} disabled={deleteMutation.isPending}>
                    Delete
                  </button>
                </td>
              </tr>
            ))}
            {discountsQuery.data.length === 0 && (
              <tr><td colSpan={6}>No discount codes.</td></tr>
            )}
          </tbody>
        </table>
      )}
    </section>
  )
}
