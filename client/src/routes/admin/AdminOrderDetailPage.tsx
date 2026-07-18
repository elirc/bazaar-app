import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { createShipment, getAdminOrder, transitionOrder } from '../../api/orders'
import { ApiError } from '../../api/client'
import { formatMoney } from '../../lib/format'
import type { Order } from '../../api/types'

// Fulfillment (PartiallyFulfilled / Fulfilled) is shipment-driven, not a manual transition.
const NEXT_STATUSES: Record<string, string[]> = {
  Pending: ['Paid', 'Cancelled'],
  Paid: ['Refunded', 'Cancelled'],
  PartiallyFulfilled: ['Refunded'],
  Fulfilled: ['Refunded'],
  Cancelled: [],
  Refunded: [],
}

function shippedByLine(order: Order): Record<string, number> {
  const map: Record<string, number> = {}
  for (const shipment of order.shipments) {
    for (const line of shipment.lines) {
      map[line.orderLineItemId] = (map[line.orderLineItemId] ?? 0) + line.quantity
    }
  }
  return map
}

export default function AdminOrderDetailPage() {
  const { id } = useParams<{ id: string }>()
  const queryClient = useQueryClient()
  const [carrier, setCarrier] = useState('')
  const [tracking, setTracking] = useState('')
  const [quantities, setQuantities] = useState<Record<string, number>>({})

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

  const shipment = useMutation({
    mutationFn: () =>
      createShipment(id!, {
        carrier,
        trackingNumber: tracking,
        lines: Object.entries(quantities)
          .filter(([, qty]) => qty > 0)
          .map(([orderLineItemId, quantity]) => ({ orderLineItemId, quantity })),
      }),
    onSuccess: (updated) => {
      queryClient.setQueryData(['admin-order', id], updated)
      queryClient.invalidateQueries({ queryKey: ['admin-orders'] })
      setCarrier('')
      setTracking('')
      setQuantities({})
    },
  })

  if (orderQuery.isLoading) return <p>Loading…</p>
  if (orderQuery.isError || !orderQuery.data) return <p className="error">Could not load order.</p>

  const order = orderQuery.data
  const nextStatuses = NEXT_STATUSES[order.status] ?? []
  const message = transition.error instanceof ApiError ? transition.error.message : null
  const shipmentMessage = shipment.error instanceof ApiError ? shipment.error.message : null
  const shipped = shippedByLine(order)
  const canShip = order.status === 'Paid' || order.status === 'PartiallyFulfilled'
  const anyToShip = Object.values(quantities).some((q) => q > 0)

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

      <h2>Fulfillment</h2>
      {order.shipments.length > 0 && (
        <ul className="shipment-list">
          {order.shipments.map((s) => (
            <li key={s.id} data-testid="shipment">
              <strong>{s.carrier}</strong> — {s.trackingNumber}
              <span className="muted"> · {s.lines.reduce((n, l) => n + l.quantity, 0)} item(s)</span>
            </li>
          ))}
        </ul>
      )}

      {canShip ? (
        <form
          className="admin-form"
          onSubmit={(e) => {
            e.preventDefault()
            shipment.mutate()
          }}
        >
          <table>
            <thead>
              <tr><th>Item</th><th>Ordered</th><th>Shipped</th><th>Ship now</th></tr>
            </thead>
            <tbody>
              {order.items.map((line) => {
                const already = shipped[line.id] ?? 0
                const remaining = line.quantity - already
                return (
                  <tr key={line.id}>
                    <td>{line.title}</td>
                    <td>{line.quantity}</td>
                    <td>{already}</td>
                    <td>
                      <input
                        type="number"
                        min={0}
                        max={remaining}
                        aria-label={`Ship quantity for ${line.sku}`}
                        value={quantities[line.id] ?? 0}
                        disabled={remaining <= 0}
                        onChange={(e) =>
                          setQuantities((q) => ({ ...q, [line.id]: Math.max(0, Math.min(remaining, Number(e.target.value))) }))
                        }
                        style={{ width: 64 }}
                      />
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
          <div className="variant-row">
            <input aria-label="Carrier" placeholder="Carrier" value={carrier} onChange={(e) => setCarrier(e.target.value)} required />
            <input aria-label="Tracking number" placeholder="Tracking number" value={tracking} onChange={(e) => setTracking(e.target.value)} required />
          </div>
          {shipmentMessage && <p className="error">{shipmentMessage}</p>}
          <button type="submit" className="primary" disabled={!anyToShip || shipment.isPending}>
            Create shipment
          </button>
        </form>
      ) : (
        <p className="muted">This order is not in a shippable state.</p>
      )}

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
