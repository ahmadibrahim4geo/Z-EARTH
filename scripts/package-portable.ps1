# Compatibility entry point. The older v1.0.0 packaging flow became stale when
# the application reached v2.2.4 and did not generate a downloadable GitHub artifact.
# Run the single, validated self-contained portable preview pipeline instead.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'create-portable-preview.ps1')
if ($LASTEXITCODE -ne 0) { throw "Portable preview packaging failed (exit $LASTEXITCODE)" }
