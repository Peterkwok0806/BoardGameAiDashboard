# ─────────────────────────────────────────────────────────────────────────────
# Build Stage
# ─────────────────────────────────────────────────────────────────────────────
FROM node:20-alpine AS build

WORKDIR /app

# Install Angular CLI globally
RUN npm install -g @angular/cli@19

# Copy package files first (for better Docker layer caching)
COPY package*.json ./

# Install dependencies
RUN npm ci --legacy-peer-deps

# Copy build configuration files
COPY angular.json ./
COPY tsconfig*.json ./
COPY tailwind.config.* ./
COPY postcss.config.* ./

# Copy source code
COPY src/ ./src/

# Build Angular application
RUN ng build --configuration production

# ─────────────────────────────────────────────────────────────────────────────
# Runtime Stage
# ─────────────────────────────────────────────────────────────────────────────
FROM nginx:alpine AS runtime

# Copy SPA fallback configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy build output to nginx html directory
COPY --from=build /app/dist/dashboard-frontend/browser /usr/share/nginx/html

# Expose port
EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
