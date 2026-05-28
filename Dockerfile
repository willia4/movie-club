FROM node:21-bookworm-slim AS node

RUN npm install --global gulp-cli

WORKDIR /source
COPY ./ .

RUN npm install
RUN gulp

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build

WORKDIR /source
COPY ./ .

RUN dotnet restore
RUN dotnet publish -c release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
WORKDIR /app
COPY --from=build /app ./
COPY --from=node /source/wwwroot/lib/bootstrap/ ./wwwroot/lib/bootstrap/

EXPOSE 80

ENTRYPOINT ["dotnet", "zinfandel_movie_club.dll"]