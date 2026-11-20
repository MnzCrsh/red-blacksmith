FROM node:26-alpine AS build

WORKDIR /app

COPY package.json package-lock.json ./

RUN npm ci

COPY . .

ARG VITE_GRAFANA_DASHBOARD_URL=""

ENV VITE_GRAFANA_DASHBOARD_URL=${VITE_GRAFANA_DASHBOARD_URL}

RUN npm run build

FROM nginxinc/nginx-unprivileged:stable-alpine AS final

COPY nginx.conf /etc/nginx/conf.d/default.conf

COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 8080
