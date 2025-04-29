FROM alpine:latest

# Installer les outils nécessaires
RUN apk add --no-cache \
    gcc \
    musl-dev \
    make \
    sqlite \
    sqlite-dev

# Définir le répertoire de travail
WORKDIR /app

# Copier le code source
COPY . .

# Créer le dossier de base de données
RUN mkdir -p /database

# Compiler l'application
RUN make

# Démarrer l'application
CMD ["./antenna_capture", "/dev/ttyACM0"]
