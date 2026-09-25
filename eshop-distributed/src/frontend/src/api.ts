// Typed client for the BFF. Every call goes to the same origin (/api, /bff); the BFF
// holds the login cookie and forwards to Catalog/Basket/Orders with the user's JWT.

export interface Product {
  id: number
  name: string
  description: string
  price: number
  imageUrl: string
}

export interface BasketItem {
  productId: number
  productName: string
  price: number
  quantity: number
  color: string
}

export interface Basket {
  userName: string
  items: BasketItem[]
  totalPrice?: number
}

export interface OrderItem {
  id: number
  productId: number
  productName: string
  price: number
  quantity: number
}

export type OrderStatus = 'Pending' | 'Paid' | 'Cancelled'

export interface Order {
  id: number
  userName: string
  items: OrderItem[]
  totalAmount: number
  status: OrderStatus
  createdAtUtc: string
  paidAtUtc: string | null
}

export interface User {
  isAuthenticated: boolean
  name: string | null
  roles: string[]
}

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

// Fired on any 401 so the auth context can drop a session the BFF no longer accepts.
export const SESSION_EXPIRED_EVENT = 'eshop:session-expired'

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (method !== 'GET') {
    headers['X-CSRF'] = '1' // required by the BFF on every write
  }
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }

  const response = await fetch(url, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    credentials: 'same-origin',
  })

  if (response.status === 401 && url.startsWith('/api/')) {
    window.dispatchEvent(new Event(SESSION_EXPIRED_EVENT))
  }

  if (!response.ok) {
    throw new ApiError(response.status, await readError(response))
  }

  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

async function readError(response: Response): Promise<string> {
  const text = await response.text()
  if (!text) {
    return `Request failed (${response.status}).`
  }
  try {
    const json = JSON.parse(text)
    if (typeof json === 'string') return json
    return json.detail ?? json.title ?? text
  } catch {
    return text
  }
}

export const authApi = {
  user: () => request<User>('GET', '/bff/user'),
  login: (email: string, password: string) => request<User>('POST', '/bff/login', { email, password }),
  register: (email: string, password: string, fullName: string) =>
    request<User>('POST', '/bff/register', { email, password, fullName }),
  logout: () => request<void>('POST', '/bff/logout'),
}

type ProductInput = Omit<Product, 'id'>

export const catalogApi = {
  list: () => request<Product[]>('GET', '/api/products'),
  get: (id: number) => request<Product>('GET', `/api/products/${id}`),
  search: (query: string) => request<Product[]>('GET', `/api/products/search/${encodeURIComponent(query)}`),
  create: (product: ProductInput) => request<Product>('POST', '/api/products', product),
  update: (id: number, product: ProductInput) => request<void>('PUT', `/api/products/${id}`, product),
  remove: (id: number) => request<void>('DELETE', `/api/products/${id}`),
}

export const basketApi = {
  // Basket answers 404 when the user has no basket yet — treat that as empty.
  get: async (userName: string): Promise<Basket> => {
    try {
      return await request<Basket>('GET', `/api/basket/${encodeURIComponent(userName)}`)
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        return { userName, items: [] }
      }
      throw error
    }
  },
  save: (basket: Basket) => request<Basket>('POST', '/api/basket', { userName: basket.userName, items: basket.items }),
}

export const ordersApi = {
  checkout: () => request<{ orderId: number; checkoutUrl: string }>('POST', '/api/orders/checkout'),
  get: (id: number) => request<Order>('GET', `/api/orders/${id}`),
  invoiceUrl: (id: number) => `/api/orders/${id}/invoice`,
}
