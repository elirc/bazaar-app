import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getAdminOrder, transitionOrder } from '../../api/orders'
import { ApiError } from '../../api/client'
import { formatMoney } from '../../lib/format'

const NEXT_STATUSES: Record<string, string[]> = {
  Pending: ['Paid', 'Cancelled'],
  Paid: ['Fulfilled', 'Refunded', 'Cancelled'],
  Fulfilled: ['Refunded'],
  Cancelled: [],
  Refunded: [],
}

export default function AdminOrderDetailPage() {
  const { id } = useParams<{ id: string }>()
  const queryClient = useQueryClient()

  const orderQuery = useQuery({
    queryKey: ['admin-order', id],
    queryFn: ({ signal }) => getAdminOrder(id!, signal),
    enabled: Boolean(id),
  })

  const transition = useMutation({
    mutationFn: (status: string) => transitionOrder(id!, status),
    onSuccess: (updated) => {
      queryClient.setQueryData(['admin-order', id], updated)
      queryClient.invalidateQueries({ queryKey: ['admin-orders'] })
    },
  })

  if (orderQuery.isLoading) return <p>Loading…</p>
  if (orderQuery.isError || !orderQuery.data) return <p className="error">Could not load order.</p>

  const order = orderQuery.data
  const nextStatuses = NEXT_STATUSES[order.status] ?? []
  const message = transition.error instanceof ApiError ? transition.error.message : null

  return (
    <section className="order-detail">
      <Link to="/admin/orders">← Orders</Link>
      <div className="admin-toolbar">
        <h1>{order.number}</h1>
        <span className="order-status" data-testid="order-status">{order.status}</span>
      </div>

      <p className="muted">{order.email}</p>

      {message && <p className="error">{message}</p>}

      {nextStatuses.length > 0 && (
        <div className="order-actions">
          {nextStatuses.map((status) => (
            <button
              key={status}
              type="button"
              onClick={() => transition.mutate(status)}
              disabled={transition.isPending}
            >
              Mark {status}
            </button>
          ))}
        </div>
      )}

      <h2>Items</h2>
      <table>
        <thead>
          <tr><th>Item</th><th>SKU</th><th>Qty</th><th>Unit</th><th>Total</th></tr>
        </thead>
        <tbody>
          {order.items.map((line) => (
            <tr key={line.sku}>
              <td>{line.title}</td>
              <td>{line.sku}</td>
              <td>{line.quantity}</td>
              <td>{formatMoney(line.unitPrice)}</td>
              <td>{formatMoney(line.lineTotal)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="order-totals">
        <div><span>Subtotal</span><span>{formatMoney(order.subtotal)}</span></div>
        {order.discountTotal.amount > 0 && (
          <div><span>Discount {order.discountCode ? `(${order.discountCode})` : ''}</span><span>−{formatMoney(order.discountTotal)}</span></div>
        )}
        <div><span>Tax</span><span>{formatMoney(order.taxTotal)}</span></div>
        <div><span>Shipping</span><span>{formatMoney(order.shippingTotal)}</span></div>
        <div className="order-totals__grand"><span>Total</span><strong>{formatMoney(order.grandTotal)}</strong></div>
      </div>

      <h2>Shipping to</h2>
      <address className="order-address">
        {order.shippingAddress.name}<br />
        {order.shippingAddress.line1}<br />
        {order.shippingAddress.line2 && <>{order.shippingAddress.line2}<br /></>}
        {order.shippingAddress.city}
        {order.shippingAddress.region ? `, ${order.shippingAddress.region}` : ''} {order.shippingAddress.postalCode}<br />
        {order.shippingAddress.country}
      </address>
    </section>
  )
}
