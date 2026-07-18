import { describe, it, expect, vi, afterEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AuthProvider } from '../../auth/AuthContext'
import { CartProvider } from '../../cart/CartContext'
import WishlistPage from './WishlistPage'
import { jsonResponse } from '../../test/utils'
import { setAuthToken } from '../../api/client'

const wishlists = [
  {
    id: 'w1', name: 'My Wishlist', isDefault: true,
    items: [
      {
        variantId: 'v1', productSlug: 'ceramic-mug', productTitle: 'Stoneware Mug', variantTitle: 'Cream',
        sku: 'MUG-CRM', price: { amount: 14, currency: 'USD' }, available: 5, backInStock: true,
        addedAt: new Date().toISOString(),
      },
    ],
  },
]

const customer = { id: 'u1', email: 'sam@example.com', firstName: null, lastName: null, role: 'Customer' }

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/wishlist']}>
        <AuthProvider>
          <CartProvider>
            <WishlistPage />
          </CartProvider>
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
  setAuthToken(null)
})

describe('WishlistPage', () => {
  it('shows wishlist items with a back-in-stock badge and moves one to the cart', async () => {
    localStorage.setItem('bazaar_auth_token', 'token')
    const moved: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = String(input)
        if (url.includes('/api/auth/me')) return Promise.resolve(jsonResponse(customer))
        if (url.includes('/move-to-cart')) {
          moved.push(url)
          return Promise.resolve(jsonResponse({ id: 'c1', token: 'tok', items: [], subtotal: { amount: 0, currency: 'USD' }, itemCount: 0, savedCount: 0 }))
        }
        if (url.includes('/api/account/wishlists')) return Promise.resolve(jsonResponse(wishlists))
        return Promise.resolve(jsonResponse({}, 404))
      }),
    )

    const user = userEvent.setup()
    renderPage()

    await waitFor(() => expect(screen.getByText('Stoneware Mug')).toBeInTheDocument())
    expect(screen.getByText('Back in stock!')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /move to cart/i }))
    await waitFor(() => expect(moved).toHaveLength(1))
    expect(moved[0]).toContain('/api/account/wishlists/w1/items/v1/move-to-cart')
  })
})
