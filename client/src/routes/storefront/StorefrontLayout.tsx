import { Link, Outlet, useNavigate } from 'react-router-dom'
import ApiStatus from '../../components/ApiStatus'
import CartDrawer from '../../components/CartDrawer'
import { useCart } from '../../cart/CartContext'
import { useAuth } from '../../auth/AuthContext'

export default function StorefrontLayout() {
  const { itemCount, toggle } = useCart()
  const { isAuthenticated, isAdmin, logout } = useAuth()
  const navigate = useNavigate()

  function signOut() {
    logout()
    navigate('/')
  }

  return (
    <div className="storefront">
      <header className="storefront__header">
        <Link to="/" className="brand">Bazaar</Link>
        <nav className="storefront__nav">
          <Link to="/">Shop</Link>
          {isAdmin && <Link to="/admin">Admin</Link>}
          {isAuthenticated ? (
            <>
              <Link to="/wishlist">Wishlist</Link>
              <Link to="/account">Account</Link>
              <button type="button" className="link" onClick={signOut}>Sign out</button>
            </>
          ) : (
            <Link to="/login">Sign in</Link>
          )}
          <button type="button" className="cart-button" onClick={toggle} aria-label={`Cart, ${itemCount} items`}>
            Cart ({itemCount})
          </button>
        </nav>
      </header>
      <main className="storefront__main">
        <Outlet />
      </main>
      <footer className="storefront__footer">
        <span>Bazaar — a Shopify-Lite demo storefront</span>
        <ApiStatus />
      </footer>
      <CartDrawer />
    </div>
  )
}
