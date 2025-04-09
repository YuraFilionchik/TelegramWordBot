FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

# Установка русской локали
RUN apt-get update \
    && apt-get install -y --no-install-recommends locales \
    && sed -i '/ru_RU.UTF-8/s/^# //g' /etc/locale.gen \
    && locale-gen ru_RU.UTF-8 \
    && update-locale LANG=ru_RU.UTF-8

ENV LANG=ru_RU.UTF-8
ENV LANGUAGE=ru_RU:ru
ENV LC_ALL=ru_RU.UTF-8
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TelegramWordBot.dll"]