# create the build container
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:90f913c96383b4146ce45985fd97e723fa1b1b6359441c4b683240236052eb59 AS build
LABEL stage=build
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o out

# create the runtime container
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:cc9c8da871c6e367a63122b858b10cfc464f5687bcfcf9d3761bcff1188cf257
WORKDIR /app
COPY --from=build /app/out .
COPY templates templates
EXPOSE 80
ENTRYPOINT ["dotnet", "evaluator.dll"]