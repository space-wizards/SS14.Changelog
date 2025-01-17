FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
RUN apt-get update && apt-get install -y git python3 python3-yaml
RUN apt-get clean
RUN mkdir /repo && chown $APP_UID /repo
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SS14.Changelog/SS14.Changelog.csproj", "SS14.Changelog/"]
RUN dotnet restore "SS14.Changelog/SS14.Changelog.csproj"
COPY . .
WORKDIR "/src/SS14.Changelog"
RUN dotnet build "SS14.Changelog.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "SS14.Changelog.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SS14.Changelog.dll"]
VOLUME /repo
ENV Changelog__ChangelogRepo=/repo
ENV Changelog__SshKey=/key
RUN mkdir ~/.ssh && chmod 700 ~/.ssh
COPY "Publish/github_known_hosts" /home/app/.ssh/known_hosts
