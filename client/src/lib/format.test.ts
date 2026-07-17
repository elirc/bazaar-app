import { describe, it, expect } from 'vitest'
import { formatMoney } from './format'

describe('formatMoney', () => {
  it('formats a USD amount as currency', () => {
    expect(formatMoney({ amount: 19.99, currency: 'USD' })).toBe('$19.99')
  })

  it('renders a dash for a missing amount', () => {
    expect(formatMoney(null)).toBe('—')
    expect(formatMoney(undefined)).toBe('—')
  })

  it('falls back gracefully for an unknown currency code', () => {
    expect(formatMoney({ amount: 5, currency: 'ZZZ' })).toContain('5')
  })
})
