import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ordersApi } from '../api'
import { useBasket } from '../basket'
import { Spinner } from '../components/Spinner'
import { formatPrice } from '../format'

export function BasketPage() {
  const { basket, changeQuantity, remove } = useBasket()
  const [busyProductId, setBusyProductId] = useState<number | null>(null)
  const [checkingOut, setCheckingOut] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (!basket) {
    return <Spinner label="Loading basket..." />
  }

  const total = basket.items.reduce((sum, i) => sum + i.price * i.quantity, 0)

  const run = async (productId: number, action: () => Promise<void>, failMessage: string) => {
    setBusyProductId(productId)
    setError(null)
    try {
      await action()
    } catch {
      setError(failMessage)
    } finally {
      setBusyProductId(null)
    }
  }

  const handleCheckout = async () => {
    setCheckingOut(true)
    setError(null)
    try {
      const { checkoutUrl } = await ordersApi.checkout()
      window.location.href = checkoutUrl // Stripe's hosted payment page
    } catch (e) {
      setError(e instanceof Error ? `Failed to start checkout: ${e.message}` : 'Failed to start checkout.')
      setCheckingOut(false)
    }
  }

  return (
    <>
      <h1>My basket</h1>

      {error && <div className="alert alert-error">{error}</div>}

      {basket.items.length === 0 ? (
        <div className="empty-state card">
          <p>Your basket is empty.</p>
          <Link to="/" className="btn btn-primary">
            Start shopping
          </Link>
        </div>
      ) : (
        <div className="basket-layout">
          <div className="card basket-items">
            {basket.items.map((item) => {
              const busy = busyProductId === item.productId
              return (
                <div key={item.productId} className="basket-row">
                  <div className="basket-info">
                    <Link to={`/products/${item.productId}`} className="product-name">
                      {item.productName}
                    </Link>
                    <span className="muted small">{formatPrice(item.price)} each</span>
                  </div>
                  <div className="qty">
                    <button
                      className="btn btn-ghost btn-icon"
                      aria-label="Decrease quantity"
                      disabled={busy || item.quantity <= 1}
                      onClick={() => run(item.productId, () => changeQuantity(item.productId, -1), 'Failed to update quantity.')}
                    >
                      −
                    </button>
                    <span>{item.quantity}</span>
                    <button
                      className="btn btn-ghost btn-icon"
                      aria-label="Increase quantity"
                      disabled={busy}
                      onClick={() => run(item.productId, () => changeQuantity(item.productId, 1), 'Failed to update quantity.')}
                    >
                      +
                    </button>
                  </div>
                  <span className="basket-subtotal">{formatPrice(item.price * item.quantity)}</span>
                  <button
                    className="btn btn-link-danger"
                    disabled={busy}
                    onClick={() => run(item.productId, () => remove(item.productId), 'Failed to remove item.')}
                  >
                    Remove
                  </button>
                </div>
              )
            })}
          </div>

          <aside className="card summary">
            <h2>Order summary</h2>
            <div className="summary-row">
              <span>Items</span>
              <span>{basket.items.reduce((sum, i) => sum + i.quantity, 0)}</span>
            </div>
            <div className="summary-row summary-total">
              <span>Total</span>
              <span>{formatPrice(total)}</span>
            </div>
            <button className="btn btn-primary btn-block btn-lg" onClick={handleCheckout} disabled={checkingOut}>
              {checkingOut ? 'Redirecting to payment...' : 'Proceed to checkout'}
            </button>
          </aside>
        </div>
      )}
    </>
  )
}
