# Build UI first in a Node.js container
FROM --platform=$BUILDPLATFORM node:25-bookworm@sha256:39f92e620aa34854b8877b43bdffd411a301a50eefb38400785a01991f25a2f6 AS ui-build
WORKDIR /ui
COPY ui .
RUN npm install
RUN npm run build

# create the build container
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0@sha256:0a506ab0c8aa077361af42f82569d364ab1b8741e967955d883e3f23683d473a AS build
ARG TARGETARCH
LABEL stage=build
WORKDIR /api
COPY catalog .
RUN mkdir -p wwwroot/
COPY --from=ui-build /ui/dist/ ./wwwroot/
RUN dotnet publish -c Release -o out -a $TARGETARCH

# create the runtime container
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:52dcfb4225fda614c38ba5997a4ec72cbd5260a624125174416e547ff9eb9b8c
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
