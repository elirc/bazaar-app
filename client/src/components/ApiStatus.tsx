import { useQuery } from '@tanstack/react-query'
import { getHealth } from '../api/health'

export default function ApiStatus() {
  const { data, isError } = useQuery({
    queryKey: ['health'],
    queryFn: ({ signal }) => getHealth(signal),
    staleTime: 60_000,
  })

  const db = data?.checks?.database
  const label = isError
    ? 'API offline'
    : data
      ? `API ${data.status}${db ? ` · DB ${db}` : ''}`
      : 'API…'
  return (
    <span className="api-status" data-testid="api-status">
      {label}
    </span>
  )
}
