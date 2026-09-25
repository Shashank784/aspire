import { Link, Navigate, Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { RequireAuth } from './components/RequireAuth'
import { AdminProducts } from './pages/AdminProducts'
import { BasketPage } from './pages/BasketPage'
import { Login } from './pages/Login'
import { MockPayment } from './pages/MockPayment'
import { OrderCancelled } from './pages/OrderCancelled'
import { OrderConfirmation } from './pages/OrderConfirmation'
import { ProductDetails } from './pages/ProductDetails'
import { Products } from './pages/Products'
import { Register } from './pages/Register'

export default function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<Products />} />
        <Route path="products" element={<Navigate to="/" replace />} />
        <Route path="products/:id" element={<ProductDetails />} />
        <Route path="login" element={<Login />} />
        <Route path="register" element={<Register />} />
        <Route path="basket" element={<RequireAuth><BasketPage /></RequireAuth>} />
        <Route path="mock-payment" element={<RequireAuth><MockPayment /></RequireAuth>} />
        <Route path="order-confirmation" element={<RequireAuth><OrderConfirmation /></RequireAuth>} />
        <Route path="order-cancelled" element={<RequireAuth><OrderCancelled /></RequireAuth>} />
        <Route path="admin/products" element={<RequireAuth role="Admin"><AdminProducts /></RequireAuth>} />
        <Route
          path="*"
          element={
            <div className="empty-state">
              <h1>Page not found</h1>
              <Link to="/" className="btn btn-primary">
                Back to shop
              </Link>
            </div>
          }
        />
      </Route>
    </Routes>
  )
}
