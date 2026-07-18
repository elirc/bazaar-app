import { describe, it, expect, vi, afterEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders, jsonResponse } from '../test/utils'
import ReturnRequestForm from './ReturnRequestForm'
import type { Order } from '../api/types'

const money = (amount: number) => ({ amount, currency: 'USD' })

const order: Order = {
  id: 'order-1',
  number: 'BZ-1001',
  email: 'buyer@example.com',
  status: 'Fulfilled',
  currency: 'USD',
  shippingAddress: { name: 'Ada', line1: '1 Main St', line2: null, city: 'Denver', region: null, postalCode: '80202', country: 'US' },
  subtotal: money(28),
  discountTotal: money(0),
  taxTotal: money(2.31),
  shippingTotal: money(5.99),
  grandTotal: money(36.3),
  discountCode: null,
  shippingMethod: 'Standard',
  giftCardTotal: money(0),
  giftCardCode: null,
  items: [
    { id: 'l1', variantId: 'v1', sku: 'MUG-CRM', title: 'Stoneware Mug', quantity: 2, unitPrice: money(14), lineTotal: money(28) },
  ],
  placedAt: new Date().toISOString(),
  shipments: [],
}

afterEach(() => vi.unstubAllGlobals())

describe('ReturnRequestForm', () => {
  it('submits the selected line quantities and confirms the request', async () => {
    let posted: { url: string; body: Record<string, unknown> } | null = null
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
        if (url.includes('/returns') && init?.method === 'POST') {
          posted = { url, body: JSON.parse(String(init?.body)) }
          return Promise.resolve(
            jsonResponse(
              { id: 'rma1', orderId: 'order-1', orderNumber: 'BZ-1001', status: 'Requested', reason: 'Wrong size', refundAmount: money(0), lines: [], createdAt: new Date().toISOString() },
              201,
            ),
          )
        }
        return Promise.resolve(jsonResponse({}, 404))
      }),
    )

    const user = userEvent.setup()
    renderWithProviders(<ReturnRequestForm order={order} />)

    // Submit is disabled until a quantity is chosen.
    const submit = screen.getByRole('button', { name: /submit return/i })
    expect(submit).toBeDisabled()

    const qty = screen.getByLabelText('Return quantity for MUG-CRM')
    await user.clear(qty)
    await user.type(qty, '2')
    await user.type(screen.getByLabelText(/reason/i), 'Wrong size')

    expect(submit).toBeEnabled()
    await user.click(submit)

    await waitFor(() => expect(posted).not.toBeNull())
    expect(posted!.url).toContain('/api/account/orders/order-1/returns')
    expect(posted!.body).toMatchObject({ reason: 'Wrong size', lines: [{ orderLineItemId: 'l1', quantity: 2 }] })

    await waitFor(() =>
      expect(screen.getByText(/your return request was submitted/i)).toBeInTheDocument(),
    )
  })

  it('caps the return quantity at the ordered amount', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(jsonResponse({}, 404))))
    const user = userEvent.setup()
    renderWithProviders(<ReturnRequestForm order={order} />)

    const qty = screen.getByLabelText('Return quantity for MUG-CRM') as HTMLInputElement
    await user.clear(qty)
    await user.type(qty, '9') // ordered quantity is 2

    expect(Number(qty.value)).toBeLessThanOrEqual(2)
  })
})
