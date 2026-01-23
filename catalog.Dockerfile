# Build UI first in a Node.js container
FROM --platform=$BUILDPLATFORM node:25-bookworm@sha256:c7afcee8493fc98aaa1fb5c5b9ed396ed98ffe596921feba4cae0c6a63c8cfeb AS ui-build
WORKDIR /ui
COPY ui .
RUN npm install
RUN npm run build

# create the build container
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0@sha256:25d14b400b75fa4e89d5bd4487a92a604a4e409ab65becb91821e7dc4ac7f81f AS build
ARG TARGETARCH
LABEL stage=build
WORKDIR /api
COPY catalog .
RUN mkdir -p wwwroot/
COPY --from=ui-build /ui/dist/ ./wwwroot/
RUN dotnet publish -c Release -o out -a $TARGETARCH

# create the runtime container
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:1aacc8154bc3071349907dae26849df301188be1a2e1f4560b903fb6275e481a
ARG INSTALL_AZURE_CLI=false
WORKDIR /app
COPY --from=build /api/out .
COPY --from=build /api/wwwroot ./wwwroot

# Conditionally install Azure CLI
RUN if [ "$INSTALL_AZURE_CLI" = "true" ]; then \
    apt-get update && \
    apt-get install -y ca-certificates curl apt-transport-https lsb-release gnupg && \
    mkdir -p /etc/apt/keyrings && \
    curl -sLS https://packages.microsoft.com/keys/microsoft.asc | \
    gpg --dearmor -o /etc/apt/keyrings/microsoft.gpg && \
    chmod go+r /etc/apt/keyrings/microsoft.gpg && \
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/microsoft.gpg] https://packages.microsoft.com/repos/azure-cli/ $(lsb_release -cs) main" | \
    tee /etc/apt/sources.list.d/azure-cli.list && \
    apt-get update && \
    apt-get install -y azure-cli && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*; \
    fi

ENTRYPOINT ["dotnet", "exp-catalog.dll"]
