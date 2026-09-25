import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { authApi, SESSION_EXPIRED_EVENT, type User } from './api'

const anonymous: User = { isAuthenticated: false, name: null, roles: [] }

interface AuthContextValue {
  user: User
  loading: boolean
  isAdmin: boolean
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string, fullName: string) => Promise<void>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User>(anonymous)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    authApi
      .user()
      .then(setUser)
      .catch(() => setUser(anonymous))
      .finally(() => setLoading(false))

    const onExpired = () => setUser(anonymous)
    window.addEventListener(SESSION_EXPIRED_EVENT, onExpired)
    return () => window.removeEventListener(SESSION_EXPIRED_EVENT, onExpired)
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    setUser(await authApi.login(email, password))
  }, [])

  const register = useCallback(async (email: string, password: string, fullName: string) => {
    setUser(await authApi.register(email, password, fullName))
  }, [])

  const logout = useCallback(async () => {
    await authApi.logout()
    setUser(anonymous)
  }, [])

  const value = useMemo(
    () => ({ user, loading, isAdmin: user.roles.includes('Admin'), login, register, logout }),
    [user, loading, login, register, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>')
  return context
}
