$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
dotnet build (Join-Path $projectRoot 'src\DDMonitor\DDMonitor.csproj') -c Release -o (Join-Path $projectRoot '.runtime\desktop') --verbosity minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
