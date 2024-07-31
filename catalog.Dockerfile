# create the build container
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
LABEL stage=build
WORKDIR /ui
COPY ui .
WORKDIR /catalog
COPY catalog .
RUN apt-get update && \
    apt-get install -y curl && \
    curl -sL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs && \
    apt-get clean
RUN ./refresh-ui.sh
RUN dotnet publish -c Release -o out -a $TARGETARCH

# create the runtime container
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /catalog/out .
COPY --from=build /catalog/wwwroot ./wwwroot
EXPOSE 80
ENTRYPOINT ["dotnet", "exp-catalog.dll"]
