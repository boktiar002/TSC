FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore

RUN dotnet publish TSC.csproj -c Release -o /app/publish --no-restore


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 10000

# Render injects PORT; 10000 is its default and what EXPOSE above documents.
# `sh -c "<script>"` puts any extra args into $0/$1..., where the script never saw them, so a
# one-off `create-admin <email> <password>` was silently swallowed and the web server booted
# instead. Pass "$@" through ("--" becomes $0) and set the port via ASPNETCORE_URLS, leaving
# argv free for the app's own commands.
ENTRYPOINT ["sh", "-c", "export ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000}; exec dotnet TSC.dll \"$@\"", "--"]
