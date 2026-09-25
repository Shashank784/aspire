import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ordersApi, type Order } from '../api'
import { useBasket } from '../basket'
import { Spinner } from '../components/Spinner'
import { formatDate, formatPrice } from '../format'

const POLL_INTERVAL_MS = 2000
const MAX_POLLS = 15

// The payment provider's webhook marks the order Paid a moment after the redirect,
// so poll for a short while instead of asking the user to refresh.
export function OrderConfirmation() {
  const [searchParams] = useSearchParams()
  const orderId = Number(searchParams.get('orderId'))
  const { refresh: refreshBasket } = useBasket()
  const [order, setOrder] = useState<Order | null>(null)
  const [notFound, setNotFound] = useState(false)
  const [gaveUp, setGaveUp] = useState(false)

  useEffect(() => {
    let polls = 0
    let timer: number | undefined
    let cancelled = false

    const load = async () => {
      try {
        const result = await ordersApi.get(orderId)
        if (cancelled) return
        setOrder(result)

        if (result.status === 'Paid') {
          refreshBasket().catch(() => {}) // the basket is cleared once payment completes
        } else if (++polls < MAX_POLLS) {
          timer = window.setTimeout(load, POLL_INTERVAL_MS)
        } else {
          setGaveUp(true)
        }
      } catch {
        if (!cancelled) setNotFound(true)
      }
    }

    load()
    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [orderId, refreshBasket])

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
        <h1>Payment processing...</h1>
        {gaveUp ? (
          <p>We still haven't heard back from the payment provider. Please refresh this page in a little while.</p>
        ) : (
          <>
            <Spinner label="Waiting for payment confirmation" />
            <p className="muted">This usually takes just a few seconds.</p>
          </>
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
        <Link className="btn btn-ghost" to="/">
          Continue shopping
        </Link>
      </div>
    </div>
  )
}
