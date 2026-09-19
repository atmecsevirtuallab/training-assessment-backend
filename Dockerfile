FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["TrainingAndAssessmentWebAPI/TrainingAndAssessmentWebAPI.csproj", "TrainingAndAssessmentWebAPI/"]
COPY ["TrainingAndAssessmentDataAccessLayer/TrainingAndAssessmentDataAccessLayer.csproj", "TrainingAndAssessmentDataAccessLayer/"]
RUN dotnet restore "TrainingAndAssessmentWebAPI/TrainingAndAssessmentWebAPI.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/TrainingAndAssessmentWebAPI"
RUN dotnet build "TrainingAndAssessmentWebAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TrainingAndAssessmentWebAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Production image with GCC, Java JDK, and Python3 pre-installed for Code Execution feature
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app

# Install GCC, Java JDK (javac/java), and Python3 into container
RUN apt-get update && apt-get install -y --no-install-recommends \
    gcc \
    g++ \
    default-jdk \
    python3 \
    && rm -rf /var/lib/apt/lists/*

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TrainingAndAssessmentWebAPI.dll"]
