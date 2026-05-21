param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

Write-Host "[1/3] Running application use-case tests..."
dotnet test tests/StationApp.Application.Tests/StationApp.Application.Tests.csproj `
    -c $Configuration `
    -m:1 `
    --filter "FullyQualifiedName~WeighingSessionTicketSyncTests"
if ($LASTEXITCODE -ne 0) { throw "Application use-case tests failed." }

Write-Host "[2/3] Running inbound processor tests..."
dotnet test tests/StationApp.Sync.Tests/StationApp.Sync.Tests.csproj `
    -c $Configuration `
    -m:1 `
    --filter "FullyQualifiedName~CutOrderInboundProcessorTests"
if ($LASTEXITCODE -ne 0) { throw "Inbound processor tests failed." }

Write-Host "[3/3] Running SQL Server integration tests..."
dotnet test tests/StationApp.IntegrationTests/StationApp.IntegrationTests.csproj `
    -c $Configuration `
    -m:1 `
    --filter "FullyQualifiedName~ReissueRegistrationCodeIntegrationTests"
if ($LASTEXITCODE -ne 0) { throw "Integration tests failed." }

Write-Host "Reissue/RegistrationCode test suite passed."
