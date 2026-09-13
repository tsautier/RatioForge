param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("win-x64", "linux-x64", "osx-x64", "osx-arm64")]
    [string]$Runtime,

    [string]$OutputRoot = "artifacts",

    [switch]$SkipSmokeCheck
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$version = (Get-Content "version.txt" -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "version.txt must contain a SemVer core version. Current value: '$version'"
}

$project = "Source/RatioForge.Desktop/RatioForge.Desktop.csproj"
$baseName = "RatioForge-$version-$Runtime"
$publishDirectory = Join-Path $OutputRoot ".publish/$baseName"
$liteDirectory = Join-Path $OutputRoot ".publish/$baseName-lite"
$extension = if ($Runtime.StartsWith("win-", [StringComparison]::Ordinal)) { ".exe" } else { "" }
$publishedExecutable = Join-Path $publishDirectory "RatioForge$extension"
$publishedLiteExecutable = Join-Path $liteDirectory "RatioForge$extension"
$rawExecutable = Join-Path $OutputRoot "$baseName$extension"
$rawLiteExecutable = Join-Path $OutputRoot "$baseName-lite$extension"

dotnet publish $project --configuration Release --runtime $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false `
    --output $publishDirectory
if ($LASTEXITCODE -ne 0) { throw "Self-contained publish failed for $Runtime." }

dotnet publish $project --configuration Release --runtime $Runtime --self-contained false `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -p:DebugSymbols=false --output $liteDirectory
if ($LASTEXITCODE -ne 0) { throw "Framework-dependent publish failed for $Runtime." }

Get-ChildItem -LiteralPath $publishDirectory -Filter "*.pdb" -File | ForEach-Object {
    Remove-Item -LiteralPath $_.FullName -Force
}
Get-ChildItem -LiteralPath $liteDirectory -Filter "*.pdb" -File | ForEach-Object {
    Remove-Item -LiteralPath $_.FullName -Force
}

foreach ($executable in @($publishedExecutable, $publishedLiteExecutable)) {
    if (!(Test-Path -LiteralPath $executable)) {
        throw "Missing executable: $executable"
    }

    if (!$SkipSmokeCheck) {
        if ($Runtime.StartsWith("win-", [StringComparison]::Ordinal)) {
            $smokeProcess = Start-Process -FilePath $executable -ArgumentList "--smoke-test" `
                -WindowStyle Hidden -Wait -PassThru
            $smokeExitCode = $smokeProcess.ExitCode
        }
        else {
            & $executable --smoke-test
            $smokeExitCode = $LASTEXITCODE
        }

        if ($smokeExitCode -ne 0) {
            throw "Smoke check failed for $executable with exit code $smokeExitCode."
        }
    }
}

$selfContainedBytes = (Get-Item -LiteralPath $publishedExecutable).Length
$liteBytes = (Get-Item -LiteralPath $publishedLiteExecutable).Length
if ($selfContainedBytes -gt 100MB) {
    throw "$publishedExecutable is $selfContainedBytes bytes; expected at most 100 MiB."
}
if ($liteBytes -gt 35MB) {
    throw "$publishedLiteExecutable is $liteBytes bytes; expected at most 35 MiB."
}

Copy-Item -LiteralPath $publishedExecutable -Destination $rawExecutable -Force
Copy-Item -LiteralPath $publishedLiteExecutable -Destination $rawLiteExecutable -Force

if ($Runtime.StartsWith("win-", [StringComparison]::Ordinal)) {
    $archive = Join-Path $OutputRoot "$baseName.zip"
    Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $archive -Force
}
elseif ($Runtime.StartsWith("osx-", [StringComparison]::Ordinal)) {
    $bundleParent = Join-Path $OutputRoot ".bundle/$baseName"
    $bundle = Join-Path $bundleParent "RatioForge.app"
    $contents = Join-Path $bundle "Contents"
    $macOs = Join-Path $contents "MacOS"
    New-Item -ItemType Directory -Path $macOs -Force | Out-Null
    Copy-Item -Path (Join-Path $publishDirectory "*") -Destination $macOs -Recurse -Force
    @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleExecutable</key><string>RatioForge</string>
  <key>CFBundleIdentifier</key><string>io.github.tsautier.ratioforge</string>
  <key>CFBundleName</key><string>RatioForge</string>
  <key>CFBundleShortVersionString</key><string>$version</string>
  <key>CFBundleVersion</key><string>$version</string>
  <key>LSMinimumSystemVersion</key><string>15.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict></plist>
"@ | Set-Content -Encoding utf8 (Join-Path $contents "Info.plist")
    $archive = Join-Path $OutputRoot "$baseName.tar.gz"
    tar -czf $archive -C $bundleParent "RatioForge.app"
    if ($LASTEXITCODE -ne 0) { throw "App bundle archive creation failed for $Runtime." }
}
else {
    $archive = Join-Path $OutputRoot "$baseName.tar.gz"
    tar -czf $archive -C $publishDirectory .
    if ($LASTEXITCODE -ne 0) { throw "Archive creation failed for $Runtime." }
}

$checksumPath = Join-Path $OutputRoot "$baseName.sha256"
$checksums = foreach ($file in @($archive, $rawExecutable, $rawLiteExecutable)) {
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $file
    "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($file))"
}
$checksums | Set-Content -Encoding ascii $checksumPath

Write-Host "Published ${Runtime}: self-contained=$selfContainedBytes bytes, lite=$liteBytes bytes."
