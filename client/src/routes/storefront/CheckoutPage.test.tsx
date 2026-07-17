import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { CartProvider } from '../../cart/CartContext'
import CheckoutPage from './CheckoutPage'
import { jsonResponse } from '../../test/utils'

const cart = {
  id: 'c', token: 'tok-123', itemCount: 2, subtotal: { amount: 28, currency: 'USD' },
  items: [
    {
      variantId: 'v1', productSlug: 'mug', productTitle: 'Stoneware Mug', variantTitle: 'Cream',
      sku: 'MUG-CRM', unitPrice: { amount: 14, currency: 'USD' }, quantity: 2,
      lineTotal: { amount: 28, currency: 'USD' }, available: 80,
    },
  ],
}

function renderCheckout() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <CartProvider>
          <CheckoutPage />
        </CartProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

beforeEach(() => localStorage.setItem('bazaar_cart_token', 'tok-123'))
afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
})

describe('CheckoutPage', () => {
  it('previews an applied discount code', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = String(input)
        if (url.includes('/api/storefront/discounts/')) {
          return Promise.resolve(
            jsonResponse({ code: 'WELCOME10', valid: true, reason: null, discount: { amount: 2.8, currency: 'USD' } }),
          )
        }
        if (url.includes('/api/cart/')) return Promise.resolve(jsonResponse(cart))
        return Promise.resolve(jsonResponse({}, 404))
      }),
    )

    const user = userEvent.setup()
    renderCheckout()

    await waitFor(() => expect(screen.getByText(/order summary/i)).toBeInTheDocument())

    await user.type(screen.getByLabelText(/discount code/i), 'WELCOME10')
    await user.click(screen.getByRole('button', { name: /^apply$/i }))

    await waitFor(() => expect(screen.getByText(/WELCOME10 applied/i)).toBeInTheDocument())
  })
})
