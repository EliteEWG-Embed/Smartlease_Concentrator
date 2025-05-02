#!/bin/bash
docker build -t antenna-capture .
docker run --rm -it --privileged --device=/dev/ttyACM0 antenna-capture