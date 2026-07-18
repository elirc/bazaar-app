import { describe, it, expect, vi, afterEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { renderWithProviders, jsonResponse } from '../../test/utils'
import AdminReportsPage from './AdminReportsPage'

const sales = {
  buckets: [{ date: '2026-07-17', orderCount: 3, revenue: { amount: 108.9, currency: 'USD' } }],
  totalOrders: 3,
  totalRevenue: { amount: 108.9, currency: 'USD' },
}
const top = [{ sku: 'MUG-CRM', title: 'Stoneware Mug', quantitySold: 5, revenue: { amount: 70, currency: 'USD' } }]
const lowStock = [{ variantId: 'v1', sku: 'BLNK-OAT', productTitle: 'Wool Throw', available: 3 }]
const discounts = [{ code: 'WELCOME10', type: 'Percentage', timesUsed: 4, usageLimit: 1000 }]

afterEach(() => vi.unstubAllGlobals())

describe('AdminReportsPage', () => {
  it('renders all four report sections', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = String(input)
        if (url.includes('/reports/sales')) return Promise.resolve(jsonResponse(sales))
        if (url.includes('/reports/top-products')) return Promise.resolve(jsonResponse(top))
        if (url.includes('/reports/low-stock')) return Promise.resolve(jsonResponse(lowStock))
        if (url.includes('/reports/discounts')) return Promise.resolve(jsonResponse(discounts))
        return Promise.resolve(jsonResponse({}, 404))
      }),
    )

    renderWithProviders(<AdminReportsPage />)

    await waitFor(() => expect(screen.getByTestId('sales-row')).toBeInTheDocument())
    expect(screen.getByTestId('top-product-row')).toBeInTheDocument()
    expect(screen.getByText('BLNK-OAT')).toBeInTheDocument()
    expect(screen.getByText('WELCOME10')).toBeInTheDocument()
    expect(screen.getByText(/108\.90 total/)).toBeInTheDocument()
  })
})
