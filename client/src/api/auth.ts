import { apiRequest } from './client'
import type { AuthResponse, CurrentUser, CustomerAddress, Order, OrderSummary } from './types'
import type { AddressInput } from './cart'

export interface RegisterBody {
  email: string
  password: string
  firstName?: string
  lastName?: string
}

export interface LoginBody {
  email: string
  password: string
}

export function register(body: RegisterBody) {
  return apiRequest<AuthResponse>('/api/auth/register', { method: 'POST', body })
}

export function login(body: LoginBody) {
  return apiRequest<AuthResponse>('/api/auth/login', { method: 'POST', body })
}

export function getCurrentUser(signal?: AbortSignal) {
  return apiRequest<CurrentUser>('/api/auth/me', { signal })
}

export function listAccountOrders(signal?: AbortSignal) {
  return apiRequest<OrderSummary[]>('/api/account/orders', { signal })
}

export function getAccountOrder(id: string, signal?: AbortSignal) {
  return apiRequest<Order>(`/api/account/orders/${id}`, { signal })
}

export interface UpsertAddressBody {
  label?: string
  isDefault: boolean
  address: AddressInput
}

export function listAddresses(signal?: AbortSignal) {
  return apiRequest<CustomerAddress[]>('/api/account/addresses', { signal })
}

export function createAddress(body: UpsertAddressBody) {
  return apiRequest<CustomerAddress>('/api/account/addresses', { method: 'POST', body })
}

export function updateAddress(id: string, body: UpsertAddressBody) {
  return apiRequest<CustomerAddress>(`/api/account/addresses/${id}`, { method: 'PUT', body })
}

export function deleteAddress(id: string) {
  return apiRequest<void>(`/api/account/addresses/${id}`, { method: 'DELETE' })
}
