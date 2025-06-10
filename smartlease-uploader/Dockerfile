FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app



FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
RUN apt-get update && apt-get install -y tzdata
ENV TZ=Europe/Paris
COPY --from=build /app .

CMD ["dotnet", "SmartleaseUploader.dll"]
