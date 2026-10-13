param([string]$Python = 'python')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskBuild = Join-Path $taskRoot '.build'
$taskDownloads = Join-Path $taskBuild 'ocr-downloads'
$taskTesseract = Join-Path $taskBuild 'ocr-tesseract'
$taskRuntime = Join-Path $taskBuild 'ocr-runtime'
New-Item -ItemType Directory -Force $taskDownloads | Out-Null
if (!(Test-Path (Join-Path $taskRuntime 'Scripts/python.exe'))) {
    & $Python -m venv $taskRuntime
    if ($LASTEXITCODE) { throw 'Não foi possível criar o ambiente Python do OCR.' }
}
& (Join-Path $taskRuntime 'Scripts/python.exe') -m pip install -r (Join-Path $PSScriptRoot 'ocr-requirements.txt')
if ($LASTEXITCODE) { throw 'Não foi possível instalar os componentes Python do OCR.' }
function Get-OcrFile([string]$Url, [string]$Destination) {
    if (!(Test-Path -LiteralPath $Destination)) { Invoke-WebRequest -Uri $Url -OutFile $Destination }
}
if (!(Test-Path (Join-Path $taskTesseract 'tesseract.exe'))) {
    Get-OcrFile 'https://www.7-zip.org/a/7zr.exe' (Join-Path $taskDownloads '7zr.exe')
    Get-OcrFile 'https://www.7-zip.org/a/7z2501-x64.exe' (Join-Path $taskDownloads '7zip-setup.exe')
    Get-OcrFile 'https://github.com/tesseract-ocr/tesseract/releases/download/5.5.0/tesseract-ocr-w64-setup-5.5.0.20241111.exe' (Join-Path $taskDownloads 'tesseract-setup.exe')
    & (Join-Path $taskDownloads '7zr.exe') x (Join-Path $taskDownloads '7zip-setup.exe') "-o$(Join-Path $taskDownloads '7zip')" -y
    if ($LASTEXITCODE) { throw 'Não foi possível preparar o extrator temporário.' }
    & (Join-Path $taskDownloads '7zip/7z.exe') x (Join-Path $taskDownloads 'tesseract-setup.exe') "-o$taskTesseract" -y
    if ($LASTEXITCODE) { throw 'Não foi possível extrair o Tesseract.' }
}
$taskData = Join-Path $taskTesseract 'tessdata'
New-Item -ItemType Directory -Force $taskData | Out-Null
foreach ($taskLanguage in 'por', 'eng') {
    Get-OcrFile "https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/$taskLanguage.traineddata" (Join-Path $taskData "$taskLanguage.traineddata")
}
& (Join-Path $taskTesseract 'tesseract.exe') --list-langs --tessdata-dir $taskData
if ($LASTEXITCODE) { throw 'O Tesseract não encontrou os modelos de idioma.' }
Write-Output 'OCR local preparado. Nenhum pacote de distribuição foi gerado.'
