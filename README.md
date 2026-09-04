# DocQuery

Demo solution built to exercise concepts from the AI-200, AZ-104 and AZ-305 exams.

Users upload PDF documents through a React UI. Each document is stored in Azure Blob Storage and a
`DocumentUploaded` message is published to Azure Service Bus. A chunking worker splits the document,
publishes `DocumentChunked`, and an embedding worker generates embeddings with Azure OpenAI and stores
them in PostgreSQL (pgvector). A query API answers natural-language questions over the documents using a
RAG pipeline. All services are containerised and deployed to Kubernetes.

## Demo scope

This is a demonstration project. It deliberately omits concerns that any production system must have:

- **Authentication and authorization.** No endpoint is protected. A production deployment should put
  the APIs behind Microsoft Entra ID (or another identity provider) and enforce per-user document access.
- **Rate limiting, request size limits tuned per environment, secrets management, and network isolation**
  are likewise out of scope.

## Structure

```
DocQuery.slnx
src/
  backend/
    DocQuery.Command.Api/          write side: document upload
  clients/
    docquery.web/                  React + Vite UI
tests/
  backend/
    DocQuery.Command.Api.Tests/
```

Further projects (domain, application, contracts, persistence, infrastructure, query API, workers,
Aspire AppHost, deployment manifests) are added as the solution grows.

## Build and test

```bash
dotnet build
dotnet test
```

UI:

```bash
cd src/clients/docquery.web
npm install
npm run dev
```
