import { describe, it, expect, vi, afterEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders, jsonResponse } from '../../test/utils'
import AdminReturnsPage from './AdminReturnsPage'

const payload = {
  items: [
    {
      id: 'rma1', orderId: 'o1', orderNumber: 'BZ-1001', email: 'buyer@example.com', status: 'Requested',
      reason: 'Wrong size', refundAmount: { amount: 0, currency: 'USD' },
      lines: [{ orderLineItemId: 'l1', sku: 'MUG-CRM', title: 'Stoneware Mug', quantity: 2 }],
      createdAt: new Date().toISOString(),
    },
  ],
  page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasPrevious: false, hasNext: false,
}

afterEach(() => vi.unstubAllGlobals())

describe('AdminReturnsPage', () => {
  it('lists requested returns and approves one', async () => {
    const approvals: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
        if (url.includes('/approve') && init?.method === 'POST') {
          approvals.push(url)
          return Promise.resolve(jsonResponse({ ...payload.items[0], status: 'Approved', refundAmount: { amount: 27.51, currency: 'USD' } }))
        }
        return Promise.resolve(jsonResponse(payload))
      }),
    )

    const user = userEvent.setup()
    renderWithProviders(<AdminReturnsPage />)

    await waitFor(() => expect(screen.getByText('buyer@example.com')).toBeInTheDocument())
    expect(screen.getByText('Wrong size')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /approve & refund/i }))
    await waitFor(() => expect(approvals).toHaveLength(1))
    expect(approvals[0]).toContain('/api/admin/returns/rma1/approve')
  })
})
