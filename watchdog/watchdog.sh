#!/bin/bash
set -euo pipefail

# Créer le dossier de logs s'il n'existe pas
mkdir -p /logs
find /logs -name "watchdog.*.log" -mtime +7 -exec rm -f {} \;
LOG_FILE="/logs/watchdog.$(date +%F).log"

log_info() {
  if [ "${LOG_CONSOLE_ENABLED:-false}" = "true" ]; then
    echo "[INFO] $1"
  else
    echo "$(date '+%F %T') [INFO] $1" >> "$LOG_FILE"
  fi
}

log_error() {
  if [ "${LOG_CONSOLE_ENABLED:-false}" = "true" ]; then
    echo "[ERROR] $1" >&2
  else
    echo "$(date '+%F %T') [ERROR] $1" >> "$LOG_FILE"
  fi
}

# Préparer dossier d'état d'alerte batterie
ALERT_STATE_DIR="/tmp/battery_alerts"
mkdir -p "$ALERT_STATE_DIR"

send_battery_alert() {
  local level="$1"
  local recipient="${ALERT_EMAIL:-}"

  if [ -z "$recipient" ]; then
    log_error "[BAT] ALERT_EMAIL not set. Skipping alert for ${level}%"
    return
  fi

  log_info "[BAT] Alert: battery dropped below ${level}% – sending email to $recipient"
  echo "Battery level is below ${level}% on $(hostname) – current: ${percent}%" \
    | mail -s "Battery Alert ${level}%" "$recipient"
}

battery_log() {
  local raw_v=$(i2cget -y 1 0x42 0x02 w 2>/dev/null | tr -d '\r') || return
  [[ $raw_v =~ ^0x[0-9a-fA-F]{4}$ ]] || { log_error "[BAT] invalid bus V raw: $raw_v"; return; }
  local dec_v=$((0x${raw_v#0x}))
  local swapped_v=$(( (dec_v & 0xFF) << 8 | (dec_v >> 8) ))
  local bus_bits=$(( swapped_v >> 3 ))
  local bus_voltage=$(awk "BEGIN { printf \"%.3f\", $bus_bits * 0.004 }")

  local raw_i=$(i2cget -y 1 0x42 0x04 w 2>/dev/null | tr -d '\r') || return
  [[ $raw_i =~ ^0x[0-9a-fA-F]{4}$ ]] || { log_error "[BAT] invalid I raw: $raw_i"; return; }
  local dec_i=$((0x${raw_i#0x}))
  local swapped_i=$(( (dec_i & 0xFF) << 8 | (dec_i >> 8) ))
  local current=$(awk "BEGIN { printf \"%.3f\", $swapped_i * 0.0001 }")

  local raw_p=$(i2cget -y 1 0x42 0x03 w 2>/dev/null | tr -d '\r') || return
  [[ $raw_p =~ ^0x[0-9a-fA-F]{4}$ ]] || { log_error "[BAT] invalid P raw: $raw_p"; return; }
  local dec_p=$((0x${raw_p#0x}))
  local swapped_p=$(( (dec_p & 0xFF) << 8 | (dec_p >> 8) ))
  local power=$(awk "BEGIN { printf \"%.3f\", $swapped_p * 0.002 }")

  local percent=$(awk "BEGIN {
        p = ($bus_voltage - 6) / 2.4 * 100;
        if (p > 100) p = 100;
        if (p < 0) p = 0;
        printf \"%d\", p
      }")

  # Lire les seuils depuis env
  IFS=',' read -r -a THRESHOLDS <<< "${ALERT_THRESHOLDS:-75,50,25}"

  for threshold in "${THRESHOLDS[@]}"; do
    threshold="$(echo "$threshold" | tr -d '[:space:]')"
    local FLAG_FILE="$ALERT_STATE_DIR/alert_${threshold}"

    if [ "$percent" -lt "$threshold" ]; then
      if [ ! -f "$FLAG_FILE" ]; then
        send_battery_alert "$threshold"
        touch "$FLAG_FILE"
      fi
    else
      [ -f "$FLAG_FILE" ] && rm -f "$FLAG_FILE"
    fi
  done

  log_info "[BAT] est. ${percent}% – ${bus_voltage} V – ${current} A – ${power} W"
}

MAX_RETRIES=10
DELAY=30
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

  battery_log
  sleep $DELAY
done
