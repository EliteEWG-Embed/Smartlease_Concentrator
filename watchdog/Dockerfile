FROM alpine:latest

RUN apk add --no-cache curl bash tzdata i2c-tools bc

ENV TZ=Europe/Paris

COPY watchdog.sh /watchdog.sh
RUN chmod +x /watchdog.sh

CMD ["nice", "-n", "10", "/watchdog.sh"]
