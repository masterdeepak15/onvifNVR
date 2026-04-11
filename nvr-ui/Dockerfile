# ─── Stage 1: Build React app ─────────────────────────────────────────────────
FROM node:20-alpine AS build
WORKDIR /app

# Accept build-time API URL.
# Leave empty (default) when UI is served behind nginx that proxies /api/ and /hubs/.
# Set to http://your-api-host:5000 only when UI is on a completely separate server.
ARG VITE_API_URL=""
ENV VITE_API_URL=${VITE_API_URL}

COPY package*.json ./
RUN npm ci --ignore-scripts

COPY . .
RUN npm run build

# ─── Stage 2: Serve via nginx ─────────────────────────────────────────────────
FROM nginx:alpine AS final

COPY --from=build /app/dist /usr/share/nginx/html

# nginx config handles:
#   - React SPA routing (all unknown paths → index.html)
#   - /api/  proxy → nvr-api:8080
#   - /hubs/ WebSocket proxy → nvr-api:8080
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
