import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { basketApi, type Basket, type Product } from './api'
import { useAuth } from './auth'

interface BasketContextValue {
  basket: Basket | null
  itemCount: number
  refresh: () => Promise<void>
  add: (product: Product) => Promise<void>
  changeQuantity: (productId: number, delta: number) => Promise<void>
  remove: (productId: number) => Promise<void>
  clear: () => void
}

const BasketContext = createContext<BasketContextValue | null>(null)

// Same flow as the old BasketActions: read the basket, change it, save it back.
// Basket service re-fetches price/name from Catalog on every save.
export function BasketProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const userName = user.isAuthenticated ? user.name : null
  const [basket, setBasket] = useState<Basket | null>(null)

  const refresh = useCallback(async () => {
    setBasket(userName ? await basketApi.get(userName) : null)
  }, [userName])

  useEffect(() => {
    refresh().catch(() => setBasket(null))
  }, [refresh])

  const update = useCallback(
    async (change: (basket: Basket) => void) => {
      if (!userName) throw new Error('Please log in first.')
      const current = await basketApi.get(userName)
      change(current)
      await basketApi.save(current)
      setBasket(await basketApi.get(userName))
    },
    [userName],
  )

  const add = useCallback(
    (product: Product) =>
      update((basket) => {
        const existing = basket.items.find((i) => i.productId === product.id)
        if (existing) {
          existing.quantity++
        } else {
          basket.items.push({
            productId: product.id,
            productName: product.name,
            price: product.price,
            quantity: 1,
            color: 'Default',
          })
        }
      }),
    [update],
  )

  // Quantity never drops below 1 here — use remove() to take an item out.
  const changeQuantity = useCallback(
    (productId: number, delta: number) =>
      update((basket) => {
        const item = basket.items.find((i) => i.productId === productId)
        if (item) item.quantity = Math.max(1, item.quantity + delta)
      }),
    [update],
  )

  const remove = useCallback(
    (productId: number) =>
      update((basket) => {
        basket.items = basket.items.filter((i) => i.productId !== productId)
      }),
    [update],
  )

  const clear = useCallback(() => setBasket(userName ? { userName, items: [] } : null), [userName])

  const value = useMemo(
    () => ({
      basket,
      itemCount: basket?.items.reduce((sum, i) => sum + i.quantity, 0) ?? 0,
      refresh,
      add,
      changeQuantity,
      remove,
      clear,
    }),
    [basket, refresh, add, changeQuantity, remove, clear],
  )

  return <BasketContext.Provider value={value}>{children}</BasketContext.Provider>
}

export function useBasket() {
  const context = useContext(BasketContext)
  if (!context) throw new Error('useBasket must be used inside <BasketProvider>')
  return context
}
