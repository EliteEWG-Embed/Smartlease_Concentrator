FROM alpine:latest

RUN apk add --no-cache i2c-tools bash

COPY monitor.sh /monitor.sh
CMD ["sh", "/monitor.sh"]
