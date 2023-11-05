FROM node:21-bookworm-slim as node

RUN npm install --global gulp-cli

WORKDIR /source
COPY ./ .

RUN npm install
RUN gulp

FROM mcr.microsoft.com/dotnet/sdk:7.0-bookworm-slim AS build

WORKDIR /source
COPY ./ .

RUN dotnet restore
RUN dotnet publish -c release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:7.0-bookworm-slim
WORKDIR /app
COPY --from=build /app ./
COPY --from=node /source/wwwroot/lib/bootstrap/ ./wwwroot/lib/bootstrap/

EXPOSE 80

ENTRYPOINT ["dotnet", "zinfandel_movie_club.dll"]