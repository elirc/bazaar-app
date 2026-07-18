import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { createReturn } from '../api/returns'
import { ApiError } from '../api/client'
import type { Order } from '../api/types'

export default function ReturnRequestForm({ order }: { order: Order }) {
  // Per-line quantity to return, keyed by order line id.
  const [quantities, setQuantities] = useState<Record<string, number>>({})
  const [reason, setReason] = useState('')
  const [done, setDone] = useState(false)

  const mutation = useMutation({
    mutationFn: () =>
      createReturn(order.id, {
        reason: reason || undefined,
        lines: Object.entries(quantities)
          .filter(([, qty]) => qty > 0)
          .map(([orderLineItemId, quantity]) => ({ orderLineItemId, quantity })),
      }),
    onSuccess: () => setDone(true),
  })

  const anySelected = Object.values(quantities).some((q) => q > 0)
  const error = mutation.error instanceof ApiError ? mutation.error.message : null

  if (done) {
    return (
      <section className="returns">
        <h2>Return requested</h2>
        <p className="success" role="status">Your return request was submitted and is awaiting review.</p>
      </section>
    )
  }

  return (
    <section className="returns">
      <h2>Request a return</h2>
      <table>
        <thead>
          <tr><th>Item</th><th>Ordered</th><th>Return qty</th></tr>
        </thead>
        <tbody>
          {order.items.map((line) => (
            <tr key={line.id}>
              <td>{line.title}</td>
              <td>{line.quantity}</td>
              <td>
                <input
                  type="number"
                  min={0}
                  max={line.quantity}
                  aria-label={`Return quantity for ${line.sku}`}
                  value={quantities[line.id] ?? 0}
                  onChange={(e) =>
                    setQuantities((q) => ({ ...q, [line.id]: Math.max(0, Math.min(line.quantity, Number(e.target.value))) }))
                  }
                  style={{ width: 64 }}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <label className="returns__reason">
        Reason (optional)
        <input value={reason} onChange={(e) => setReason(e.target.value)} />
      </label>

      {error && <p className="error" role="alert">{error}</p>}
      <button type="button" className="primary" disabled={!anySelected || mutation.isPending} onClick={() => mutation.mutate()}>
        {mutation.isPending ? 'Submitting…' : 'Submit return'}
      </button>
    </section>
  )
}
