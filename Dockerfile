FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj first (correct path)
COPY NumberGuessingGame/NumberGuessingGame.csproj NumberGuessingGame/
RUN dotnet restore NumberGuessingGame/NumberGuessingGame.csproj

# Copy everything else
COPY . .
WORKDIR /src/NumberGuessingGame
RUN dotnet publish -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "NumberGuessingGame.dll"]
