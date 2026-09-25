import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth'
import { safeReturnUrl } from '../format'

export function Register() {
  const { register } = useAuth()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const returnUrl = safeReturnUrl(searchParams.get('returnUrl'))

  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await register(email, password, fullName)
      navigate(returnUrl, { replace: true })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Registration failed.')
      setBusy(false)
    }
  }

  return (
    <div className="auth-card card">
      <h1>Create an account</h1>
      <p className="muted">It only takes a minute.</p>

      {error && <div className="alert alert-error">{error}</div>}

      <form onSubmit={handleSubmit} className="form">
        <label>
          Full name
          <input value={fullName} onChange={(e) => setFullName(e.target.value)} autoComplete="name" />
        </label>
        <label>
          Email
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required autoComplete="email" />
        </label>
        <label>
          Password
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            minLength={6}
            autoComplete="new-password"
          />
          <span className="hint">At least 6 characters.</span>
        </label>
        <button type="submit" className="btn btn-primary btn-block" disabled={busy}>
          {busy ? 'Creating account...' : 'Register'}
        </button>
      </form>

      <p className="muted small">
        Already have an account? <Link to={`/login?returnUrl=${encodeURIComponent(returnUrl)}`}>Log in here</Link>
      </p>
    </div>
  )
}
