import { describe, it, expect, vi, afterEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders, jsonResponse } from '../../test/utils'
import AdminWebhooksPage from './AdminWebhooksPage'

const subs = [
  { id: 'w1', url: 'https://example.com/hooks', events: ['order.paid'], secret: 'whsec_abc', isActive: true, createdAt: new Date().toISOString() },
]
const deliveries = [
  { id: 'd1', subscriptionId: 'w1', event: 'order.paid', url: 'https://example.com/hooks', success: true, responseStatus: 200, attemptCount: 1, createdAt: new Date().toISOString() },
]

afterEach(() => vi.unstubAllGlobals())

describe('AdminWebhooksPage', () => {
  it('lists subscriptions and deliveries and creates a subscription', async () => {
    const created: Array<{ url: string; body: Record<string, unknown> }> = []
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
        if (init?.method === 'POST' && url.endsWith('/api/admin/webhooks')) {
          created.push({ url, body: JSON.parse(String(init.body)) })
          return Promise.resolve(jsonResponse({ ...subs[0], id: 'w2', url: 'https://new.example.com/h' }, 201))
        }
        if (url.includes('/deliveries')) return Promise.resolve(jsonResponse(deliveries))
        return Promise.resolve(jsonResponse(subs))
      }),
    )

    const user = userEvent.setup()
    renderWithProviders(<AdminWebhooksPage />)

    await waitFor(() => expect(screen.getByTestId('webhook-row')).toBeInTheDocument())
    expect(screen.getByTestId('delivery-row')).toBeInTheDocument()

    await user.type(screen.getByLabelText(/endpoint url/i), 'https://new.example.com/h')
    await user.click(screen.getByRole('button', { name: /add subscription/i }))

    await waitFor(() => expect(created).toHaveLength(1))
    expect(created[0].body).toMatchObject({ url: 'https://new.example.com/h', events: ['order.paid'] })
  })
})
