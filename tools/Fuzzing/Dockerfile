# tools/Fuzzing/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN printf "nameserver 1.1.1.1\nnameserver 8.8.8.8\n" > /etc/resolv.conf

# AFL++ + basic build tooling
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    afl++ \
    clang \
    llvm \
    make \
    git \
    ca-certificates \
    rsync \
    && rm -rf /var/lib/apt/lists/*

RUN dotnet tool install --tool-path /opt/tools SharpFuzz.CommandLine --version 2.2.0

ENV PATH="/opt/tools:${PATH}"

WORKDIR /work

# Copy the runner script into the image
COPY tools/Fuzzing/run-afl.sh /usr/local/bin/run-afl.sh
RUN chmod +x /usr/local/bin/run-afl.sh

ENTRYPOINT ["/usr/local/bin/run-afl.sh"]
