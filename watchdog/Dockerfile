FROM alpine:latest

RUN apk add --no-cache curl bash

COPY watchdog.sh /watchdog.sh
RUN chmod +x /watchdog.sh

CMD ["nice", "-n", "10", "/watchdog.sh"]

