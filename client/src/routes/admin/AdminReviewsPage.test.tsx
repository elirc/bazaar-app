import { describe, it, expect, vi, afterEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders, jsonResponse } from '../../test/utils'
import AdminReviewsPage from './AdminReviewsPage'

const payload = {
  items: [
    {
      id: 'r1', productId: 'p1', productTitle: 'Stoneware Mug', productSlug: 'ceramic-mug',
      authorName: 'Ada L.', rating: 4, title: 'Nice', body: 'Good mug.', status: 'Pending',
      isVerifiedPurchase: true, helpfulCount: 0, createdAt: new Date().toISOString(),
    },
  ],
  page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasPrevious: false, hasNext: false,
}

afterEach(() => vi.unstubAllGlobals())

describe('AdminReviewsPage', () => {
  it('lists pending reviews and approves one', async () => {
    const moderated: { url: string; body: unknown }[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
        if (url.includes('/moderate')) {
          moderated.push({ url, body: JSON.parse(String(init?.body)) })
          return Promise.resolve(jsonResponse({ id: 'r1', status: 'Approved' }))
        }
        return Promise.resolve(jsonResponse(payload))
      }),
    )

    const user = userEvent.setup()
    renderWithProviders(<AdminReviewsPage />)

    await waitFor(() => expect(screen.getByText('Good mug.')).toBeInTheDocument())
    await user.click(screen.getByRole('button', { name: /approve/i }))

    await waitFor(() => expect(moderated).toHaveLength(1))
    expect(moderated[0].url).toContain('/api/admin/reviews/r1/moderate')
    expect(moderated[0].body).toMatchObject({ status: 'Approved' })
  })
})
