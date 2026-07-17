import { useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { listStorefrontCollections, listStorefrontProducts } from '../../api/catalog'
import ProductCard from '../../components/ProductCard'
import Pagination from '../../components/Pagination'

const SORTS = [
  { value: 'newest', label: 'Newest' },
  { value: 'price_asc', label: 'Price: Low to High' },
  { value: 'price_desc', label: 'Price: High to Low' },
  { value: 'title_asc', label: 'Name: A–Z' },
  { value: 'title_desc', label: 'Name: Z–A' },
]

export default function ProductListPage() {
  const [params, setParams] = useSearchParams()
  const search = params.get('search') ?? ''
  const collection = params.get('collection') ?? ''
  const sort = params.get('sort') ?? 'newest'
  const page = Math.max(1, Number(params.get('page') ?? '1') || 1)

  const [searchInput, setSearchInput] = useState(search)

  const collectionsQuery = useQuery({
    queryKey: ['storefront-collections'],
    queryFn: ({ signal }) => listStorefrontCollections(signal),
  })

  const productsQuery = useQuery({
    queryKey: ['storefront-products', { search, collection, sort, page }],
    queryFn: ({ signal }) =>
      listStorefrontProducts(
        { search: search || undefined, collection: collection || undefined, sort, page, pageSize: 12 },
        signal,
      ),
    placeholderData: keepPreviousData,
  })

  function updateParams(mutate: (next: URLSearchParams) => void) {
    setParams((prev) => {
      const next = new URLSearchParams(prev)
      mutate(next)
      return next
    })
  }

  function submitSearch(event: FormEvent) {
    event.preventDefault()
    updateParams((next) => {
      if (searchInput.trim()) next.set('search', searchInput.trim())
      else next.delete('search')
      next.delete('page')
    })
  }

  const data = productsQuery.data

  return (
    <section className="catalog">
      <div className="catalog__header">
        <h1>Shop</h1>
        <form className="catalog__search" onSubmit={submitSearch} role="search">
          <input
            type="search"
            aria-label="Search products"
            placeholder="Search products…"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
          />
          <button type="submit" className="primary">Search</button>
        </form>
      </div>

      <div className="catalog__filters">
        <label>
          Collection
          <select
            value={collection}
            onChange={(e) =>
              updateParams((next) => {
                if (e.target.value) next.set('collection', e.target.value)
                else next.delete('collection')
                next.delete('page')
              })
            }
          >
            <option value="">All collections</option>
            {collectionsQuery.data?.map((c) => (
              <option key={c.id} value={c.slug}>
                {c.title} ({c.productCount})
              </option>
            ))}
          </select>
        </label>

        <label>
          Sort
          <select
            value={sort}
            onChange={(e) =>
              updateParams((next) => {
                next.set('sort', e.target.value)
                next.delete('page')
              })
            }
          >
            {SORTS.map((s) => (
              <option key={s.value} value={s.value}>
                {s.label}
              </option>
            ))}
          </select>
        </label>
      </div>

      {productsQuery.isLoading && <p>Loading products…</p>}
      {productsQuery.isError && <p className="error">Could not load products.</p>}

      {data && data.items.length === 0 && <p>No products match your search.</p>}

      {data && data.items.length > 0 && (
        <>
          <p className="catalog__count">{data.totalCount} product(s)</p>
          <div className="product-grid" data-testid="product-grid">
            {data.items.map((product) => (
              <ProductCard key={product.id} product={product} />
            ))}
          </div>
          <Pagination
            page={data.page}
            totalPages={data.totalPages}
            hasPrevious={data.hasPrevious}
            hasNext={data.hasNext}
            onChange={(n) => updateParams((next) => next.set('page', String(n)))}
          />
        </>
      )}
    </section>
  )
}
