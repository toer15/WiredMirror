param([switch]$SkipBuild, [switch]$SkipNativeCheck, [string]$AppDirectory = "$PSScriptRoot/outbox/WiredMirror-0.4.0-release")
$ErrorActionPreference = 'Stop'
$version = ([xml](Get-Content -Raw "$PSScriptRoot/Directory.Build.props")).Project.PropertyGroup.Version
$appDirectory = [IO.Path]::GetFullPath($AppDirectory)
if (!$SkipBuild) {
    & "$PSScriptRoot/build.ps1" -OutputDirectory $appDirectory
}
dotnet run --project "$PSScriptRoot/tests/WiredMirror.Tests/WiredMirror.Tests.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw 'Logic tests failed.' }
if (!$SkipNativeCheck) {
    dotnet "$appDirectory/WiredMirror.dll" --check
    if ($LASTEXITCODE -ne 0) { throw 'Native startup check failed.' }
}
& "$appDirectory/Setup/WiredMirror.Setup.exe" --verify-payload
if ($LASTEXITCODE -ne 0) { throw 'Embedded setup payload verification failed.' }

$sourceStage = Join-Path $PSScriptRoot "work/source-$version"
New-Item -ItemType Directory -Force $sourceStage | Out-Null
foreach ($file in @('README.md', 'LICENSE', 'SOURCES.md', 'VALIDATION.md', 'RELEASE_NOTES.md', 'build.ps1', 'package.ps1', 'Directory.Build.props', '.gitignore')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $sourceStage -Force
}
New-Item -ItemType Directory -Force "$sourceStage/.github/workflows" | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot/.github/workflows/release.yml" -Destination "$sourceStage/.github/workflows/release.yml" -Force
foreach ($directory in @('src','tests')) {
    Get-ChildItem (Join-Path $PSScriptRoot $directory) -Recurse -File |
        Where-Object { $_.Extension -in '.cs','.csproj' -and $_.FullName -notmatch '\\(bin|obj)\\' } |
        ForEach-Object {
            $relative = $_.FullName.Substring($PSScriptRoot.Length + 1)
            $destination = Join-Path $sourceStage $relative
            New-Item -ItemType Directory -Force (Split-Path $destination) | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
        }
}
$upstreamSource = Join-Path $PSScriptRoot 'iPhoneMirror-v1.8.3-source.zip'
if (!(Test-Path -LiteralPath $upstreamSource)) {
    $upstreamSource = Join-Path $PSScriptRoot 'work/iPhoneMirror-v1.8.3-source.zip'
    if (!(Test-Path -LiteralPath $upstreamSource)) {
        $revision = git -C "$PSScriptRoot/vendor/iPhoneMirror" rev-parse HEAD
        if ($revision -ne '1270c443a29457751cadd20f6d7bbe1ce213b9db') { throw 'The upstream source checkout is not the pinned revision.' }
        git -C "$PSScriptRoot/vendor/iPhoneMirror" archive --format=zip --prefix=vendor/iPhoneMirror/ "--output=$upstreamSource" HEAD
        if ($LASTEXITCODE -ne 0) { throw 'Upstream source archive failed.' }
    }
}
Copy-Item -LiteralPath $upstreamSource -Destination $sourceStage -Force
Copy-Item -LiteralPath "$PSScriptRoot/README.md","$PSScriptRoot/VALIDATION.md","$PSScriptRoot/SOURCES.md" -Destination $appDirectory -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
$appZip = Join-Path $PSScriptRoot "outbox/WiredMirror-$version-windows-x64.zip"
$sourceZip = Join-Path $PSScriptRoot "outbox/WiredMirror-$version-source.zip"
foreach ($archivePath in @($appZip,$sourceZip)) {
    if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath }
}
[IO.Compression.ZipFile]::CreateFromDirectory($sourceStage, $sourceZip)
$archive = [IO.Compression.ZipFile]::Open($appZip, [IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem $appDirectory -Recurse -File |
        Where-Object { $_.Extension -notin '.log','.pdb' -and $_.Name -notmatch '^(iPhoneMirror.Driver.exe|startup-check.json|wireless-check.json|update-result.txt)$' } |
        ForEach-Object {
            $name = 'WiredMirror/' + $_.FullName.Substring($appDirectory.Length + 1).Replace('\','/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$_.FullName,$name,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
}
finally { $archive.Dispose() }
Get-FileHash -Algorithm SHA256 -LiteralPath $appZip,$sourceZip |
    ForEach-Object { '{0}  {1}' -f $_.Hash,(Split-Path $_.Path -Leaf) } |
    Set-Content -Encoding ASCII -LiteralPath (Join-Path $PSScriptRoot "outbox/WiredMirror-$version-SHA256SUMS.txt")
Get-Item -LiteralPath $appZip,$sourceZip | Select-Object Name,Length
