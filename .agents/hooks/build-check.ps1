$ErrorActionPreference = 'SilentlyContinue'

try {
    $raw = [Console]::In.ReadToEnd()
    $targetPath = ""
    if (-not [string]::IsNullOrWhiteSpace($raw)) {
        $payload = $raw | ConvertFrom-Json
        $targetPath = [string]$payload.toolCall.args.Path
    }

    # Only build when C# / .NET project/solution files are modified
    if ($targetPath -notmatch '\.(cs|csproj|props|sln)$') {
        Write-Output "{}"
        exit 0
    }

    $slnDir = "f:\coding\_LinkdingProject\Episod2_First\OptiFlow"
    if (-not (Test-Path (Join-Path $slnDir "OptiFlow.sln"))) {
        Write-Output "{}"
        exit 0
    }

    $logFile = Join-Path $PSScriptRoot "last-build.log"
    $output = & dotnet build (Join-Path $slnDir "OptiFlow.sln") --no-restore --verbosity quiet 2>&1
    $exitCode = $LASTEXITCODE

    $timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    $statusLine = "[$timestamp] ExitCode=$exitCode Target=$targetPath"
    @($statusLine) + $output | Set-Content -Path $logFile -Encoding UTF8
}
catch {
}

Write-Output "{}"
exit 0
