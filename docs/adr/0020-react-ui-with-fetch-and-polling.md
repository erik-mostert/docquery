# 0020. React UI talks to both APIs with fetch and polls for document progress

Date: 2026-09-06

## Status

Accepted

## Context

The backend is complete: the command API accepts uploads, the query API lists documents and answers questions.
The demo needs a browser front end that shows the pipeline working (a document moving from Uploaded through
Chunked to Embedded) and shows a grounded answer with its citations. The Vite React TypeScript template in
`src/clients/docquery.web` had been scaffolded but not touched. The project rule of test-first development applies
to the front end as well.

Constraints: two API origins (CQRS, ADR 0002), no authentication (ADR 0005), and Aspire assigns the dev server
its port, so the APIs cannot hard-code the browser origin they allow.

## Decision

**Plain React with fetch, no framework beyond Vite.** State is component state plus one custom hook; there is no
router, no state library, no component library and no generated client. The API surface is four calls and one
page, so anything more would be scaffolding for its own sake.

**One API client module.** `createApiClient({ commandApiUrl, queryApiUrl, fetch })` returns typed functions for
upload, list, get and ask, and turns RFC 9457 problem details into an `ApiError` carrying status, title, detail and
`Retry-After`. The URLs come from `VITE_API_URL` and `VITE_QUERY_API_URL`, set by the AppHost from the API
endpoints; the client is handed to components through a React context so tests substitute a fake without mocking
modules.

**Polling for progress.** `useDocuments` loads the list once and re-fetches every three seconds while any
document is `Uploaded` or `Chunked`, stopping when every document is `Embedded` or `Failed`. The query API gained
`GET /documents` and `GET /documents/{id}` for this. Polling is the simplest mechanism that shows the asynchronous
pipeline (ADR 0011) without adding a push channel.

**Citations are rendered from data.** The answer's `[n]` markers become links to the numbered citation entries,
which list file, page, similarity and excerpt from the API response (ADR 0019).

**CORS origin from the AppHost.** The Vite dev server is declared first, and both APIs receive
`Cors__AllowedOrigins__0` from its endpoint reference, so whatever port Aspire allocates is the allowed origin.
`vite.config.ts` honours `PORT` with `strictPort` so the origin cannot drift. The `appsettings.json` default of
`http://localhost:5173` remains for running the UI outside Aspire.

**Testing.** Vitest with Testing Library and jsdom. Each component and the hook have tests against a fake API
client; the client has tests against a fake `fetch`. `npm test` runs them; `npm run build` type-checks with
`erasableSyntaxOnly`, so TypeScript-only constructs such as parameter properties are avoided.

## Consequences

- The UI shows the pipeline's asynchronous nature honestly: an upload returns at once and the row changes status
  on its own within a few seconds.
- No push channel means up to three seconds of lag and one list request per interval per open browser while
  documents are processing; idle browsers make no requests.
- Adding a page (document detail, chat history) means adding a router; that is deferred until a second page
  exists.
- Vulnerability hygiene: `npm audit` reports zero known vulnerabilities at the time of writing and is part of the
  checklist before committing front-end changes.

## Alternatives considered

- **SignalR or Server-Sent Events for progress.** Real-time, but a new hub or stream endpoint, a connection to
  manage in the browser and more to explain; polling covers the demo's needs.
- **A generated OpenAPI client.** Type-safe by construction, but a build step and generated code for four calls;
  a hand-written client with tests is smaller and reads better.
- **A component library or Tailwind.** Faster styling, but a dependency tree to audit for a single page; a short
  stylesheet with CSS variables was enough.
- **Next.js.** Server rendering and routing are not needed; the app is a thin client over two APIs.
