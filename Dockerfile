FROM mcr.microsoft.com/dotnet/sdk:7.0-bookworm-slim AS build

WORKDIR /source
COPY ./* .

RUN dotnet restore
RUN dotnet publish -c release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:7.0-bookworm-slim
WORKDIR /app
COPY --from=build /app ./

EXPOSE 80

ENTRYPOINT ["dotnet", "zinfandel_movie_club.dll"]