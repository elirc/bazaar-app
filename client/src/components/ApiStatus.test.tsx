import { describe, it, expect, vi, afterEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { renderWithProviders, jsonResponse } from '../test/utils'
import ApiStatus from './ApiStatus'

afterEach(() => vi.unstubAllGlobals())

describe('ApiStatus', () => {
  it('reflects the health endpoint status', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(jsonResponse({ status: 'ok', service: 'bazaar-api' }))))
    renderWithProviders(<ApiStatus />)

    await waitFor(() => expect(screen.getByTestId('api-status')).toHaveTextContent(/api ok/i))
  })

  it('shows offline when the request fails', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(jsonResponse({ title: 'boom' }, 500))))
    renderWithProviders(<ApiStatus />)

    await waitFor(() => expect(screen.getByTestId('api-status')).toHaveTextContent(/offline/i))
  })
})
