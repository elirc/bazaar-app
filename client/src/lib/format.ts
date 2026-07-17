import type { Money } from '../api/types'

export function formatMoney(money: Money | null | undefined): string {
  if (!money) return '—'
  try {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: money.currency,
    }).format(money.amount)
  } catch {
    return `${money.amount.toFixed(2)} ${money.currency}`
  }
}
