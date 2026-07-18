import { Link, Navigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { listAccountOrders } from '../../api/auth'
import { listAccountReturns } from '../../api/returns'
import { useAuth } from '../../auth/AuthContext'
import { formatMoney } from '../../lib/format'
import AddressBook from '../../components/AddressBook'

export default function AccountOrdersPage() {
  const { isAuthenticated, isLoading, user } = useAuth()

  const ordersQuery = useQuery({
    queryKey: ['account-orders'],
    queryFn: ({ signal }) => listAccountOrders(signal),
    enabled: isAuthenticated,
  })

  const returnsQuery = useQuery({
    queryKey: ['account-returns'],
    queryFn: ({ signal }) => listAccountReturns(signal),
    enabled: isAuthenticated,
  })

  if (isLoading) return <p>Loading…</p>
  if (!isAuthenticated) return <Navigate to="/login" replace state={{ from: '/account' }} />

  return (
    <section className="account">
      <h1>Your account</h1>
      <p className="muted">Signed in as {user?.email}</p>

      <h2>Order history</h2>
      {ordersQuery.isLoading && <p>Loading orders…</p>}
      {ordersQuery.data && ordersQuery.data.length === 0 && <p>You have no orders yet.</p>}
      {ordersQuery.data && ordersQuery.data.length > 0 && (
        <table>
          <thead>
            <tr><th>Order</th><th>Placed</th><th>Status</th><th>Items</th><th>Total</th></tr>
          </thead>
          <tbody>
            {ordersQuery.data.map((order) => (
              <tr key={order.id} data-testid="account-order-row">
                <td><Link to={`/order/${order.id}`}>{order.number}</Link></td>
                <td>{new Date(order.placedAt).toLocaleDateString()}</td>
                <td>{order.status}</td>
                <td>{order.itemCount}</td>
                <td>{formatMoney(order.grandTotal)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {returnsQuery.data && returnsQuery.data.length > 0 && (
        <>
          <h2>Returns</h2>
          <table>
            <thead>
              <tr><th>Order</th><th>Status</th><th>Refund</th><th>Requested</th></tr>
            </thead>
            <tbody>
              {returnsQuery.data.map((rma) => (
                <tr key={rma.id} data-testid="account-return-row">
                  <td>{rma.orderNumber}</td>
                  <td>{rma.status}</td>
                  <td>{rma.status === 'Approved' ? formatMoney(rma.refundAmount) : '—'}</td>
                  <td>{new Date(rma.createdAt).toLocaleDateString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}

      <AddressBook />
    </section>
  )
}
