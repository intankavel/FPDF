param([string]$Version = '0.8.1', [string]$GhostscriptRoot = 'C:\Program Files\gs\gs10.08.0')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use uma versão como 0.7.0.' }
$releaseRoot = Join-Path $projectRoot 'artifacts\releases'
$packageRoot = Join-Path $releaseRoot "FPDF-$Version-win-x64"
$archive = "$packageRoot.zip"
if ((Test-Path -LiteralPath $packageRoot) -or (Test-Path -LiteralPath $archive)) { throw 'Já existe um pacote desta versão. Preserve-o ou use outra versão.' }
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
$vendorRoot = Join-Path $projectRoot 'vendor'
$manifest = Get-Content -LiteralPath (Join-Path $vendorRoot 'manifest.json') -Raw | ConvertFrom-Json
foreach ($asset in $manifest.sources) {
    $path = Join-Path (Join-Path $vendorRoot 'downloads') $asset.name
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $asset.sha256) { throw "Fonte alterada ou incompleta: $($asset.name)" }
}
foreach ($asset in $manifest.pdftkBinaries) {
    $path = Join-Path (Join-Path $vendorRoot 'server-official\app\bin') $asset.name
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $asset.sha256) { throw "PDFtk alterado: $($asset.name)" }
}
foreach ($asset in $manifest.ghostscriptBinaries) {
    $path = Join-Path (Join-Path $GhostscriptRoot 'bin') $asset.name
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $asset.sha256) { throw "Ghostscript alterado: $($asset.name)" }
}
& dotnet publish (Join-Path $projectRoot 'src\FPDF.App') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false --source https://api.nuget.org/v3/index.json -o $packageRoot
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar. O pacote não foi compactado.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\LEIA-ME.txt') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\THIRD-PARTY.md') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\BUILD-THIRD-PARTY.md') -Destination $packageRoot
$toolsRoot = Join-Path $packageRoot 'tools'
New-Item -ItemType Directory -Path $toolsRoot -Force | Out-Null
foreach ($asset in $manifest.pdftkBinaries) {
    Copy-Item -LiteralPath (Join-Path (Join-Path $vendorRoot 'server-official\app\bin') $asset.name) -Destination $toolsRoot
}
$gsRoot = Join-Path $toolsRoot 'ghostscript'
New-Item -ItemType Directory -Path (Join-Path $gsRoot 'bin') -Force | Out-Null
foreach ($asset in $manifest.ghostscriptBinaries) {
    Copy-Item -LiteralPath (Join-Path (Join-Path $GhostscriptRoot 'bin') $asset.name) -Destination (Join-Path $gsRoot 'bin')
}
foreach ($name in @('Resource','lib','iccprofiles','doc')) {
    Copy-Item -LiteralPath (Join-Path $GhostscriptRoot $name) -Destination (Join-Path $gsRoot $name) -Recurse
}
$thirdPartySources = Join-Path $packageRoot 'fontes-terceiros'
New-Item -ItemType Directory -Path $thirdPartySources -Force | Out-Null
foreach ($asset in $manifest.sources) {
    Copy-Item -LiteralPath (Join-Path (Join-Path $vendorRoot 'downloads') $asset.name) -Destination $thirdPartySources
}
Copy-Item -LiteralPath (Join-Path $vendorRoot 'manifest.json') -Destination $thirdPartySources
$licenseRoot = Join-Path $packageRoot 'licencas'
New-Item -ItemType Directory -Path $licenseRoot -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $vendorRoot 'server-official\app\license_gpl_pdftk') -Destination (Join-Path $licenseRoot 'pdftk') -Recurse
Copy-Item -LiteralPath (Join-Path $vendorRoot 'notices') -Destination (Join-Path $licenseRoot 'fontes') -Recurse
Copy-Item -LiteralPath (Join-Path $GhostscriptRoot 'doc\COPYING') -Destination (Join-Path $licenseRoot 'ghostscript-AGPL.txt')
$config = Get-Content -LiteralPath (Join-Path $packageRoot 'FPDF.runtimeconfig.json') -Raw | ConvertFrom-Json
$runtimeVersion = ($config.runtimeOptions.includedFrameworks | Where-Object name -eq 'Microsoft.NETCore.App').version
$desktopVersion = ($config.runtimeOptions.includedFrameworks | Where-Object name -eq 'Microsoft.WindowsDesktop.App').version
$nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
$runtimeRoot = Join-Path $nugetRoot "microsoft.netcore.app.runtime.win-x64\$runtimeVersion"
$desktopRoot = Join-Path $nugetRoot "microsoft.windowsdesktop.app.runtime.win-x64\$desktopVersion"
Copy-Item -LiteralPath (Join-Path $runtimeRoot 'LICENSE.TXT') -Destination (Join-Path $licenseRoot 'dotnet-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $runtimeRoot 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $licenseRoot 'dotnet-THIRD-PARTY-NOTICES.txt')
Copy-Item -LiteralPath (Join-Path $desktopRoot 'LICENSE') -Destination (Join-Path $licenseRoot 'windowsdesktop-LICENSE.txt')
$sourceRoot = Join-Path $packageRoot 'codigo-fonte'
New-Item -ItemType Directory -Path $sourceRoot -Force | Out-Null
foreach ($name in @('src', 'tests', 'scripts', 'docs')) {
    $base = Join-Path $projectRoot $name
    foreach ($file in Get-ChildItem -LiteralPath $base -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }) {
        $relative = [IO.Path]::GetRelativePath($projectRoot, $file.FullName)
        $destination = Join-Path $sourceRoot $relative
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
}
foreach ($name in @('FPDF.sln', 'global.json', 'NuGet.Config', 'README.md', 'CHANGELOG.md', 'LICENSE', 'FPDF.ico', '.gitignore')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $sourceRoot
}
New-Item -ItemType Directory -Path (Join-Path $sourceRoot 'vendor') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $vendorRoot 'manifest.json') -Destination (Join-Path $sourceRoot 'vendor')
if (Get-ChildItem -LiteralPath $sourceRoot -Filter '*.pdf' -Recurse) { throw 'O código-fonte contém PDFs inesperados. Revise antes de distribuir.' }
Compress-Archive -LiteralPath $packageRoot -DestinationPath $archive -CompressionLevel Optimal
Get-FileHash -LiteralPath $archive -Algorithm SHA256 | ForEach-Object { "$($_.Hash)  $([IO.Path]::GetFileName($archive))" } | Set-Content -LiteralPath "$archive.sha256" -Encoding utf8
Write-Output "Pacote gerado: $archive"
