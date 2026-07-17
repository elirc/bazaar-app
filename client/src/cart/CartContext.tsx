import { createContext, useCallback, useContext, useState, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { addCartItem, createCart, getCart, removeCartItem, updateCartItem } from '../api/cart'
import type { Cart } from '../api/types'

const TOKEN_KEY = 'bazaar_cart_token'

interface CartContextValue {
  cart: Cart | undefined
  itemCount: number
  isLoading: boolean
  isOpen: boolean
  open: () => void
  close: () => void
  toggle: () => void
  addItem: (variantId: string, quantity?: number) => Promise<void>
  updateItem: (variantId: string, quantity: number) => Promise<void>
  removeItem: (variantId: string) => Promise<void>
  resetCart: () => void
}

const CartContext = createContext<CartContextValue | null>(null)

function readToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_KEY)
  } catch {
    return null
  }
}

export function CartProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [token, setToken] = useState<string | null>(readToken)
  const [isOpen, setIsOpen] = useState(false)

  const cartQuery = useQuery({
    queryKey: ['cart', token],
    queryFn: ({ signal }) => getCart(token!, signal),
    enabled: Boolean(token),
  })

  const persistToken = useCallback((next: string | null) => {
    setToken(next)
    try {
      if (next) localStorage.setItem(TOKEN_KEY, next)
      else localStorage.removeItem(TOKEN_KEY)
    } catch {
      // Ignore storage failures (e.g. private mode).
    }
  }, [])

  const ensureToken = useCallback(async (): Promise<string> => {
    if (token) return token
    const cart = await createCart()
    persistToken(cart.token)
    return cart.token
  }, [token, persistToken])

  const addItem = useCallback(
    async (variantId: string, quantity = 1) => {
      const activeToken = await ensureToken()
      const updated = await addCartItem(activeToken, variantId, quantity)
      queryClient.setQueryData(['cart', activeToken], updated)
      setIsOpen(true)
    },
    [ensureToken, queryClient],
  )

  const updateItem = useCallback(
    async (variantId: string, quantity: number) => {
      if (!token) return
      const updated = await updateCartItem(token, variantId, quantity)
      queryClient.setQueryData(['cart', token], updated)
    },
    [token, queryClient],
  )

  const removeItem = useCallback(
    async (variantId: string) => {
      if (!token) return
      const updated = await removeCartItem(token, variantId)
      queryClient.setQueryData(['cart', token], updated)
    },
    [token, queryClient],
  )

  const resetCart = useCallback(() => {
    persistToken(null)
    setIsOpen(false)
  }, [persistToken])

  const cart = cartQuery.data

  const value: CartContextValue = {
    cart,
    itemCount: cart?.itemCount ?? 0,
    isLoading: cartQuery.isLoading,
    isOpen,
    open: () => setIsOpen(true),
    close: () => setIsOpen(false),
    toggle: () => setIsOpen((prev) => !prev),
    addItem,
    updateItem,
    removeItem,
    resetCart,
  }

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useCart(): CartContextValue {
  const ctx = useContext(CartContext)
  if (!ctx) throw new Error('useCart must be used within a CartProvider')
  return ctx
}
