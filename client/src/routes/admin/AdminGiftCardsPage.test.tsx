import { describe, it, expect, vi, afterEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders, jsonResponse } from '../../test/utils'
import AdminGiftCardsPage from './AdminGiftCardsPage'

const cards = [
  { id: 'g1', code: 'GIFT25', balance: { amount: 25, currency: 'USD' }, initialBalance: { amount: 25, currency: 'USD' }, isActive: true, createdAt: new Date().toISOString() },
]

afterEach(() => vi.unstubAllGlobals())

describe('AdminGiftCardsPage', () => {
  it('lists gift cards and issues a new one', async () => {
    const issued: { url: string; body: unknown }[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
        if (init?.method === 'POST') {
          issued.push({ url, body: JSON.parse(String(init?.body)) })
          return Promise.resolve(jsonResponse({ id: 'g2', code: 'GC-NEW', balance: { amount: 50, currency: 'USD' }, initialBalance: { amount: 50, currency: 'USD' }, isActive: true, createdAt: new Date().toISOString() }, 201))
        }
        return Promise.resolve(jsonResponse(cards))
      }),
    )

    const user = userEvent.setup()
    renderWithProviders(<AdminGiftCardsPage />)

    await waitFor(() => expect(screen.getByText('GIFT25')).toBeInTheDocument())

    await user.type(screen.getByLabelText(/gift card amount/i), '50')
    await user.click(screen.getByRole('button', { name: /issue/i }))

    await waitFor(() => expect(issued).toHaveLength(1))
    expect(issued[0].url).toContain('/api/admin/gift-cards')
    expect(issued[0].body).toMatchObject({ amount: 50 })
  })
})
