import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../auth'
import { Spinner } from './Spinner'

// UI-only guard for nicer navigation — the real checks happen in the services (JWT + roles).
export function RequireAuth({ children, role }: { children: ReactNode; role?: string }) {
  const { user, loading } = useAuth()
  const location = useLocation()

  if (loading) {
    return <Spinner />
  }

  if (!user.isAuthenticated) {
    const returnUrl = encodeURIComponent(location.pathname + location.search)
    return <Navigate to={`/login?returnUrl=${returnUrl}`} replace />
  }

  if (role && !user.roles.includes(role)) {
    return (
      <div className="empty-state">
        <h1>Access denied</h1>
        <p>You need the {role} role to see this page.</p>
      </div>
    )
  }

  return children
}
