param(
    [Parameter(Mandatory)] [ValidateSet('Backup', 'Restore')] [string] $Action,
    [Parameter(Mandatory)] [string] $File,
    [ValidatePattern('^[a-z][a-z0-9_]{0,62}$')] [string] $NewDatabase
)

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$localFile = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($File)
$containerFile = '/tmp/karate-' + [Guid]::NewGuid().ToString('N') + '.dump'

function Invoke-TournamentCompose {
    docker compose --project-directory $project @args
    if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed (exit $LASTEXITCODE)." }
}

if ($Action -eq 'Backup') {
    if (Test-Path -LiteralPath $localFile) { throw 'Choose a new backup file path.' }
    try {
        Invoke-TournamentCompose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom --no-owner --no-acl --file="$1"' sh $containerFile
        Invoke-TournamentCompose cp "postgres:${containerFile}" $localFile
    }
    finally {
        Invoke-TournamentCompose exec -T postgres rm -f -- $containerFile
    }
    Write-Output "Backup: $localFile"
}
else {
    if (!$NewDatabase) { throw 'Restore requires -NewDatabase with a fresh database name.' }
    if (!(Test-Path -LiteralPath $localFile -PathType Leaf)) { throw 'Backup file does not exist.' }
    try {
        Invoke-TournamentCompose cp $localFile "postgres:${containerFile}"
        Invoke-TournamentCompose exec -T postgres sh -c 'pg_restore --list "$1" >/dev/null' sh $containerFile
        Invoke-TournamentCompose exec -T postgres sh -c 'createdb -U "$POSTGRES_USER" -- "$1"' sh $NewDatabase
        Invoke-TournamentCompose exec -T postgres sh -c 'pg_restore -U "$POSTGRES_USER" -d "$1" --single-transaction --exit-on-error --no-owner --no-acl "$2"' sh $NewDatabase $containerFile
    }
    finally {
        Invoke-TournamentCompose exec -T postgres rm -f -- $containerFile
    }
    Write-Output "Restored into: $NewDatabase"
}
