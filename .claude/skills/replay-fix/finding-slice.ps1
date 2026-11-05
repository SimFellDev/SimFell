# One finding of a hero's findings document with its evidence from the fights most at stake, for one replay-finding-fixer
# run. Prints the path of the slice.
param(
    [Parameter(Mandatory)][string]$Hero,
    [Parameter(Mandatory)][string]$Label,
    [Parameter(Mandatory)][string]$Key,
    [Parameter(Mandatory)][string]$OutFile,
    [int]$Fights = 3
)

$document = Get-Content "data/accuracy/$Hero/$Label.findings.json" -Raw | ConvertFrom-Json
$finding = $document.findings | Where-Object key -eq $Key
if (-not $finding) { throw "No finding $Key in data/accuracy/$Hero/$Label.findings.json" }

$evidence = foreach ($row in @($finding.byFight) | Select-Object -First $Fights) {
    $fight = $document.fights | Where-Object review -eq $row.review
    $own = (Get-Content $fight.files.findings -Raw | ConvertFrom-Json).findings | Where-Object key -eq $Key
    # Every measure rewrites the fight documents, so they can belong to a later engine than the label.
    if (-not $own -or $own.issues -ne $row.issues) {
        throw "$($fight.files.findings) is not from label $Label. Run the measure with ACCURACY_LABEL=$Label on the accepted code, then slice again."
    }
    [pscustomobject]@{ review = $row.review; files = $fight.files; finding = $own }
}

$slice = [pscustomobject]@{
    schema     = $document.schema
    hero       = $document.hero
    label      = $document.label
    patch      = $document.patch
    thresholds = $document.thresholds
    fights     = $document.totals.fights
    finding    = $finding
    evidence   = @($evidence)
}
$slice | ConvertTo-Json -Depth 20 -Compress | Set-Content -Path $OutFile -Encoding utf8
$OutFile
