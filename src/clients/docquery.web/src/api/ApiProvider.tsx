import type { ReactNode } from 'react'
import { ApiContext } from './apiContext'
import type { ApiClient } from './client'

/** Supplies the API client to the tree; tests wrap components with a fake client. */
export function ApiProvider({ client, children }: { client: ApiClient; children: ReactNode }) {
  return <ApiContext.Provider value={client}>{children}</ApiContext.Provider>
}
