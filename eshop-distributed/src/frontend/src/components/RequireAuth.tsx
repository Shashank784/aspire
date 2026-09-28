import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../auth'
import { Spinner } from './Spinner'

// UI-only guard for nicer navigation — the real checks happen in the services (JWT + roles).
// customerOnly: basket and checkout pages, which Admins don't use.
export function RequireAuth({ children, role, customerOnly }: { children: ReactNode; role?: string; customerOnly?: boolean }) {
  const { user, loading, isAdmin } = useAuth()
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

  if (customerOnly && isAdmin) {
    return (
      <div className="empty-state">
        <h1>Not available for Admins</h1>
        <p>Admins manage products and cannot buy them.</p>
      </div>
    )
  }

  return children
}
