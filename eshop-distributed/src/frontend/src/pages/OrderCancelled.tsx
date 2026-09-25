import { useEffect } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ordersApi } from '../api'

export function OrderCancelled() {
  const [searchParams] = useSearchParams()
  const orderId = Number(searchParams.get('orderId'))

  // Mark the unpaid order as Cancelled so it doesn't stay Pending forever.
  useEffect(() => {
    if (orderId) {
      ordersApi.cancel(orderId).catch(() => {})
    }
  }, [orderId])

  return (
    <div className="empty-state card">
      <h1>Payment cancelled</h1>
      <p>Your payment was not completed. Your basket has been left untouched, so you can try again whenever you're ready.</p>
      <Link className="btn btn-primary" to="/basket">
        Back to basket
      </Link>
    </div>
  )
}
