import { useContext } from 'react'
import { ApiContext } from './apiContext'
import type { ApiClient } from './client'

export function useApi(): ApiClient {
  return useContext(ApiContext)
}
