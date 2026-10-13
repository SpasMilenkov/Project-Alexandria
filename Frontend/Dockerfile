# ===== BUILD STAGE =====
FROM node:24.15-alpine3.22@sha256:b689d4005875ae167178471a7a622ec2909459a3bbb32277260be1971af7a99f AS build
WORKDIR /app

# Keep in sync with packageManager in package.json
RUN npm install -g pnpm@12.4.2

# Copy package files for dependency caching
COPY package.json pnpm-lock.yaml  pnpm-workspace.yaml  ./
RUN pnpm install --frozen-lockfile

# Copy source and build
COPY . .
ARG VITE_API_BASE_URL=/api
ARG VITE_STREAMING_ENABLED=false
ARG VITE_PBKDF2_ITERATIONS=800000
ARG VITE_AUTOTAGGING_ENABLED=false
ENV CI=true
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL} \
    VITE_STREAMING_ENABLED=${VITE_STREAMING_ENABLED} \
    VITE_PBKDF2_ITERATIONS=${VITE_PBKDF2_ITERATIONS} \
    VITE_AUTOTAGGING_ENABLED=${VITE_AUTOTAGGING_ENABLED}
RUN pnpm run build-only

# ===== RUNTIME STAGE =====
FROM nginx:1.31.8-alpine3.24-slim@sha256:b899b0ef5345e14c01720a5ff83e716bacda65c7077a7363602ff7e7970bf88a AS final

# Remove default nginx config
RUN rm /etc/nginx/conf.d/default.conf

# Copy built assets and nginx config
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/nginx.conf

RUN addgroup -S nginx-app && adduser -S -G nginx-app nginx-app \
    && chown -R nginx-app:nginx-app /usr/share/nginx/html \
    && chown -R nginx-app:nginx-app /var/cache/nginx \
    && chown -R nginx-app:nginx-app /var/log/nginx \
    && touch /var/run/nginx.pid \
    && chown nginx-app:nginx-app /var/run/nginx.pid

USER nginx-app

EXPOSE 8080

CMD ["nginx", "-g", "daemon off;"]
