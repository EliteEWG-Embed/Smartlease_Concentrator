FROM node:18-bullseye-slim

WORKDIR /app

RUN apt-get update && apt-get install -y \
    python3 \
    make \
    g++ \
    sqlite3 \
    libsqlite3-dev

COPY package*.json ./
RUN npm install
RUN rm -rf node_modules && npm install && npm rebuild better-sqlite3 --build-from-source



COPY . .

EXPOSE 80

CMD ["node", "server.js"]
