# Builds the Vite app and serves the static bundle with nginx (ADR 0021).
# The API base URLs are baked in at build time; in the cluster both APIs sit behind the same ingress as the UI,
# so relative paths are used and no CORS is involved.
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
ARG VITE_API_URL=/api/command
ARG VITE_QUERY_API_URL=/api/query
ENV VITE_API_URL=$VITE_API_URL VITE_QUERY_API_URL=$VITE_QUERY_API_URL
RUN npm run build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
