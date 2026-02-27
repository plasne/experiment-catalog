# Build UI first in a Node.js container
FROM --platform=$BUILDPLATFORM node:25-bookworm@sha256:c4bfed36421c310d1fbb6dc51faf98065768fbc1c2c1ddd554813ecaa81bb2db AS ui-build
WORKDIR /ui
COPY ui .
RUN npm install
RUN npm run build

# create the build container
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e362a8dbcd691522456da26a5198b8f3ca1d7641c95624fadc5e3e82678bd08a AS build
ARG TARGETARCH
LABEL stage=build
WORKDIR /api
COPY catalog .
RUN mkdir -p wwwroot/
COPY --from=ui-build /ui/dist/ ./wwwroot/
RUN dotnet publish -c Release -o out -a $TARGETARCH

# create the runtime container
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:aec87aa74ddf129da573fa69f42f229a23c953a1c6fdecedea1aa6b1fe147d76
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
