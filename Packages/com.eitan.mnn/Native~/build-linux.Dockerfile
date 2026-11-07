# The amd64 Ubuntu 24.04 image used for the verified GNU x86_64 build.
FROM ubuntu:24.04@sha256:fe81560990fae86dc4743202b2cb4d3df98d0bcf949173cecef15ce1127ef852
ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential gcc-13=13.3.0-6ubuntu2~24.04.1 g++-13=13.3.0-6ubuntu2~24.04.1 \
    cmake python3 mono-runtime ca-certificates \
    && rm -rf /var/lib/apt/lists/*
