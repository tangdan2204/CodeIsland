$ErrorActionPreference = Stop

$projectRoot = Split-Path -Parent $PSScriptRoot
$releaseDir = Join-Path $projectRoot windows\CodeIsland.Desktop\bin\Release\net8.0-windows\win-x64\publish
$debugDir = Join-Path $projectRoot windows\CodeIsland.Desktop\bin\Debug\net8.0-windows
$appDir = if (Test-Path -LiteralPath (Join-Path $releaseDir CodeIsland.Desktop.exe)) { $releaseDir } else { $debugDir }
$exe = Join-Path $appDir CodeIsland.Desktop.exe

if (-not (Test-Path -LiteralPath $exe)) {
    $bridge = Join-Path $projectRoot windows\CodeIsland.Bridge\CodeIsland.Bridge.csproj
    $project = Join-Path $projectRoot windows\CodeIsland.Desktop\CodeIsland.Desktop.csproj
    dotnet publish $bridge -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true | Out-Host
    dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false | Out-Host
    $bridgeExe = Join-Path $projectRoot windows\CodeIsland.Bridge\bin\Release\net8.0-windows\win-x64\publish\CodeIsland.Bridge.exe
    Copy-Item -LiteralPath $bridgeExe -Destination (Join-Path $releaseDir CodeIsland.Bridge.exe) -Force
    $appDir = $releaseDir
    $exe = Join-Path $appDir CodeIsland.Desktop.exe
}

Start-Process -FilePath $exe -WorkingDirectory $appDir
