[CmdletBinding()]
param(
    [ValidateSet('Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',
    [string]$OutputRoot,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$pcAppRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $pcAppRoot 'artifacts'
}
$outputRootFull = [System.IO.Path]::GetFullPath($OutputRoot)
$pcAppPrefix = $pcAppRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $outputRootFull.StartsWith($pcAppPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "发布目录必须位于 pc-app 内：$outputRootFull"
}

$version = 'V3.1'
$solution = Join-Path $pcAppRoot 'SkillExam.sln'
$project = Join-Path $pcAppRoot 'src\SkillExam.App\SkillExam.App.csproj'
$portableDirectory = Join-Path $outputRootFull "portable\SkillExam-$version-win-x64"
$zipPath = Join-Path $outputRootFull "SkillExam-$version-win-x64-portable.zip"

# 发布目录只存放当前便携版，避免旧版本或安装包混入交付物。
if (Test-Path -LiteralPath $outputRootFull) {
    Remove-Item -LiteralPath $outputRootFull -Recurse -Force
}
New-Item -ItemType Directory -Path $portableDirectory -Force | Out-Null

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
$operationGuide = Join-Path $portableDirectory '操作说明.md'
$developmentGuide = Join-Path $portableDirectory '开发文档.md'
Copy-Item -LiteralPath (Join-Path $pcAppRoot '操作说明.md') -Destination $operationGuide
Copy-Item -LiteralPath (Join-Path $pcAppRoot '开发文档.md') -Destination $developmentGuide

foreach ($requiredFile in @($appExecutable, $bundledBank, $operationGuide, $developmentGuide)) {
    if (-not (Test-Path -LiteralPath $requiredFile)) {
        throw "发布缺少必需文件：$requiredFile"
    }
}

Compress-Archive -Path (Join-Path $portableDirectory '*') -DestinationPath $zipPath -CompressionLevel Optimal

[pscustomobject]@{
    Version = $version
    PortableDirectory = $portableDirectory
    PortableZip = $zipPath
}
