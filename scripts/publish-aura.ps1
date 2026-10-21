$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'src\app\Aura.App\Aura.App.csproj'
$publishDirectory = Join-Path $repositoryRoot 'dist\Aura'

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Aura app project was not found: $projectPath"
}

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

$dotnetArguments = @(
    'publish', $projectPath,
    '--configuration', 'Release',
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    '--output', $publishDirectory,
    '-p:Platform=x64',
    '-p:PlatformTarget=x64',
    '-p:WindowsAppSDKSelfContained=true',
    '-p:PublishSingleFile=false'
)

& dotnet @dotnetArguments
if ($LASTEXITCODE -ne 0) {
    throw "Aura publish failed with exit code $LASTEXITCODE"
}

$executablePath = Join-Path $publishDirectory 'Aura.App.exe'
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Publish completed without the expected executable: $executablePath"
}

Write-Output "Published Aura executable: $executablePath"
