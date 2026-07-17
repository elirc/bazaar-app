import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { getOrder } from '../../api/cart'
import { formatMoney } from '../../lib/format'

export default function OrderConfirmationPage() {
  const { id } = useParams<{ id: string }>()
  const { data: order, isLoading, isError } = useQuery({
    queryKey: ['order', id],
    queryFn: ({ signal }) => getOrder(id!, signal),
    enabled: Boolean(id),
  })

  if (isLoading) return <p>Loading your order…</p>
  if (isError || !order) return <p className="error">We couldn't find that order.</p>

  return (
    <section className="order-confirmation">
      <h1>Thank you!</h1>
      <p data-testid="order-number">
        Your order <strong>{order.number}</strong> is <strong>{order.status}</strong>.
      </p>
      <p className="muted">A confirmation was sent to {order.email}.</p>

      <table>
        <thead>
          <tr>
            <th>Item</th>
            <th>Qty</th>
            <th>Total</th>
          </tr>
        </thead>
        <tbody>
          {order.items.map((line) => (
            <tr key={line.sku}>
              <td>{line.title}</td>
              <td>{line.quantity}</td>
              <td>{formatMoney(line.lineTotal)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="order-totals">
        <div><span>Subtotal</span><span>{formatMoney(order.subtotal)}</span></div>
        {order.discountTotal.amount > 0 && (
          <div><span>Discount</span><span>−{formatMoney(order.discountTotal)}</span></div>
        )}
        <div><span>Tax</span><span>{formatMoney(order.taxTotal)}</span></div>
        <div><span>Shipping</span><span>{formatMoney(order.shippingTotal)}</span></div>
        <div className="order-totals__grand"><span>Total</span><strong>{formatMoney(order.grandTotal)}</strong></div>
      </div>

      <Link to="/" className="button">Continue shopping</Link>
    </section>
  )
}
