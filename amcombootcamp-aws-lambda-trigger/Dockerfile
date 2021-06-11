FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["amcombootcamp-aws-lambda-trigger.csproj", "amcombootcamp-aws-lambda-trigger/"]
RUN dotnet restore "amcombootcamp-aws-lambda-trigger/amcombootcamp-aws-lambda-trigger.csproj"
COPY . .

FROM build AS publish
RUN dotnet publish -c Release /p:GenerateRuntimeConfigurationFiles=true --runtime linux-x64 --self-contained false -o /app

FROM public.ecr.aws/lambda/dotnet:5.0 as final
WORKDIR /var/task

COPY  --from=publish "app/"  .