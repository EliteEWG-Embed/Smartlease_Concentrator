#!/bin/bash

# Créer le dossier de logs s'il n'existe pas
mkdir -p /logs

# Nettoyer les anciens fichiers > 7 jours
find /logs -name "watchdog.*.log" -mtime +7 -exec rm -f {} \;

# Définir le fichier de log du jour
LOG_FILE="/logs/watchdog.$(date +%F).log"

log_info() {
  if [ "$LOG_CONSOLE_ENABLED" = "true" ]; then
    echo "[INFO] $1"
  else
    echo "$(date '+%F %T') [INFO] $1" >> "$LOG_FILE"
  fi
}

log_error() {
  if [ "$LOG_CONSOLE_ENABLED" = "true" ]; then
    echo "[ERROR] $1" >&2
  else
    echo "$(date '+%F %T') [ERROR] $1" >> "$LOG_FILE"
  fi
}

MAX_RETRIES=10
DELAY=30  # seconds
FAIL_COUNT=0

SUPERVISOR_URL="${BALENA_SUPERVISOR_ADDRESS}/ping"

log_info "[WATCHDOG] Using supervisor URL: $SUPERVISOR_URL"

while true; do
  RESPONSE=$(curl -s -X GET --header "Content-Type:application/json" "$SUPERVISOR_URL")

  if [ "$RESPONSE" = "OK" ]; then
    if [ "$FAIL_COUNT" -gt 0 ]; then
      log_info "[WATCHDOG] Supervisor is reachable again"
    fi
    FAIL_COUNT=0
  else
    FAIL_COUNT=$((FAIL_COUNT + 1))
    log_error "[WATCHDOG] Device is OFFLINE ($FAIL_COUNT/$MAX_RETRIES)"
  fi

  # Vérifie la connectivité Internet via Google
  wget --spider --quiet https://www.google.com
  if [ "$?" != 0 ]; then
    FAIL_COUNT=$((FAIL_COUNT + 1))
    log_error "[WATCHDOG] Internet is OFFLINE ($FAIL_COUNT/$MAX_RETRIES)"
  else
    if [ "$FAIL_COUNT" -gt 0 ]; then
      log_info "[WATCHDOG] Internet is reachable again"
    fi
    FAIL_COUNT=0
  fi

  if [ "$FAIL_COUNT" -ge "$MAX_RETRIES" ]; then
    log_error "[WATCHDOG] Offline too long. Triggering reboot"
    curl -X POST --header "Content-Type:application/json" \
      "$BALENA_SUPERVISOR_ADDRESS/v1/reboot?apikey=$BALENA_SUPERVISOR_API_KEY"
    exit 0
  fi

  sleep $DELAY
done
