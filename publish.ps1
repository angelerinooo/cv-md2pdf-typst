param(
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputFolder = "publish"
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path (Join-Path (Join-Path $repoRoot "src") "MdCvConverter") "MdCvConverter.csproj"
$publishDir = Join-Path (Join-Path $repoRoot $OutputFolder) $RuntimeIdentifier
$zipPath = Join-Path $repoRoot "MdCvConverter-$RuntimeIdentifier.zip"
$tarPath = Join-Path $repoRoot "MdCvConverter-$RuntimeIdentifier.tar.gz"
$isWindows = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

Write-Host "Publishing distributable package to: $publishDir"

dotnet publish $projectFile -c $Configuration -r $RuntimeIdentifier --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false -o $publishDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

if (Test-Path $tarPath) {
    Remove-Item $tarPath -Force
}

Write-Host "Creating ZIP/TAR archive for runtime: $RuntimeIdentifier"

if ($isWindows) {
    Write-Host "Creating ZIP package: $zipPath"
    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force
    Write-Host "Package ZIP: $zipPath"
} else {
    if (-not (Get-Command tar -ErrorAction SilentlyContinue)) {
        Write-Error "tar is required to create the distributable package on this platform."
        exit 1
    }

    Write-Host "Creating TAR.GZ package: $tarPath"
    Push-Location $publishDir
    tar -czf $tarPath .
    Pop-Location
    Write-Host "Package TAR.GZ: $tarPath"
}

Write-Host "Publish complete."
Write-Host "Package directory: $publishDir"
if ($isWindows) {
    Write-Host "Package ZIP: $zipPath"
} else {
    Write-Host "Package TAR.GZ: $tarPath"
}
Write-Host "Note: The Typst CLI must still be installed and available on PATH to run the package."