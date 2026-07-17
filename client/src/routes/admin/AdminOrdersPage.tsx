import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { listAdminOrders } from '../../api/orders'
import { formatMoney } from '../../lib/format'
import Pagination from '../../components/Pagination'

const STATUSES = ['', 'Pending', 'Paid', 'Fulfilled', 'Cancelled', 'Refunded']

export default function AdminOrdersPage() {
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)

  const ordersQuery = useQuery({
    queryKey: ['admin-orders', { search, status, page }],
    queryFn: ({ signal }) =>
      listAdminOrders({ search: search || undefined, status: status || undefined, page, pageSize: 20 }, signal),
    placeholderData: keepPreviousData,
  })

  function submitSearch(event: FormEvent) {
    event.preventDefault()
    setSearch(searchInput.trim())
    setPage(1)
  }

  const data = ordersQuery.data

  return (
    <section>
      <h1>Orders</h1>
      <div className="admin-filters">
        <form onSubmit={submitSearch} role="search">
          <input
            type="search"
            aria-label="Search orders"
            placeholder="Search by number or email…"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
          />
          <button type="submit">Search</button>
        </form>
        <label>
          Status
          <select value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
            {STATUSES.map((s) => (
              <option key={s || 'all'} value={s}>{s || 'All'}</option>
            ))}
          </select>
        </label>
      </div>

      {ordersQuery.isLoading && <p>Loading…</p>}
      {ordersQuery.isError && <p className="error">Could not load orders.</p>}

      {data && (
        <>
          <table>
            <thead>
              <tr>
                <th>Order</th>
                <th>Email</th>
                <th>Status</th>
                <th>Items</th>
                <th>Total</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((order) => (
                <tr key={order.id} data-testid="admin-order-row">
                  <td><Link to={`/admin/orders/${order.id}`}>{order.number}</Link></td>
                  <td>{order.email}</td>
                  <td>{order.status}</td>
                  <td>{order.itemCount}</td>
                  <td>{formatMoney(order.grandTotal)}</td>
                </tr>
              ))}
              {data.items.length === 0 && (
                <tr><td colSpan={5}>No orders yet.</td></tr>
              )}
            </tbody>
          </table>
          <Pagination
            page={data.page}
            totalPages={data.totalPages}
            hasPrevious={data.hasPrevious}
            hasNext={data.hasNext}
            onChange={setPage}
          />
        </>
      )}
    </section>
  )
}
