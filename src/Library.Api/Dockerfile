# Build from the repository root:
#   docker build -f src/Library.Api/Dockerfile -t library-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, on their own layer. Restore then only re-runs when a
# dependency actually changes, rather than on every source edit.
COPY LibrarySystem.slnx ./
COPY src/Library.Domain/Library.Domain.csproj                 src/Library.Domain/
COPY src/Library.Application/Library.Application.csproj       src/Library.Application/
COPY src/Library.Infrastructure/Library.Infrastructure.csproj src/Library.Infrastructure/
COPY src/Library.Api/Library.Api.csproj                       src/Library.Api/
COPY tests/Library.UnitTests/Library.UnitTests.csproj         tests/Library.UnitTests/

RUN dotnet restore src/Library.Api/Library.Api.csproj

COPY . .
RUN dotnet publish src/Library.Api/Library.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The base image provides a non-root account. Running as root inside a container is
# a habit worth not having, and nothing here needs privileges.
USER $APP_UID

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Library.Api.dll"]
