# One finding of a downloaded findings document, for one replay-finding-fixer run. Prints the path of the slice.
param(
    [Parameter(Mandatory)][string]$Findings,
    [Parameter(Mandatory)][string]$Key,
    [Parameter(Mandatory)][string]$OutFile
)

$document = Get-Content $Findings -Raw | ConvertFrom-Json
$finding = $document.findings | Where-Object key -eq $Key
if (-not $finding) { throw "No finding $Key in $Findings" }

$slice = [pscustomobject]@{
    schema     = $document.schema
    hero       = $document.hero
    patch      = $document.patch
    thresholds = $document.thresholds
    fight      = $document.fight
    finding    = $finding
}
$slice | ConvertTo-Json -Depth 20 -Compress | Set-Content -Path $OutFile -Encoding utf8
$OutFile
