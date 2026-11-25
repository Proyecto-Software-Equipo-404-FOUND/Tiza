# syntax=docker/dockerfile:1
# Frontend de Tiza: se compila con Vite y lo sirve nginx.
# nginx reenvía /api y /hubs (SignalR, con WebSocket) al gateway, igual que el proxy de `npm run dev`.

FROM node:22-alpine AS build
WORKDIR /app
# Cypress no hace falta para compilar: se evita bajar su binario
ENV CYPRESS_INSTALL_BINARY=0
COPY package.json package-lock.json ./
RUN --mount=type=cache,id=npm,target=/root/.npm npm ci
COPY . .
# Siempre contra el backend real y por la misma URL (nginx hace de proxy).
# Las claves opcionales de Google Drive se toman del frontend/.env local si existe (no se versiona).
ENV VITE_USE_MOCKS=false \
    VITE_API_URL=
RUN npm run build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
