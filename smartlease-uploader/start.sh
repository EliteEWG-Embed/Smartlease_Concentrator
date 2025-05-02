#!/bin/bash
docker build -t smartlease-uploader .
docker run --rm -it -v $(pwd)/../shared/database:/database smartlease-uploader
