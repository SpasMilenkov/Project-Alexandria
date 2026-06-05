# ===== BUILD STAGE =====
FROM node:24.15-alpine3.22@sha256:b689d4005875ae167178471a7a622ec2909459a3bbb32277260be1971af7a99f AS build
WORKDIR /app

# Enable corepack for pnpm
RUN npm install -g pnpm@11.0.6

# Copy package files for dependency caching
COPY package.json pnpm-lock.yaml  pnpm-workspace.yaml  ./
RUN pnpm install --frozen-lockfile

# Copy source and build
COPY . .
ENV CI=true
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
