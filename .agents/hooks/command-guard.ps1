$ErrorActionPreference = 'Stop'

function Emit-Decision {
    param([string]$Decision, [string]$Reason = "")
    if ([string]::IsNullOrEmpty($Reason)) {
        @{ decision = $Decision } | ConvertTo-Json -Compress
    } else {
        @{ decision = $Decision; reason = $Reason } | ConvertTo-Json -Compress
    }
    exit 0
}

try {
    $raw = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) { Emit-Decision -Decision "allow" }

    $payload = $raw | ConvertFrom-Json
    $cmd = [string]$payload.toolCall.args.CommandLine

    if ([string]::IsNullOrWhiteSpace($cmd)) { Emit-Decision -Decision "allow" }

    # Never stage .env or secret files
    if ($cmd -imatch 'git\s+add\s+.*(\.env\b|secrets\.json|appsettings\.[^/\\]*\.local\.json)') {
        Emit-Decision -Decision "deny" -Reason "BLOCKED: Refusing to stage secret/env files into git."
    }

    # Confirm destructive commands
    if (
        $cmd -imatch 'dotnet\s+ef\s+database\s+drop' -or
        $cmd -imatch '\b(DROP\s+TABLE|DROP\s+DATABASE|TRUNCATE\s+TABLE)\b' -or
        $cmd -imatch 'git\s+push\s+.*(--force|-f\b)' -or
        $cmd -imatch 'git\s+reset\s+--hard'
    ) {
        Emit-Decision -Decision "ask" -Reason "Destructive database or git command detected. Please confirm before running."
    }

    Emit-Decision -Decision "allow"
}
catch {
    Emit-Decision -Decision "allow"
}
