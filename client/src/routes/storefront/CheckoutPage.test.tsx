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
    // The first "Apply" button is the discount one (gift-card entry adds a second).
    await user.click(screen.getAllByRole('button', { name: /^apply$/i })[0])

    await waitFor(() => expect(screen.getByText(/WELCOME10 applied/i)).toBeInTheDocument())
  })

  it('lists shipping options and submits the selected method', async () => {
    const shippingOptions = [
      { code: 'standard', name: 'Standard', rateType: 'FreeOverThreshold', cost: { amount: 5.99, currency: 'USD' }, deliveryEstimate: '3–5 business days', minDays: 3, maxDays: 5 },
      { code: 'express', name: 'Express', rateType: 'Flat', cost: { amount: 14.99, currency: 'USD' }, deliveryEstimate: '1–2 business days', minDays: 1, maxDays: 2 },
    ]
    let checkoutBody: Record<string, unknown> | null = null
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
        if (url.includes('/api/checkout/shipping-options')) return Promise.resolve(jsonResponse(shippingOptions))
        if (url.includes('/api/checkout')) {
          checkoutBody = JSON.parse(String(init?.body))
          return Promise.resolve(jsonResponse({ id: 'order-1' }, 201))
        }
        if (url.includes('/api/cart/')) return Promise.resolve(jsonResponse(cart))
        return Promise.resolve(jsonResponse({}, 404))
      }),
    )

    const user = userEvent.setup()
    renderCheckout()

    await waitFor(() => expect(screen.getByText('Express')).toBeInTheDocument())
    await user.click(screen.getByRole('radio', { name: /express/i }))

    await user.type(screen.getByLabelText('Email'), 'buyer@example.com')
    await user.type(screen.getByLabelText(/full name/i), 'Ada')
    await user.type(screen.getByLabelText(/address line 1/i), '1 Main St')
    await user.type(screen.getByLabelText('City'), 'Denver')
    await user.type(screen.getByLabelText(/postal code/i), '80202')
    await user.click(screen.getByRole('button', { name: /pay/i }))

    await waitFor(() => expect(checkoutBody).not.toBeNull())
    expect(checkoutBody!.shippingMethodCode).toBe('express')
  })

  it('applies a discount and a gift card together and submits both codes', async () => {
    const shippingOptions = [
      { code: 'standard', name: 'Standard', rateType: 'FreeOverThreshold', cost: { amount: 5.99, currency: 'USD' }, deliveryEstimate: '3–5 business days', minDays: 3, maxDays: 5 },
    ]
    let checkoutBody: Record<string, unknown> | null = null
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
        if (url.includes('/api/storefront/discounts/')) {
          return Promise.resolve(jsonResponse({ code: 'WELCOME10', valid: true, reason: null, discount: { amount: 2.8, currency: 'USD' } }))
        }
        if (url.includes('/api/storefront/gift-cards/')) {
          return Promise.resolve(jsonResponse({ code: 'GIFT25', valid: true, balance: { amount: 25, currency: 'USD' } }))
        }
        if (url.includes('/api/checkout/shipping-options')) return Promise.resolve(jsonResponse(shippingOptions))
        if (url.includes('/api/checkout')) {
          checkoutBody = JSON.parse(String(init?.body))
          return Promise.resolve(jsonResponse({ id: 'order-1' }, 201))
        }
        if (url.includes('/api/cart/')) return Promise.resolve(jsonResponse(cart))
        return Promise.resolve(jsonResponse({}, 404))
      }),
    )

    const user = userEvent.setup()
    renderCheckout()

    await waitFor(() => expect(screen.getByText(/order summary/i)).toBeInTheDocument())

    // Apply a percentage discount…
    await user.type(screen.getByLabelText(/discount code/i), 'WELCOME10')
    await user.click(screen.getAllByRole('button', { name: /^apply$/i })[0])
    await waitFor(() => expect(screen.getByText(/WELCOME10 applied/i)).toBeInTheDocument())

    // …and a gift card (the second "Apply" button).
    await user.type(screen.getByLabelText(/gift card/i), 'GIFT25')
    await user.click(screen.getAllByRole('button', { name: /^apply$/i })[1])
    await waitFor(() => expect(screen.getByText(/GIFT25 — .* available/i)).toBeInTheDocument())

    await user.type(screen.getByLabelText('Email'), 'buyer@example.com')
    await user.type(screen.getByLabelText(/full name/i), 'Ada')
    await user.type(screen.getByLabelText(/address line 1/i), '1 Main St')
    await user.type(screen.getByLabelText('City'), 'Denver')
    await user.type(screen.getByLabelText(/postal code/i), '80202')
    await user.click(screen.getByRole('button', { name: /pay/i }))

    await waitFor(() => expect(checkoutBody).not.toBeNull())
    expect(checkoutBody!.discountCode).toBe('WELCOME10')
    expect(checkoutBody!.giftCardCode).toBe('GIFT25')
  })
})
