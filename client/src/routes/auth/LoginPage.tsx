import { useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { ApiError } from '../../api/client'

export default function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation() as { state?: { from?: string } }

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setPending(true)
    try {
      const user = await login({ email, password })
      const dest = location.state?.from ?? (user.role === 'Admin' ? '/admin' : '/account')
      navigate(dest, { replace: true })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Sign in failed.')
    } finally {
      setPending(false)
    }
  }

  return (
    <section className="auth-page">
      <h1>Sign in</h1>
      <form className="admin-form" onSubmit={submit}>
        <label>
          Email
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
        </label>
        <label>
          Password
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        </label>
        {error && <p className="error" role="alert">{error}</p>}
        <button type="submit" className="primary" disabled={pending}>
          {pending ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
      <p className="muted">
        No account? <Link to="/register">Create one</Link>.
      </p>
    </section>
  )
}
