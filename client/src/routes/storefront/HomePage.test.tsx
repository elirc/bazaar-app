import { describe, it, expect, vi, afterEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import HomePage from './HomePage'

function renderWithQuery(ui: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>)
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('HomePage', () => {
  it('renders the storefront heading immediately', () => {
    vi.stubGlobal('fetch', vi.fn().mockReturnValue(new Promise(() => {})))
    renderWithQuery(<HomePage />)
    expect(screen.getByRole('heading', { name: /welcome to bazaar/i })).toBeInTheDocument()
  })

  it('shows the API status once the health check resolves', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ status: 'ok', service: 'bazaar-api' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    vi.stubGlobal('fetch', fetchMock)

    renderWithQuery(<HomePage />)

    await waitFor(() =>
      expect(screen.getByTestId('api-status')).toHaveTextContent(/api status: ok/i),
    )
  })
})
