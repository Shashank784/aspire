import { useEffect, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { ordersApi, type Order } from '../api'
import { Spinner } from '../components/Spinner'
import { formatPrice } from '../format'

// Stand-in for Stripe's hosted payment page, used when Orders runs with Payment:Provider = Mock.
export function MockPayment() {
  const [searchParams] = useSearchParams()
  const orderId = Number(searchParams.get('orderId'))
  const navigate = useNavigate()
  const [order, setOrder] = useState<Order | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [paying, setPaying] = useState(false)

  useEffect(() => {
    ordersApi
      .get(orderId)
      .then(setOrder)
      .catch(() => setError('Order not found.'))
  }, [orderId])

  const handlePay = async () => {
    setPaying(true)
    setError(null)
    try {
      await ordersApi.mockPay(orderId)
      navigate(`/order-confirmation?orderId=${orderId}`, { replace: true })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Payment failed.')
      setPaying(false)
    }
  }

  if (error && !order) {
    return (
      <div className="empty-state">
        <p>{error}</p>
        <Link to="/basket" className="btn btn-primary">
          Back to basket
        </Link>
      </div>
    )
  }

  if (!order) {
    return <Spinner label="Loading payment..." />
  }

  if (order.status !== 'Pending') {
    const target = order.status === 'Paid' ? `/order-confirmation?orderId=${order.id}` : '/basket'
    return (
      <div className="empty-state card">
        <p>This order is already {order.status.toLowerCase()}.</p>
        <Link to={target} className="btn btn-primary">
          Continue
        </Link>
      </div>
    )
  }

  return (
    <div className="card payment">
      <span className="test-badge">Test mode · no real money</span>
      <h1>Pay for order #{order.id}</h1>

      <table className="table">
        <tbody>
          {order.items.map((item) => (
            <tr key={item.id}>
              <td>
                {item.productName} <span className="muted">× {item.quantity}</span>
              </td>
              <td className="right">{formatPrice(item.price * item.quantity)}</td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <td>Total</td>
            <td className="right">{formatPrice(order.totalAmount)}</td>
          </tr>
        </tfoot>
      </table>

      {error && <div className="alert alert-error">{error}</div>}

      <button className="btn btn-primary btn-block btn-lg" onClick={handlePay} disabled={paying}>
        {paying ? 'Processing...' : `Pay ${formatPrice(order.totalAmount)}`}
      </button>
      <Link to={`/order-cancelled?orderId=${order.id}`} className="btn btn-ghost btn-block payment-cancel">
        Cancel
      </Link>
    </div>
  )
}
