import { Link } from 'react-router-dom'

export function OrderCancelled() {
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
