$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$repoDir = Split-Path $PSScriptRoot -Parent
$distDir = Join-Path $repoDir 'dist'
New-Item -ItemType Directory -Path $distDir -Force | Out-Null
$files = @((Join-Path $repoDir 'LU-Click-Pro.exe'), (Join-Path $repoDir 'DOC-TRUOC.txt'))
$zipPath = Join-Path $distDir 'LU-Click-Pro-Windows-x64.zip'
Compress-Archive -LiteralPath $files -DestinationPath $zipPath -Force
Write-Host "Packaged: $zipPath"
