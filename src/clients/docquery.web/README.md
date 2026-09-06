# docquery.web

Vite + React + TypeScript front end for DocQuery: upload PDFs, watch them get chunked and embedded, ask questions
and read answers with citations. Design notes are in [ADR 0020](../../../docs/adr/0020-react-ui-with-fetch-and-polling.md).

| Command | What it does |
|---|---|
| `npm run dev` | Dev server (port from `PORT`, else 5173; Aspire sets it) |
| `npm test` | Vitest + Testing Library, once |
| `npm run test:watch` | Same, watching |
| `npm run build` | Type-check (`tsc -b`) and bundle to `dist/` |
| `npm run lint` | oxlint |
| `npm audit` | Must report zero known vulnerabilities before committing |

Configuration comes from environment variables read at build/dev time:

| Variable | Meaning | Default |
|---|---|---|
| `VITE_API_URL` | Command API base URL (uploads) | `http://localhost:5290` |
| `VITE_QUERY_API_URL` | Query API base URL (documents, questions) | `http://localhost:5291` |

Layout: `src/api` (typed client, error mapping, context), `src/hooks` (document list with polling),
`src/components` (upload form, document list, ask form, answer), `src/test` (setup and fakes).
