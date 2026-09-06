import { createContext } from 'react'
import { api, type ApiClient } from './client'

/** The API client for the tree; the real client by default, a fake in tests. */
export const ApiContext = createContext<ApiClient>(api)
