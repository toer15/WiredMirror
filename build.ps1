param([string]$OutputDirectory = "$PSScriptRoot/outbox/WiredMirror-0.4.0")
$ErrorActionPreference = 'Stop'
$downloadDirectory = Join-Path $PSScriptRoot 'work/downloads'
New-Item -ItemType Directory -Force $downloadDirectory,$OutputDirectory | Out-Null
$archivePath = Join-Path $downloadDirectory 'iPhoneMirror-v1.8.3-win-x64.zip'
$expectedHash = '97c5ded39a9437069cb24fd4ee59fe9775c1ebcf2700e3fd6305c9e424396bf0'
if (!(Test-Path -LiteralPath $archivePath)) {
    Invoke-WebRequest -UseBasicParsing 'https://github.com/RayrenSX/iPhoneMirror/releases/download/v1.8.3/iPhoneMirror-v1.8.3-win-x64.zip' -OutFile $archivePath
}
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'USB engine release checksum mismatch. The archive was not used.'
}
if (!(Test-Path -LiteralPath "$PSScriptRoot/vendor/iPhoneMirror/src/DriverInstaller/Services/DriverPayload.cs")) {
    $sourceZip = Join-Path $PSScriptRoot 'iPhoneMirror-v1.8.3-source.zip'
    if (!(Test-Path -LiteralPath $sourceZip) -or
        (Get-FileHash -LiteralPath $sourceZip -Algorithm SHA256).Hash -ne '79E480C5CA753BB6441DED5F8D72922C15B51B4B1FE6F31D4E6A93A6257D1200') {
        throw 'The pinned upstream source archive is missing or its checksum does not match.'
    }
    Expand-Archive -LiteralPath $sourceZip -DestinationPath $PSScriptRoot -Force
}
$setupDirectory = Join-Path $OutputDirectory 'Setup'
dotnet publish "$PSScriptRoot/src/WiredMirror.Setup/WiredMirror.Setup.csproj" -c Release --self-contained false -o $setupDirectory
if ($LASTEXITCODE -ne 0) { throw 'USB setup helper build failed.' }
$setupExecutable = Join-Path $setupDirectory 'WiredMirror.Setup.exe'
(Get-FileHash -LiteralPath $setupExecutable -Algorithm SHA256).Hash |
    Set-Content -Encoding ASCII -LiteralPath ($setupExecutable + '.sha256')
dotnet publish "$PSScriptRoot/src/WiredMirror/WiredMirror.csproj" -c Release -r win-x64 --self-contained false -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
dotnet publish "$PSScriptRoot/src/WiredMirror.Update/WiredMirror.Update.csproj" -c Release --self-contained false -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Update helper build failed.' }
'WiredMirror portable installation v1' | Set-Content -Encoding ASCII -LiteralPath (Join-Path $OutputDirectory '.wiredmirror-install')

# Extract native USB and AirPlay components. The app's setup helper handles installation.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$releaseArchive = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $files = @('iPhoneMirror.Core.dll',
        'iPhoneMirror.UsbConfigurationSwitch.exe', 'libusb-1.0.dll', 'libusb0.dll',
        'msvcp140.dll', 'vcruntime140.dll', 'vcruntime140_1.dll',
        'THIRD_PARTY_NOTICES.md', 'DRIVER_DEPENDENCIES.md')
    foreach ($entry in $releaseArchive.Entries) {
        $relative = $entry.FullName -replace '^iPhoneMirror-v1\.8\.3-win-x64/', ''
        $wirelessComponent = $relative -like 'Wireless/*' -and $relative -notlike 'Wireless/UxPlay/*'
        if (!$entry.Name -or ($relative -notin $files -and $relative -notlike 'licenses/*' -and !$wirelessComponent)) { continue }
        $destination = Join-Path $OutputDirectory $relative
        New-Item -ItemType Directory -Force (Split-Path $destination) | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination, $true)
    }
}
finally { $releaseArchive.Dispose() }
Copy-Item -LiteralPath "$PSScriptRoot/README.md","$PSScriptRoot/LICENSE","$PSScriptRoot/SOURCES.md","$PSScriptRoot/VALIDATION.md" -Destination $OutputDirectory -Force
Write-Output "Built: $OutputDirectory/WiredMirror.exe"
