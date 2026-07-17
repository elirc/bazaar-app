import { describe, it, expect, vi, afterEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders, jsonResponse } from '../../test/utils'
import ProductListPage from './ProductListPage'

const productsPayload = {
  items: [
    {
      id: '1', slug: 'classic-tee', title: 'Classic Tee', vendor: 'Bazaar', status: 'Active',
      imageUrl: null, priceFrom: { amount: 19.99, currency: 'USD' }, collections: ['apparel'],
    },
    {
      id: '2', slug: 'mug', title: 'Stoneware Mug', vendor: null, status: 'Active',
      imageUrl: null, priceFrom: { amount: 14.0, currency: 'USD' }, collections: ['home'],
    },
  ],
  page: 1, pageSize: 12, totalCount: 2, totalPages: 1, hasPrevious: false, hasNext: false,
}

const collectionsPayload = [
  { id: 'c1', slug: 'apparel', title: 'Apparel', description: null, productCount: 1 },
]

function stubCatalog(onProducts?: (url: string) => void) {
  const fetchMock = vi.fn((input: RequestInfo | URL) => {
    const url = String(input)
    if (url.includes('/api/storefront/collections')) return Promise.resolve(jsonResponse(collectionsPayload))
    if (url.includes('/api/storefront/products')) {
      onProducts?.(url)
      return Promise.resolve(jsonResponse(productsPayload))
    }
    return Promise.resolve(jsonResponse({}, 404))
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

afterEach(() => vi.unstubAllGlobals())

describe('ProductListPage', () => {
  it('renders the product grid from the API', async () => {
    stubCatalog()
    renderWithProviders(<ProductListPage />)

    await waitFor(() => expect(screen.getByText('Classic Tee')).toBeInTheDocument())
    expect(screen.getAllByTestId('product-card')).toHaveLength(2)
    expect(screen.getByText('$19.99')).toBeInTheDocument()
    expect(screen.getByText('2 product(s)')).toBeInTheDocument()
  })

  it('sends the search term to the API on submit', async () => {
    const urls: string[] = []
    stubCatalog((url) => urls.push(url))
    const user = userEvent.setup()
    renderWithProviders(<ProductListPage />)

    await waitFor(() => expect(screen.getByText('Classic Tee')).toBeInTheDocument())

    await user.type(screen.getByRole('searchbox', { name: /search products/i }), 'hoodie')
    await user.click(screen.getByRole('button', { name: /^search$/i }))

    await waitFor(() => expect(urls.some((u) => u.includes('search=hoodie'))).toBe(true))
  })
})
