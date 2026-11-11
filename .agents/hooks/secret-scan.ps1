$ErrorActionPreference = 'Stop'

function Emit-Decision {
    param(
        [string]$Decision,
        [string]$Reason = ""
    )
    if ([string]::IsNullOrEmpty($Reason)) {
        @{ decision = $Decision } | ConvertTo-Json -Compress
    } else {
        @{ decision = $Decision; reason = $Reason } | ConvertTo-Json -Compress
    }
    exit 0
}

try {
    $raw = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) {
        Emit-Decision -Decision "allow"
    }

    $payload = $raw | ConvertFrom-Json
    $argsObj = $payload.toolCall.args
    $targetPath = [string]$argsObj.Path

    # Skip scanning the scanner scripts themselves
    if ($targetPath -match 'secret-scan\.(ps1|cmd)$') {
        Emit-Decision -Decision "allow"
    }

    # Block writing directly to sensitive secret files
    if (-not [string]::IsNullOrWhiteSpace($targetPath)) {
        $fileName = [System.IO.Path]::GetFileName($targetPath)
        if (
            ($fileName -match '^\.env(\..+)?$' -and $fileName -notmatch '^\.env\.(example|template|sample)$') -or
            ($fileName -ieq 'secrets.json') -or
            ($fileName -imatch '^appsettings\..*\.local\.json$') -or
            ($fileName -imatch '\.(pem|key|pfx|p12)$')
        ) {
            Emit-Decision -Decision "deny" -Reason "BLOCKED: Writing to sensitive secret file '$fileName' is prohibited. Use dotnet user-secrets or a vault."
        }
    }

    # Combine written/edited text
    $textParts = @()
    if ($null -ne $argsObj.Content) { $textParts += [string]$argsObj.Content }
    if ($null -ne $argsObj.Replacement) { $textParts += [string]$argsObj.Replacement }
    $text = $textParts -join "`n"

    if ([string]::IsNullOrWhiteSpace($text)) {
        Emit-Decision -Decision "allow"
    }

    # Enforce Domain project purity invariant
    if ($targetPath -imatch 'OptiFlow\.Domain\.csproj$' -and $text -imatch '<(PackageReference|ProjectReference)\b') {
        Emit-Decision -Decision "deny" -Reason "BLOCKED: OptiFlow.Domain must not contain PackageReference or ProjectReference entries (Clean Architecture invariant)."
    }

    # High-confidence universal secret patterns (all file types)
    if ($text -match '-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----') {
        Emit-Decision -Decision "deny" -Reason "BLOCKED: Private key material detected. Never write private keys into repository files."
    }
    if ($text -match '\bsk-(?:proj-|live-|ant-)?[A-Za-z0-9_-]{24,}\b') {
        Emit-Decision -Decision "deny" -Reason "BLOCKED: Hardcoded API key (sk-*) detected. Use User Secrets or environment variables."
    }
    if ($text -match '\beyJ[A-Za-z0-9_-]{12,}\.eyJ[A-Za-z0-9_-]{12,}\.[A-Za-z0-9_-]{12,}\b') {
        Emit-Decision -Decision "deny" -Reason "BLOCKED: Hardcoded JWT token detected."
    }
    if ($text -match '\bAKIA[0-9A-Z]{16}\b') {
        Emit-Decision -Decision "deny" -Reason "BLOCKED: Hardcoded AWS access key detected."
    }
    if ($text -match '\b(?:ghp|gho|ghu|ghs|ghr|github_pat)_[A-Za-z0-9_]{20,}\b') {
        Emit-Decision -Decision "deny" -Reason "BLOCKED: Hardcoded GitHub token detected."
    }

    # Code/config-only secret patterns (skip markdown docs/guides)
    $isMarkdown = $targetPath -imatch '\.md$'
    if (-not $isMarkdown) {
        $lines = $text -split "`r?`n"
        foreach ($line in $lines) {
            if ($line -match '^\s*(//|#|<!--|\*)') { continue }
            if ($line -match 'credential_ref|IConfiguration|GetEnvironmentVariable|user-secrets|SecretName|Vault') { continue }

            # Connection string password literal
            if ($line -imatch '(Password|Pwd)\s*=\s*([^;\s"''\}$\{]+)') {
                $val = $Matches[2]
                if ($val -notmatch '^(\*+|<.*>|your[-_].*|changeme|placeholder|example|optiflow_dev)$') {
                    Emit-Decision -Decision "deny" -Reason "BLOCKED: Potential hardcoded database/service password in connection string. Use dotnet user-secrets or env vars."
                }
            }

            # Optimizely / Opal hardcoded key or secret
            if ($line -imatch '(Optimizely|Opal)[^\r\n]{0,40}(ApiKey|ClientSecret|SigningKey|Secret)\s*[:=]\s*["'']([^"''\$\{]{8,})["'']') {
                $val = $Matches[3]
                if ($val -notmatch '^(your[-_].*|placeholder|changeme|test[-_]?only|dummy|example)') {
                    Emit-Decision -Decision "deny" -Reason "BLOCKED: Potential hardcoded Optimizely/Opal secret detected. Use User Secrets or vault references."
                }
            }
        }
    }

    Emit-Decision -Decision "allow"
}
catch {
    # Fail open only if hook payload could not be parsed cleanly, log error to stderr
    Emit-Decision -Decision "allow"
}
