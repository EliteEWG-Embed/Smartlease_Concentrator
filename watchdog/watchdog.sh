#!/bin/bash

MAX_RETRIES=1
DELAY=30  # seconds
FAIL_COUNT=0

SUPERVISOR_URL="${BALENA_SUPERVISOR_ADDRESS}/ping"

echo "[WATCHDOG] Using supervisor URL: $SUPERVISOR_URL"

while true; do
  RESPONSE=$(curl -s -X GET --header "Content-Type:application/json" "$SUPERVISOR_URL")

  if [ "$RESPONSE" = "OK" ]; then
    echo "[WATCHDOG] Supervisor is reachable"
    FAIL_COUNT=0
  else
    FAIL_COUNT=$((FAIL_COUNT + 1))
    echo "[WATCHDOG] Device is OFFLINE ($FAIL_COUNT/$MAX_RETRIES)"
  fi
  wget --spider --quiet https://www.google.com
  if [ "$?" != 0 ]; then
    FAIL_COUNT=$((FAIL_COUNT + 1))
    echo "[WATCHDOG] Internet is OFFLINE ($FAIL_COUNT/$MAX_RETRIES)"
  else
    echo "[WATCHDOG] Internet is reachable"
    FAIL_COUNT=0
  fi

  if [ "$FAIL_COUNT" -ge "$MAX_RETRIES" ]; then
    echo "[WATCHDOG] Offline too long. Triggering reboot"
    curl -X POST --header "Content-Type:application/json" \
    "$BALENA_SUPERVISOR_ADDRESS/v1/reboot?apikey=$BALENA_SUPERVISOR_API_KEY"
    exit 0
  fi

  sleep $DELAY
done
