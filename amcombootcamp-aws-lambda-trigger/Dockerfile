FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["amcombootcamp-aws-lambda-trigger.csproj", "amcombootcamp-aws-lambda-trigger/"]
RUN dotnet restore "amcombootcamp-aws-lambda-trigger/amcombootcamp-aws-lambda-trigger.csproj"
COPY . .

FROM build AS publish
RUN dotnet publish "amcombootcamp-aws-lambda-trigger/amcombootcamp-aws-lambda-trigger.csproj" -c Release -o /app/publish

FROM public.ecr.aws/lambda/dotnet:5.0
WORKDIR /var/task

COPY  --from=publish "app/publish"  .
