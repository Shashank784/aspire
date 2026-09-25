import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth'
import { useBasket } from '../basket'

export function Layout() {
  const { user, isAdmin, logout } = useAuth()
  const { itemCount } = useBasket()
  const navigate = useNavigate()

  const handleLogout = async () => {
    await logout()
    navigate('/')
  }

  return (
    <div className="app">
      <header className="header">
        <div className="container header-inner">
          <Link to="/" className="brand">
            <span className="brand-mark">e</span>Shop
          </Link>

          <nav className="nav">
            <NavLink to="/" end>
              Shop
            </NavLink>
            {user.isAuthenticated && <NavLink to="/basket">Basket</NavLink>}
            {isAdmin && <NavLink to="/admin/products">Admin</NavLink>}
          </nav>

          <div className="header-actions">
            {user.isAuthenticated ? (
              <>
                <Link to="/basket" className="basket-button" aria-label={`Basket, ${itemCount} items`}>
                  <BasketIcon />
                  {itemCount > 0 && <span className="badge">{itemCount}</span>}
                </Link>
                <span className="user-name" title={user.name ?? ''}>
                  {user.name}
                </span>
                <button className="btn btn-ghost btn-sm" onClick={handleLogout}>
                  Log out
                </button>
              </>
            ) : (
              <>
                <Link to="/login" className="btn btn-ghost btn-sm">
                  Log in
                </Link>
                <Link to="/register" className="btn btn-primary btn-sm">
                  Register
                </Link>
              </>
            )}
          </div>
        </div>
      </header>

      <main className="container main">
        <Outlet />
      </main>

      <footer className="footer">
        <div className="container">eShop · built with .NET Aspire and React</div>
      </footer>
    </div>
  )
}

function BasketIcon() {
  return (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <circle cx="9" cy="21" r="1" />
      <circle cx="20" cy="21" r="1" />
      <path d="M1 1h4l2.7 13.4a2 2 0 0 0 2 1.6h9.7a2 2 0 0 0 2-1.6L23 6H6" />
    </svg>
  )
}
