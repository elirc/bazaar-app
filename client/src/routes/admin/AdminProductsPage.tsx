import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { deleteAdminProduct, listAdminProducts } from '../../api/catalog'
import { formatMoney } from '../../lib/format'
import Pagination from '../../components/Pagination'

const STATUSES = ['', 'Draft', 'Active', 'Archived']

export default function AdminProductsPage() {
  const queryClient = useQueryClient()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)

  const productsQuery = useQuery({
    queryKey: ['admin-products', { search, status, page }],
    queryFn: ({ signal }) =>
      listAdminProducts({ search: search || undefined, status: status || undefined, page, pageSize: 20 }, signal),
    placeholderData: keepPreviousData,
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteAdminProduct(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin-products'] }),
  })

  function submitSearch(event: FormEvent) {
    event.preventDefault()
    setSearch(searchInput.trim())
    setPage(1)
  }

  const data = productsQuery.data

  return (
    <section>
      <div className="admin-toolbar">
        <h1>Products</h1>
        <Link to="/admin/products/new" className="button primary">+ New product</Link>
      </div>

      <div className="admin-filters">
        <form onSubmit={submitSearch} role="search">
          <input
            type="search"
            aria-label="Search products"
            placeholder="Search by title or slug…"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
          />
          <button type="submit">Search</button>
        </form>
        <label>
          Status
          <select
            value={status}
            onChange={(e) => {
              setStatus(e.target.value)
              setPage(1)
            }}
          >
            {STATUSES.map((s) => (
              <option key={s || 'all'} value={s}>
                {s || 'All'}
              </option>
            ))}
          </select>
        </label>
      </div>

      {productsQuery.isLoading && <p>Loading…</p>}
      {productsQuery.isError && <p className="error">Could not load products.</p>}
      {deleteMutation.isError && <p className="error">Delete failed (the product may be referenced by an order).</p>}

      {data && (
        <>
          <table>
            <thead>
              <tr>
                <th>Title</th>
                <th>Status</th>
                <th>Price from</th>
                <th>Collections</th>
                <th aria-label="Actions" />
              </tr>
            </thead>
            <tbody>
              {data.items.map((product) => (
                <tr key={product.id} data-testid="admin-product-row">
                  <td>
                    <Link to={`/admin/products/${product.id}`}>{product.title}</Link>
                    <div className="muted">{product.slug}</div>
                  </td>
                  <td>{product.status}</td>
                  <td>{formatMoney(product.priceFrom)}</td>
                  <td>{product.collections.join(', ') || '—'}</td>
                  <td className="row-actions">
                    <Link to={`/admin/products/${product.id}`}>Edit</Link>
                    <button
                      type="button"
                      onClick={() => deleteMutation.mutate(product.id)}
                      disabled={deleteMutation.isPending}
                    >
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
              {data.items.length === 0 && (
                <tr>
                  <td colSpan={5}>No products found.</td>
                </tr>
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
