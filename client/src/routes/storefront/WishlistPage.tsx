import { useState, type FormEvent } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createWishlist,
  deleteWishlist,
  listWishlists,
  moveWishlistItemToCart,
  removeWishlistItem,
} from '../../api/wishlist'
import { useAuth } from '../../auth/AuthContext'
import { useCart } from '../../cart/CartContext'
import { formatMoney } from '../../lib/format'

export default function WishlistPage() {
  const { isAuthenticated, isLoading } = useAuth()
  const { token, applyCart } = useCart()
  const queryClient = useQueryClient()
  const [name, setName] = useState('')

  const wishlistsQuery = useQuery({
    queryKey: ['wishlists'],
    queryFn: ({ signal }) => listWishlists(signal),
    enabled: isAuthenticated,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['wishlists'] })

  const createMutation = useMutation({
    mutationFn: () => createWishlist(name),
    onSuccess: () => {
      invalidate()
      setName('')
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteWishlist(id),
    onSuccess: invalidate,
  })

  const removeMutation = useMutation({
    mutationFn: ({ wishlistId, variantId }: { wishlistId: string; variantId: string }) =>
      removeWishlistItem(wishlistId, variantId),
    onSuccess: invalidate,
  })

  const moveMutation = useMutation({
    mutationFn: ({ wishlistId, variantId }: { wishlistId: string; variantId: string }) =>
      moveWishlistItemToCart(wishlistId, variantId, token ?? undefined),
    onSuccess: (cart) => {
      applyCart(cart)
      invalidate()
    },
  })

  if (isLoading) return <p>Loading…</p>
  if (!isAuthenticated) return <Navigate to="/login" replace state={{ from: '/wishlist' }} />

  function submit(event: FormEvent) {
    event.preventDefault()
    createMutation.mutate()
  }

  return (
    <section className="wishlist-page">
      <h1>Wishlists</h1>

      <form className="admin-form inline" onSubmit={submit}>
        <input
          aria-label="New wishlist name"
          placeholder="New list name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          required
        />
        <button type="submit" className="primary" disabled={createMutation.isPending}>Create list</button>
      </form>

      {wishlistsQuery.isLoading && <p>Loading…</p>}
      {wishlistsQuery.data?.map((wishlist) => (
        <div key={wishlist.id} className="wishlist" data-testid="wishlist">
          <div className="wishlist__head">
            <h2>{wishlist.name}{wishlist.isDefault && <span className="badge"> Default</span>}</h2>
            {!wishlist.isDefault && (
              <button type="button" onClick={() => deleteMutation.mutate(wishlist.id)}>Delete list</button>
            )}
          </div>

          {wishlist.items.length === 0 ? (
            <p className="muted">This list is empty.</p>
          ) : (
            <ul className="wishlist__items">
              {wishlist.items.map((item) => (
                <li key={item.variantId} className="wishlist__item" data-testid="wishlist-item">
                  <div>
                    <Link to={`/products/${item.productSlug}`}>{item.productTitle}</Link>
                    <div className="muted">{item.variantTitle} · {formatMoney(item.price)}</div>
                    {item.backInStock && <span className="badge">Back in stock!</span>}
                    {item.available <= 0 && !item.backInStock && <span className="muted"> Out of stock</span>}
                  </div>
                  <div className="row-actions">
                    <button
                      type="button"
                      className="primary"
                      disabled={item.available <= 0 || moveMutation.isPending}
                      onClick={() => moveMutation.mutate({ wishlistId: wishlist.id, variantId: item.variantId })}
                    >
                      Move to cart
                    </button>
                    <button
                      type="button"
                      onClick={() => removeMutation.mutate({ wishlistId: wishlist.id, variantId: item.variantId })}
                    >
                      Remove
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
      ))}
    </section>
  )
}
