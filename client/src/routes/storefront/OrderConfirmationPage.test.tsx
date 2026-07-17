import { describe, it, expect, vi, afterEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { Routes, Route } from 'react-router-dom'
import { renderWithProviders, jsonResponse } from '../../test/utils'
import OrderConfirmationPage from './OrderConfirmationPage'

const order = {
  id: 'o1', number: 'BZ-1001', email: 'buyer@example.com', status: 'Paid', currency: 'USD',
  shippingAddress: { name: 'Ada', line1: '1 Way', line2: null, city: 'London', region: null, postalCode: 'EC1A', country: 'GB' },
  subtotal: { amount: 28, currency: 'USD' },
  discountTotal: { amount: 0, currency: 'USD' },
  taxTotal: { amount: 2.31, currency: 'USD' },
  shippingTotal: { amount: 5.99, currency: 'USD' },
  grandTotal: { amount: 36.3, currency: 'USD' },
  discountCode: null,
  items: [{ sku: 'MUG-CRM', title: 'Stoneware Mug', quantity: 2, unitPrice: { amount: 14, currency: 'USD' }, lineTotal: { amount: 28, currency: 'USD' } }],
  placedAt: '2026-07-17T00:00:00Z',
}

afterEach(() => vi.unstubAllGlobals())

describe('OrderConfirmationPage', () => {
  it('shows the placed order number and total', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(jsonResponse(order))))
    renderWithProviders(
      <Routes>
        <Route path="/order/:id" element={<OrderConfirmationPage />} />
      </Routes>,
      '/order/o1',
    )

    await waitFor(() => expect(screen.getByTestId('order-number')).toHaveTextContent(/BZ-1001/))
    expect(screen.getByText('$36.30')).toBeInTheDocument()
  })
})
