import { describe, it, expect, vi, afterEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Routes, Route } from 'react-router-dom'
import { renderWithProviders, jsonResponse } from '../../test/utils'
import AdminOrderDetailPage from './AdminOrderDetailPage'

const paidOrder = {
  id: 'o1', number: 'BZ-1001', email: 'buyer@example.com', status: 'Paid', currency: 'USD',
  shippingAddress: { name: 'Grace', line1: '1 Navy Yard', line2: null, city: 'Arlington', region: null, postalCode: '22202', country: 'US' },
  subtotal: { amount: 45.5, currency: 'USD' },
  discountTotal: { amount: 0, currency: 'USD' },
  taxTotal: { amount: 3.75, currency: 'USD' },
  shippingTotal: { amount: 5.99, currency: 'USD' },
  grandTotal: { amount: 55.24, currency: 'USD' },
  discountCode: null,
  items: [{ sku: 'BELT-34', title: 'Belt', quantity: 1, unitPrice: { amount: 45.5, currency: 'USD' }, lineTotal: { amount: 45.5, currency: 'USD' } }],
  placedAt: '2026-07-17T00:00:00Z',
}

afterEach(() => vi.unstubAllGlobals())

describe('AdminOrderDetailPage', () => {
  it('shows a paid order and posts a status transition', async () => {
    const transitions: Array<{ url: string; body: string }> = []
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
        if (init?.method === 'POST' && url.includes('/transition')) {
          transitions.push({ url, body: String(init.body) })
          return Promise.resolve(jsonResponse({ ...paidOrder, status: 'Fulfilled' }))
        }
        return Promise.resolve(jsonResponse(paidOrder))
      }),
    )

    const user = userEvent.setup()
    renderWithProviders(
      <Routes>
        <Route path="/admin/orders/:id" element={<AdminOrderDetailPage />} />
      </Routes>,
      '/admin/orders/o1',
    )

    await waitFor(() => expect(screen.getByTestId('order-status')).toHaveTextContent('Paid'))

    await user.click(screen.getByRole('button', { name: /mark fulfilled/i }))

    await waitFor(() => expect(transitions).toHaveLength(1))
    expect(transitions[0].body).toContain('Fulfilled')
    await waitFor(() => expect(screen.getByTestId('order-status')).toHaveTextContent('Fulfilled'))
  })
})
