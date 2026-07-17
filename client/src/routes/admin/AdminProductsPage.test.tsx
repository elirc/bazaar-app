import { describe, it, expect, vi, afterEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders, jsonResponse } from '../../test/utils'
import AdminProductsPage from './AdminProductsPage'

const payload = {
  items: [
    {
      id: 'p1', slug: 'classic-tee', title: 'Classic Tee', vendor: 'Bazaar', status: 'Active',
      imageUrl: null, priceFrom: { amount: 19.99, currency: 'USD' }, collections: ['apparel'],
    },
  ],
  page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasPrevious: false, hasNext: false,
}

afterEach(() => vi.unstubAllGlobals())

describe('AdminProductsPage', () => {
  it('lists products from the admin API', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(jsonResponse(payload))))
    renderWithProviders(<AdminProductsPage />)

    await waitFor(() => expect(screen.getByText('Classic Tee')).toBeInTheDocument())
    expect(screen.getAllByTestId('admin-product-row')).toHaveLength(1)
  })

  it('issues a DELETE when the delete button is clicked', async () => {
    const deleteUrls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
        if (init?.method === 'DELETE') {
          deleteUrls.push(url)
          return Promise.resolve(new Response(null, { status: 204 }))
        }
        return Promise.resolve(jsonResponse(payload))
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<AdminProductsPage />)

    await waitFor(() => expect(screen.getByText('Classic Tee')).toBeInTheDocument())
    await user.click(screen.getByRole('button', { name: /delete/i }))

    await waitFor(() => expect(deleteUrls).toHaveLength(1))
    expect(deleteUrls[0]).toContain('/api/admin/products/p1')
  })
})
