import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { approveReturn, listAdminReturns, rejectReturn } from '../../api/returns'
import { formatMoney } from '../../lib/format'

const STATUSES = ['Requested', 'Approved', 'Rejected']

export default function AdminReturnsPage() {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState('Requested')

  const returnsQuery = useQuery({
    queryKey: ['admin-returns', status],
    queryFn: ({ signal }) => listAdminReturns({ status }, signal),
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['admin-returns'] })

  const approveMutation = useMutation({ mutationFn: (id: string) => approveReturn(id), onSuccess: invalidate })
  const rejectMutation = useMutation({ mutationFn: (id: string) => rejectReturn(id), onSuccess: invalidate })

  return (
    <section>
      <h1>Returns queue</h1>

      <div className="admin-filters">
        <label>
          Status
          <select value={status} onChange={(e) => setStatus(e.target.value)}>
            {STATUSES.map((s) => (
              <option key={s} value={s}>{s}</option>
            ))}
          </select>
        </label>
      </div>

      {returnsQuery.isLoading && <p>Loading…</p>}
      {returnsQuery.data && (
        <table>
          <thead>
            <tr><th>Order</th><th>Customer</th><th>Items</th><th>Refund</th><th>Reason</th><th aria-label="Actions" /></tr>
          </thead>
          <tbody>
            {returnsQuery.data.items.map((rma) => (
              <tr key={rma.id} data-testid="admin-return-row">
                <td>{rma.orderNumber}</td>
                <td>{rma.email}</td>
                <td>
                  {rma.lines.map((l) => (
                    <div key={l.orderLineItemId} className="muted">{l.title} × {l.quantity}</div>
                  ))}
                </td>
                <td>{rma.status === 'Approved' ? formatMoney(rma.refundAmount) : '—'}</td>
                <td>{rma.reason ?? '—'}</td>
                <td className="row-actions">
                  {rma.status === 'Requested' && (
                    <>
                      <button type="button" onClick={() => approveMutation.mutate(rma.id)} disabled={approveMutation.isPending}>
                        Approve &amp; refund
                      </button>
                      <button type="button" onClick={() => rejectMutation.mutate(rma.id)} disabled={rejectMutation.isPending}>
                        Reject
                      </button>
                    </>
                  )}
                </td>
              </tr>
            ))}
            {returnsQuery.data.items.length === 0 && (
              <tr><td colSpan={6}>No returns in this state.</td></tr>
            )}
          </tbody>
        </table>
      )}
    </section>
  )
}
