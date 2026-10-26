# Read-only Google Earth Pro user-data discovery.
# Does not copy personal My Places, caches, map tiles, credentials or the registry.
# Example:
#   powershell -NoProfile -File scripts/inspect-google-earth-userdata.ps1
# Optional report: -OutputPath "$env:USERPROFILE\Desktop\GoogleEarth-file-inventory.json"
param(
    [string]$OutputPath = ""
)
$ErrorActionPreference = "Stop"
$roots = @(
    @{ Label = "LocalLow (Google Earth support)"; Template = '%USERPROFILE%\AppData\LocalLow\Google\GoogleEarth'; Path = (Join-Path $env:USERPROFILE 'AppData\LocalLow\Google\GoogleEarth') },
    @{ Label = "Local (Google developer documentation)"; Template = '%LOCALAPPDATA%\Google\GoogleEarth'; Path = (Join-Path $env:LOCALAPPDATA 'Google\GoogleEarth') },
    @{ Label = "Program installation"; Template = '%ProgramFiles%\Google\Google Earth Pro\client'; Path = (Join-Path $env:ProgramFiles 'Google\Google Earth Pro\client') }
)
$report = @{
    schema = 'zearth.google-earth-userdata-inventory.v1'
    createdUtc = [DateTime]::UtcNow.ToString('o')
    note = 'Metadata only. No credentials, user data contents, maps, or cache files are copied.'
    directories = @()
}
foreach ($root in $roots) {
    $available = Test-Path -LiteralPath $root.Path -PathType Container
    $entry = @{ label = $root.Label; pathTemplate = $root.Template; exists = [bool]$available; files = @(); subdirectories = @() }
    if ($available) {
        $entry.files = @(
            Get-ChildItem -LiteralPath $root.Path -File -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.Extension -in @('.kml', '.kmz', '.ini', '.cfg', '.json', '.log') } |
            ForEach-Object { @{ name = $_.Name; extension = $_.Extension; sizeBytes = $_.Length } }
        )
        $entry.subdirectories = @(
            Get-ChildItem -LiteralPath $root.Path -Directory -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -notmatch '^(?i:Cache|dbCache|cache2?)$' } |
            ForEach-Object { $_.Name }
        )
    }
    $report.directories += $entry
}
$json = ConvertTo-Json -InputObject $report -Depth 8
if ($OutputPath) {
    $destination = [IO.Path]::GetFullPath($OutputPath)
    $parent = Split-Path -Parent $destination
    if (-not (Test-Path -LiteralPath $parent)) {
        throw "Output directory does not exist. Create it explicitly: $parent"
    }
    [IO.File]::WriteAllText($destination, $json, [Text.UTF8Encoding]::new($false))
    Write-Output "Saved metadata-only report: $destination"
}
else {
    Write-Output $json
}
