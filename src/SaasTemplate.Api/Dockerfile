FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY AccessiShield.Api.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app

# Install Playwright browsers in the build stage where the SDK tools are available
RUN dotnet tool install --global Microsoft.Playwright.CLI \
    && /root/.dotnet/tools/playwright install chromium --with-deps

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy Playwright browser cache from build stage
COPY --from=build /root/.cache/ms-playwright /root/.cache/ms-playwright

# Install Chromium system dependencies
# libXfixes, libXext, libX11 are required by chrome-headless-shell but not
# pulled in transitively by the other X11 libs listed here.
RUN apt-get update && apt-get install -y --no-install-recommends \
    libnss3 libatk1.0-0t64 libatk-bridge2.0-0t64 libcups2t64 libdrm2 \
    libxkbcommon0 libxcomposite1 libxdamage1 libxrandr2 libgbm1 \
    libpango-1.0-0 libcairo2 libasound2t64 libxshmfence1 \
    libxfixes3 libxext6 libx11-6 libxcb1 \
    fonts-liberation fonts-noto-color-emoji \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AccessiShield.Api.dll"]
