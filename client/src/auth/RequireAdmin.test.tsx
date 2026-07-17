import { describe, it, expect, vi, afterEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { renderWithProviders, jsonResponse } from '../test/utils'
import { AuthProvider } from './AuthContext'
import RequireAdmin from './RequireAdmin'
import { setAuthToken } from '../api/client'

const TOKEN_KEY = 'bazaar_auth_token'

function stubMe(role: string) {
  vi.stubGlobal(
    'fetch',
    vi.fn(() =>
      Promise.resolve(
        jsonResponse({ id: 'u1', email: 'u@example.com', firstName: null, lastName: null, role }),
      ),
    ),
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
  setAuthToken(null)
})

describe('RequireAdmin', () => {
  it('renders children for an admin session', async () => {
    localStorage.setItem(TOKEN_KEY, 'admin-token')
    stubMe('Admin')

    renderWithProviders(
      <AuthProvider>
        <RequireAdmin>
          <div>Secret admin area</div>
        </RequireAdmin>
      </AuthProvider>,
    )

    await waitFor(() => expect(screen.getByText('Secret admin area')).toBeInTheDocument())
  })

  it('blocks a signed-in non-admin', async () => {
    localStorage.setItem(TOKEN_KEY, 'customer-token')
    stubMe('Customer')

    renderWithProviders(
      <AuthProvider>
        <RequireAdmin>
          <div>Secret admin area</div>
        </RequireAdmin>
      </AuthProvider>,
    )

    await waitFor(() => expect(screen.getByText('Not authorized')).toBeInTheDocument())
    expect(screen.queryByText('Secret admin area')).not.toBeInTheDocument()
  })
})
