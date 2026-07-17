import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { CartProvider } from '../../cart/CartContext'
import ProductDetailPage from './ProductDetailPage'
import { jsonResponse } from '../../test/utils'

const product = {
  id: 'p1', slug: 'ceramic-mug', title: 'Stoneware Mug', description: 'A nice mug.', vendor: 'Kiln & Co',
  status: 'Active', images: [], collections: ['home'],
  variants: [
    { id: 'v1', sku: 'MUG-CRM', title: 'Cream', price: { amount: 14, currency: 'USD' }, position: 0, options: [{ name: 'Color', value: 'Cream' }], available: 80 },
  ],
}

function renderDetail() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/products/ceramic-mug']}>
        <CartProvider>
          <Routes>
            <Route path="/products/:slug" element={<ProductDetailPage />} />
          </Routes>
        </CartProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

beforeEach(() => localStorage.clear())
afterEach(() => vi.unstubAllGlobals())

describe('ProductDetailPage', () => {
  it('adds the selected variant to the cart', async () => {
    const posts: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
        if (init?.method === 'POST' && url.endsWith('/api/cart')) {
          return Promise.resolve(jsonResponse({ id: 'c1', token: 'newtok', items: [], subtotal: { amount: 0, currency: 'USD' }, itemCount: 0 }))
        }
        if (init?.method === 'POST' && url.includes('/api/cart/newtok/items')) {
          posts.push(url)
          return Promise.resolve(jsonResponse({ id: 'c1', token: 'newtok', itemCount: 1, subtotal: { amount: 14, currency: 'USD' }, items: [] }))
        }
        if (url.includes('/api/storefront/products/')) return Promise.resolve(jsonResponse(product))
        return Promise.resolve(jsonResponse({}, 404))
      }),
    )

    const user = userEvent.setup()
    renderDetail()

    await waitFor(() => expect(screen.getByRole('heading', { name: /stoneware mug/i })).toBeInTheDocument())
    expect(screen.getByTestId('availability')).toHaveTextContent(/80 in stock/i)

    await user.click(screen.getByRole('button', { name: /add to cart/i }))

    await waitFor(() => expect(posts).toHaveLength(1))
    expect(posts[0]).toContain('/api/cart/newtok/items')
  })
})
