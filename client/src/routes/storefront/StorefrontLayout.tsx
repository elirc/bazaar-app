import { Link, Outlet } from 'react-router-dom'
import ApiStatus from '../../components/ApiStatus'
import CartDrawer from '../../components/CartDrawer'
import { useCart } from '../../cart/CartContext'

export default function StorefrontLayout() {
  const { itemCount, toggle } = useCart()

  return (
    <div className="storefront">
      <header className="storefront__header">
        <Link to="/" className="brand">Bazaar</Link>
        <nav className="storefront__nav">
          <Link to="/">Shop</Link>
          <Link to="/admin">Admin</Link>
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
