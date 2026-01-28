FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /VRemoteServer

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:5.0
WORKDIR /VRemoteServer
COPY --from=build /VRemoteServer/out .

ENV DOTNET_EnableDiagnostics=0

ENTRYPOINT ["dotnet", "VRemoteServer.dll"]