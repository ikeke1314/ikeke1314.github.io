[CmdletBinding()]
param(
    [ValidateSet('Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',
    [string]$OutputRoot,
    [string]$InnoCompiler,
    [switch]$SkipTests,
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$pcAppRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $pcAppRoot 'artifacts'
}
$outputRootFull = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $outputRootFull.StartsWith($pcAppRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "发布目录必须位于 pc-app 内：$outputRootFull"
}

$solution = Join-Path $pcAppRoot 'SkillExam.sln'
$project = Join-Path $pcAppRoot 'src\SkillExam.App\SkillExam.App.csproj'
$portableDirectory = Join-Path $outputRootFull 'portable\SkillExam-V3.0-win-x64'
$installerDirectory = Join-Path $outputRootFull 'installer'
$zipPath = Join-Path $outputRootFull 'SkillExam-V3.0-win-x64-portable.zip'
$installerScript = Join-Path $pcAppRoot 'installer\SkillExam.iss'

foreach ($directory in @($portableDirectory, $installerDirectory)) {
    $resolved = [System.IO.Path]::GetFullPath($directory)
    if (-not $resolved.StartsWith($outputRootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "拒绝清理发布目录之外的路径：$resolved"
    }
    if (Test-Path -LiteralPath $resolved) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
    New-Item -ItemType Directory -Path $resolved -Force | Out-Null
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Push-Location $pcAppRoot
try {
    dotnet restore $solution
    if (-not $SkipTests) {
        dotnet test $solution --configuration $Configuration --no-restore --verbosity minimal
    }
    dotnet publish $project `
        --configuration $Configuration `
        --runtime $Runtime `
        --self-contained true `
        --no-restore `
        --output $portableDirectory `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishTrimmed=false `
        -p:DebugType=none `
        -p:DebugSymbols=false
}
finally {
    Pop-Location
}

$appExecutable = Join-Path $portableDirectory '技能士考试刷题系统.exe'
$bundledBank = Join-Path $portableDirectory 'exam_bank\PE 技能士题库.xlsx'
foreach ($requiredFile in @($appExecutable, $bundledBank)) {
    if (-not (Test-Path -LiteralPath $requiredFile)) {
        throw "发布缺少必需文件：$requiredFile"
    }
}

Compress-Archive -Path (Join-Path $portableDirectory '*') -DestinationPath $zipPath -CompressionLevel Optimal

if (-not $SkipInstaller) {
    $compilerCandidates = @(
        $InnoCompiler,
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique
    $compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $compiler) {
        throw '未找到 Inno Setup 6 的 ISCC.exe。请安装后重试，或使用 -SkipInstaller 仅生成便携版。'
    }

    & $compiler "/DSourceDir=$portableDirectory" "/DOutputDir=$installerDirectory" $installerScript
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup 编译失败，退出码：$LASTEXITCODE"
    }
}

[pscustomobject]@{
    Version = 'V3.0'
    PortableDirectory = $portableDirectory
    PortableZip = $zipPath
    InstallerDirectory = if ($SkipInstaller) { $null } else { $installerDirectory }
}
