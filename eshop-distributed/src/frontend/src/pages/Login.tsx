import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth'
import { safeReturnUrl } from '../format'

export function Login() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const returnUrl = safeReturnUrl(searchParams.get('returnUrl'))

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await login(email, password)
      navigate(returnUrl, { replace: true })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Login failed.')
      setBusy(false)
    }
  }

  return (
    <div className="auth-card card">
      <h1>Welcome back</h1>
      <p className="muted">Log in to add products to your basket and check out.</p>

      {error && <div className="alert alert-error">{error}</div>}

      <form onSubmit={handleSubmit} className="form">
        <label>
          Email
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required autoComplete="email" />
        </label>
        <label>
          Password
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required autoComplete="current-password" />
        </label>
        <button type="submit" className="btn btn-primary btn-block" disabled={busy}>
          {busy ? 'Logging in...' : 'Log in'}
        </button>
      </form>

      <p className="muted small">
        Don't have an account? <Link to={`/register?returnUrl=${encodeURIComponent(returnUrl)}`}>Register here</Link>
      </p>
    </div>
  )
}
