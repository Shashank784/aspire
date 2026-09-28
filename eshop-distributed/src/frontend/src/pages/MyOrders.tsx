import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ordersApi, type Order } from '../api'
import { Spinner } from '../components/Spinner'
import { formatDate, formatPrice } from '../format'

// Order history for the logged-in customer. Invoices exist only for paid orders.
export function MyOrders() {
  const [orders, setOrders] = useState<Order[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    ordersApi
      .list()
      .then(setOrders)
      .catch(() => setError('We could not load your orders. Please try again later.'))
  }, [])

  if (error) {
    return <div className="alert alert-error">{error}</div>
  }

  if (!orders) {
    return <Spinner label="Loading your orders..." />
  }

  return (
    <>
      <h1>My orders</h1>

      {orders.length === 0 ? (
        <div className="empty-state card">
          <p>You have not placed any orders yet.</p>
          <Link to="/" className="btn btn-primary">
            Start shopping
          </Link>
        </div>
      ) : (
        <div className="order-list">
          {orders.map((order) => (
            <article key={order.id} className="card order-card">
              <header className="order-head">
                <div>
                  <h2>Order #{order.id}</h2>
                  <span className="muted small">
                    Placed on {formatDate(order.createdAtUtc)}
                    {order.paidAtUtc && <> · paid on {formatDate(order.paidAtUtc)}</>}
                  </span>
                </div>
                <span className={`status status-${order.status.toLowerCase()}`}>{order.status}</span>
              </header>

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
                      <td className="nowrap">{formatPrice(item.price)}</td>
                      <td>{item.quantity}</td>
                      <td className="right nowrap">{formatPrice(item.price * item.quantity)}</td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr>
                    <td colSpan={3}>Total</td>
                    <td className="right nowrap">{formatPrice(order.totalAmount)}</td>
                  </tr>
                </tfoot>
              </table>

              {order.status === 'Paid' && (
                <div className="actions">
                  <a className="btn btn-primary btn-sm" href={ordersApi.invoiceUrl(order.id)}>
                    Download invoice
                  </a>
                </div>
              )}
            </article>
          ))}
        </div>
      )}
    </>
  )
}
