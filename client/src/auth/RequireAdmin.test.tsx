import { describe, it, expect, vi, afterEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
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

  it('redirects a guest to the sign-in page', async () => {
    // No token in storage -> the guard navigates to /login instead of rendering the area.
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <MemoryRouter initialEntries={['/admin']}>
            <Routes>
              <Route
                path="/admin"
                element={
                  <RequireAdmin>
                    <div>Secret admin area</div>
                  </RequireAdmin>
                }
              />
              <Route path="/login" element={<div>Sign in page</div>} />
            </Routes>
          </MemoryRouter>
        </AuthProvider>
      </QueryClientProvider>,
    )

    await waitFor(() => expect(screen.getByText('Sign in page')).toBeInTheDocument())
    expect(screen.queryByText('Secret admin area')).not.toBeInTheDocument()
  })
})
