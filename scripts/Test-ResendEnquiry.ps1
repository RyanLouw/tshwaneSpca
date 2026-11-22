param(
    [Parameter(Mandatory = $true)]
    [string] $TestInbox
)

$ErrorActionPreference = 'Stop'
$origin = 'https://localhost:5443'
$dataDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('tshwane-resend-' + [Guid]::NewGuid().ToString('N'))
$settings = @{
    'ASPNETCORE_ENVIRONMENT' = 'Development'
    'DOTNET_ENVIRONMENT' = 'Development'
    'TSHWANE_DATA_DIR' = $dataDirectory
    'Mail__Mode' = 'Resend'
    'Mail__PublicUrl' = $origin
    'Mail__From' = 'onboarding@resend.dev'
    'Mail__ResendApiKey' = $null
    'Mail__GeneralTo' = $TestInbox
    'Mail__VolunteerTo' = $TestInbox
    'Mail__DonationsTo' = $TestInbox
    'Mail__SponsorshipTo' = $TestInbox
    'Mail__WaltlooTo' = $TestInbox
    'Mail__CenturionTo' = $TestInbox
    'Mail__WaltlooClinicTo' = $TestInbox
    'Mail__CenturionClinicTo' = $TestInbox
    'Mail__VolunteerApplicationsTo' = $TestInbox
}

$previous = @{}
foreach ($name in $settings.Keys) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

Push-Location (Join-Path $PSScriptRoot '..')
try {
    & dotnet dev-certs https --trust
    if ($LASTEXITCODE -ne 0) { throw 'Could not trust the local .NET HTTPS certificate.' }

    foreach ($name in $settings.Keys) {
        [Environment]::SetEnvironmentVariable($name, $settings[$name], 'Process')
    }

    & dotnet run --project TshwaneSPCA.MigrationService
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the test database.' }

    $key = Read-Host 'Resend API key' -AsSecureString
    try {
        if ($key.Length -eq 0) { throw 'Enter a Resend API key.' }
        $env:Mail__ResendApiKey = [System.Net.NetworkCredential]::new('', $key).Password
    }
    finally {
        $key.Dispose()
    }

    Write-Host "Open $origin/enquiries/general and submit an enquiry with sample information."
    Write-Host "Check your test inbox and the Resend dashboard for delivery. Press Ctrl+C when done."
    & dotnet run --project TshwaneSPCA.Web --no-launch-profile -- --urls $origin '--Staff:InitialAdminEmail=' '--Mail:VolunteerApplicationsCc='
    if ($LASTEXITCODE -notin 0, 130, -1073741510) { throw 'The website exited with an error.' }
}
finally {
    foreach ($name in $previous.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process')
    }
    if (Test-Path -LiteralPath $dataDirectory -PathType Container) {
        Remove-Item -LiteralPath $dataDirectory -Recurse -Force
    }
    Pop-Location
}
