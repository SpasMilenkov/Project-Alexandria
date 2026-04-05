# ===== BUILD STAGE =====
FROM node:22-alpine3.23@sha256:45ee7adabf5073b70c18746ef34be804077b8f5fa09d0188c19697590463b53e AS build
WORKDIR /app

# Enable corepack for pnpm
RUN npm install -g pnpm@10.32.1

# Copy package files for dependency caching
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile

# Copy source and build
COPY . .
ENV VITE_API_BASE_URL=/api
RUN pnpm run build-only

# ===== RUNTIME STAGE =====
FROM nginx:1.28.3-alpine-slim@sha256:b33eedfdf089be1f83759ced27b4deec5b6f1b6fc2a6819ebce0ae351a4406e5 AS final

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
