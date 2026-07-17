import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './AuthContext'

/** Route guard: only signed-in admins may render children; others are redirected to sign in. */
export default function RequireAdmin({ children }: { children: ReactNode }) {
  const { isAuthenticated, isAdmin, isLoading } = useAuth()
  const location = useLocation()

  if (isLoading) return <p style={{ padding: 32 }}>Checking access…</p>

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  if (!isAdmin) {
    return (
      <section style={{ padding: 32 }}>
        <h1>Not authorized</h1>
        <p className="muted">Your account does not have admin access.</p>
      </section>
    )
  }

  return <>{children}</>
}
