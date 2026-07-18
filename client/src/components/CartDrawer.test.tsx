import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { CartProvider, useCart } from '../cart/CartContext'
import CartDrawer from './CartDrawer'
import { jsonResponse } from '../test/utils'

const cartPayload = {
  id: 'cart1',
  token: 'tok-123',
  itemCount: 1,
  subtotal: { amount: 14, currency: 'USD' },
  items: [
    {
      variantId: 'v1', productSlug: 'mug', productTitle: 'Stoneware Mug', variantTitle: 'Cream',
      sku: 'MUG-CRM', unitPrice: { amount: 14, currency: 'USD' }, quantity: 1,
      lineTotal: { amount: 14, currency: 'USD' }, available: 80,
    },
  ],
}

function Harness() {
  const { open } = useCart()
  return (
    <>
      <button type="button" onClick={open}>Open cart</button>
      <CartDrawer />
    </>
  )
}

function renderCart() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <CartProvider>
          <Harness />
        </CartProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  localStorage.setItem('bazaar_cart_token', 'tok-123')
})

afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
})

describe('CartDrawer', () => {
  it('renders cart lines and increments quantity via the API', async () => {
    const puts: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
        if (init?.method === 'PUT') {
          puts.push(url)
          return Promise.resolve(
            jsonResponse({ ...cartPayload, itemCount: 2, items: [{ ...cartPayload.items[0], quantity: 2 }] }),
          )
        }
        return Promise.resolve(jsonResponse(cartPayload))
      }),
    )

    const user = userEvent.setup()
    renderCart()

    await user.click(screen.getByRole('button', { name: /open cart/i }))
    await waitFor(() => expect(screen.getByTestId('cart-line')).toBeInTheDocument())
    expect(screen.getByText('Stoneware Mug')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /increase MUG-CRM/i }))
    await waitFor(() => expect(puts).toHaveLength(1))
    expect(puts[0]).toContain('/api/cart/tok-123/items/v1')
  })

  it('saves an active line for later via the API', async () => {
    const savedCalls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
        if (url.includes('/saved') && init?.method === 'POST') {
          savedCalls.push(url)
          return Promise.resolve(jsonResponse({ ...cartPayload, itemCount: 0, savedCount: 1, subtotal: { amount: 0, currency: 'USD' }, items: [{ ...cartPayload.items[0], savedForLater: true }] }))
        }
        return Promise.resolve(jsonResponse(cartPayload))
      }),
    )

    const user = userEvent.setup()
    renderCart()

    await user.click(screen.getByRole('button', { name: /open cart/i }))
    await waitFor(() => expect(screen.getByTestId('cart-line')).toBeInTheDocument())

    await user.click(screen.getByRole('button', { name: /save for later/i }))
    await waitFor(() => expect(savedCalls).toHaveLength(1))
    expect(savedCalls[0]).toContain('/api/cart/tok-123/items/v1/saved')
    await waitFor(() => expect(screen.getByTestId('saved-line')).toBeInTheDocument())
  })
})
