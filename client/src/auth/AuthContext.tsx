import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { setAuthToken } from '../api/client'
import { getCurrentUser, login as loginRequest, register as registerRequest } from '../api/auth'
import type { LoginBody, RegisterBody } from '../api/auth'
import type { CurrentUser } from '../api/types'

const TOKEN_KEY = 'bazaar_auth_token'

interface AuthContextValue {
  user: CurrentUser | null
  token: string | null
  isAuthenticated: boolean
  isAdmin: boolean
  isLoading: boolean
  login: (body: LoginBody) => Promise<CurrentUser>
  register: (body: RegisterBody) => Promise<CurrentUser>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

function readToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_KEY)
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(readToken)
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [isLoading, setIsLoading] = useState<boolean>(Boolean(readToken()))

  const persistToken = useCallback((next: string | null) => {
    setToken(next)
    setAuthToken(next)
    try {
      if (next) localStorage.setItem(TOKEN_KEY, next)
      else localStorage.removeItem(TOKEN_KEY)
    } catch {
      // Ignore storage failures (e.g. private mode).
    }
  }, [])

  // Restore the session on load: seed the client token, then verify it against /me.
  useEffect(() => {
    const existing = readToken()
    if (!existing) {
      setIsLoading(false)
      return
    }
    setAuthToken(existing)
    let cancelled = false
    getCurrentUser()
      .then((me) => {
        if (!cancelled) setUser(me)
      })
      .catch(() => {
        if (!cancelled) persistToken(null)
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [persistToken])

  const login = useCallback(
    async (body: LoginBody) => {
      const auth = await loginRequest(body)
      persistToken(auth.token)
      setUser(auth.customer)
      return auth.customer
    },
    [persistToken],
  )

  const register = useCallback(
    async (body: RegisterBody) => {
      const auth = await registerRequest(body)
      persistToken(auth.token)
      setUser(auth.customer)
      return auth.customer
    },
    [persistToken],
  )

  const logout = useCallback(() => {
    persistToken(null)
    setUser(null)
  }, [persistToken])

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      token,
      isAuthenticated: Boolean(user),
      isAdmin: user?.role === 'Admin',
      isLoading,
      login,
      register,
      logout,
    }),
    [user, token, isLoading, login, register, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider')
  return ctx
}
