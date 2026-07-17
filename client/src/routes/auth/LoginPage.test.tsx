import { describe, it, expect, vi, afterEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders, jsonResponse } from '../../test/utils'
import { AuthProvider } from '../../auth/AuthContext'
import LoginPage from './LoginPage'
import { setAuthToken } from '../../api/client'

afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
  setAuthToken(null)
})

describe('LoginPage', () => {
  it('posts credentials to the login endpoint', async () => {
    const calls: { url: string; body: unknown }[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
        if (url.includes('/api/auth/login')) {
          calls.push({ url, body: JSON.parse(String(init?.body)) })
          return Promise.resolve(
            jsonResponse({
              token: 'jwt-token',
              expiresAt: new Date().toISOString(),
              customer: { id: 'u1', email: 'sam@example.com', firstName: null, lastName: null, role: 'Customer' },
            }),
          )
        }
        return Promise.resolve(jsonResponse({}))
      }),
    )

    const user = userEvent.setup()
    renderWithProviders(
      <AuthProvider>
        <LoginPage />
      </AuthProvider>,
    )

    await user.type(screen.getByLabelText('Email'), 'sam@example.com')
    await user.type(screen.getByLabelText('Password'), 'supersecret')
    await user.click(screen.getByRole('button', { name: /sign in/i }))

    await waitFor(() => expect(calls).toHaveLength(1))
    expect(calls[0].body).toMatchObject({ email: 'sam@example.com', password: 'supersecret' })
  })
})
