# Stufe 1: Blazor-WebAssembly-App bauen (Release, mit Trimming und vorkomprimierten Dateien).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Zeiterfassung.Web/ src/Zeiterfassung.Web/
RUN dotnet publish src/Zeiterfassung.Web/Zeiterfassung.Web.csproj -c Release -nodeReuse:false -p:UseSharedCompilation=false -o /out

# Stufe 2: nur die fertigen statischen Dateien in nginx.
FROM nginx:alpine
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY docker/headers.inc /etc/nginx/headers.inc
COPY --from=build /out/wwwroot/ /usr/share/nginx/html/
EXPOSE 80
