import { useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import type { Product } from '../api'
import { useAuth } from '../auth'
import { useBasket } from '../basket'

// Browsing is open to everyone; adding to the basket sends guests to login first.
export function AddToBasketButton({ product, size = 'sm' }: { product: Product; size?: 'sm' | 'lg' }) {
  const { user } = useAuth()
  const { add } = useBasket()
  const navigate = useNavigate()
  const location = useLocation()
  const [state, setState] = useState<'idle' | 'busy' | 'added' | 'failed'>('idle')

  const handleClick = async () => {
    if (!user.isAuthenticated) {
      navigate(`/login?returnUrl=${encodeURIComponent(location.pathname + location.search)}`)
      return
    }

    setState('busy')
    try {
      await add(product)
      setState('added')
      setTimeout(() => setState('idle'), 1500)
    } catch {
      setState('failed')
    }
  }

  const label = { idle: 'Add to basket', busy: 'Adding...', added: 'Added ✓', failed: 'Failed — try again' }[state]

  return (
    <button
      className={`btn ${state === 'added' ? 'btn-success' : 'btn-primary'} ${size === 'lg' ? 'btn-lg' : 'btn-sm'}`}
      onClick={handleClick}
      disabled={state === 'busy'}
    >
      {label}
    </button>
  )
}
