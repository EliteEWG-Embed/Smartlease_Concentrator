
while true; do
  VOLT_HEX=$(i2cget -y 1 0x36 0x02 w)
  if [ $? -eq 0 ]; then
    VOLT_DEC=$(( ((VOLT_HEX & 0xFF) << 8) + (VOLT_HEX >> 8) ))
    VOLTAGE=$(echo "$VOLT_DEC * 1.25 / 1000" | bc -l)
    percent=$(awk -v v="$VOLTAGE" 'BEGIN {
      max=4.2; min=3.0;
      if (v > max) v = max;
      if (v < min) v = min;
      printf "%.0f", ((v - min) / (max - min)) * 100
    }')
    if [ "$percent" -lt 20 ]; then
      echo "[BATTERY] UPS faible : $percent%"
    else
      echo "[BATTERY] UPS OK : $percent%"
    fi
  else
    echo "[BATTERY] UPS non détecté"
  fi
  sleep 60
done
