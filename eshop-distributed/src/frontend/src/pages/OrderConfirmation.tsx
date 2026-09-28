import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ordersApi, type Order } from '../api'
import { useBasket } from '../basket'
import { Spinner } from '../components/Spinner'
import { formatDate, formatPrice } from '../format'

// The mock payment page marks the order Paid before it sends the customer here.
export function OrderConfirmation() {
  const [searchParams] = useSearchParams()
  const orderId = Number(searchParams.get('orderId'))
  const { clear: clearBasket } = useBasket()
  const [order, setOrder] = useState<Order | null>(null)
  const [notFound, setNotFound] = useState(false)

  useEffect(() => {
    let cancelled = false

    ordersApi
      .get(orderId)
      .then((result) => {
        if (cancelled) return
        setOrder(result)
        if (result.status === 'Paid') {
          // Basket's OrderPaid listener also clears it, but that runs a moment later —
          // clear it here too so the header and basket page are empty right away.
          clearBasket().catch(() => {})
        }
      })
      .catch(() => !cancelled && setNotFound(true))

    return () => {
      cancelled = true
    }
  }, [orderId, clearBasket])

  if (notFound) {
    return (
      <div className="empty-state">
        <p>Order not found.</p>
        <Link to="/" className="btn btn-primary">
          Back to shop
        </Link>
      </div>
    )
  }

  if (!order) {
    return <Spinner label="Loading your order..." />
  }

  if (order.status !== 'Paid') {
    return (
      <div className="empty-state card">
        <h1>This order is not paid</h1>
        <p>Order #{order.id} is {order.status.toLowerCase()}.</p>
        {order.status === 'Pending' && (
          <Link to={`/mock-payment?orderId=${order.id}`} className="btn btn-primary">
            Go to payment
          </Link>
        )}
      </div>
    )
  }

  return (
    <div className="card confirmation">
      <div className="success-mark" aria-hidden="true">
        ✓
      </div>
      <h1>Thank you! Your order is confirmed.</h1>
      <p className="muted">
        Order #{order.id} · paid on {formatDate(order.paidAtUtc)}
      </p>

      <table className="table">
        <thead>
          <tr>
            <th>Product</th>
            <th>Price</th>
            <th>Qty</th>
            <th className="right">Subtotal</th>
          </tr>
        </thead>
        <tbody>
          {order.items.map((item) => (
            <tr key={item.id}>
              <td>{item.productName}</td>
              <td>{formatPrice(item.price)}</td>
              <td>{item.quantity}</td>
              <td className="right">{formatPrice(item.price * item.quantity)}</td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <td colSpan={3}>Total</td>
            <td className="right">{formatPrice(order.totalAmount)}</td>
          </tr>
        </tfoot>
      </table>

      <div className="actions">
        <a className="btn btn-primary" href={ordersApi.invoiceUrl(order.id)}>
          Download invoice
        </a>
        <Link className="btn btn-ghost" to="/orders">
          My orders
        </Link>
        <Link className="btn btn-ghost" to="/">
          Continue shopping
        </Link>
      </div>
    </div>
  )
}
