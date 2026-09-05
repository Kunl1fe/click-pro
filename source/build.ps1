$ErrorActionPreference = 'Stop'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Khong tim thay compiler .NET Framework 64-bit.' }
$source = Join-Path $PSScriptRoot 'Program.cs'
$manifest = Join-Path $PSScriptRoot 'app.manifest'
$output = Join-Path (Split-Path $PSScriptRoot -Parent) 'LU-Click-Pro.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /win32manifest:$manifest /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /out:$output $source
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host "Built: $output"
