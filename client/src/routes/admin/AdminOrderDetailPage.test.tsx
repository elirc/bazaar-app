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
  giftCardTotal: { amount: 0, currency: 'USD' },
  giftCardCode: null,
  discountCode: null,
  items: [{ id: 'l1', variantId: 'v1', sku: 'BELT-34', title: 'Belt', quantity: 2, unitPrice: { amount: 45.5, currency: 'USD' }, lineTotal: { amount: 91, currency: 'USD' } }],
  placedAt: '2026-07-17T00:00:00Z',
  shipments: [] as unknown[],
}

afterEach(() => vi.unstubAllGlobals())

function renderDetail() {
  return renderWithProviders(
    <Routes>
      <Route path="/admin/orders/:id" element={<AdminOrderDetailPage />} />
    </Routes>,
    '/admin/orders/o1',
  )
}

describe('AdminOrderDetailPage', () => {
  it('creates a partial shipment and derives the status', async () => {
    const shipments: Array<{ url: string; body: Record<string, unknown> }> = []
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
        if (init?.method === 'POST' && url.includes('/shipments')) {
          shipments.push({ url, body: JSON.parse(String(init.body)) })
          return Promise.resolve(jsonResponse({ ...paidOrder, status: 'PartiallyFulfilled', shipments: [{ id: 's1', carrier: 'UPS', trackingNumber: '1Z-1', shippedAt: new Date().toISOString(), lines: [{ orderLineItemId: 'l1', sku: 'BELT-34', title: 'Belt', quantity: 1 }] }] }))
        }
        return Promise.resolve(jsonResponse(paidOrder))
      }),
    )

    const user = userEvent.setup()
    renderDetail()

    await waitFor(() => expect(screen.getByTestId('order-status')).toHaveTextContent('Paid'))

    await user.clear(screen.getByLabelText(/ship quantity for BELT-34/i))
    await user.type(screen.getByLabelText(/ship quantity for BELT-34/i), '1')
    await user.type(screen.getByLabelText('Carrier'), 'UPS')
    await user.type(screen.getByLabelText(/tracking number/i), '1Z-1')
    await user.click(screen.getByRole('button', { name: /create shipment/i }))

    await waitFor(() => expect(shipments).toHaveLength(1))
    expect(shipments[0].url).toContain('/api/admin/orders/o1/shipments')
    expect(shipments[0].body).toMatchObject({ carrier: 'UPS', trackingNumber: '1Z-1', lines: [{ orderLineItemId: 'l1', quantity: 1 }] })
    await waitFor(() => expect(screen.getByTestId('order-status')).toHaveTextContent('PartiallyFulfilled'))
  })

  it('offers refund/cancel transitions but not a manual fulfill', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(jsonResponse(paidOrder))))
    renderDetail()

    await waitFor(() => expect(screen.getByTestId('order-status')).toHaveTextContent('Paid'))
    expect(screen.getByRole('button', { name: /mark cancelled/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /mark refunded/i })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /mark fulfilled/i })).not.toBeInTheDocument()
  })
})
