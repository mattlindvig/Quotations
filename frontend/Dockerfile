# Build stage
FROM node:18-alpine AS build
WORKDIR /app

# Copy package files
COPY package.json package-lock.json ./

# Install dependencies
RUN npm ci

# Copy source code
COPY . .

# Build the application
RUN npm run build

# Production stage
FROM node:18-alpine AS production
WORKDIR /app

# Install a simple HTTP server for serving static content
RUN npm install -g serve

# Copy built assets from build stage
COPY --from=build /app/dist /app/dist

# Install curl for healthcheck
RUN apk add --no-cache curl

EXPOSE 5173

# Serve the application
CMD ["serve", "-s", "dist", "-l", "5173"]