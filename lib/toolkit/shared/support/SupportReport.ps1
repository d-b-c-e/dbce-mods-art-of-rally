# Windows PowerShell 5.1+ host adapter over the exact adjacent SupportReport.cs.
# MIT. No discovery, device, clipboard, browser, network or upload operations.
$script:DbceReportSourcePath = Join-Path $PSScriptRoot 'SupportReport.cs'

function Initialize-DbceSupportReport {
    $source = [IO.File]::ReadAllText($script:DbceReportSourcePath)
    $hash = [Security.Cryptography.SHA256]::Create()
    try { $fingerprint = [BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($source))).Replace('-','') }
    finally { $hash.Dispose() }
    $type = 'Dbce.Wheel.Support.SupportReport' -as [type]
    if ($null -eq $type) {
        # Only assembly metadata is added; the canonical implementation is unchanged.
        $marker = 'namespace Dbce.Wheel.Support'
        if (!$source.Contains($marker)) { throw 'The packaged report source is not recognized.' }
        $attribute = '[assembly: System.Reflection.AssemblyMetadataAttribute("DbceSupportSourceSha256", "' + $fingerprint + '")]' + "`n"
        Add-Type -TypeDefinition ($source.Replace($marker, $attribute + $marker)) -Language CSharp -IgnoreWarnings -ErrorAction Stop
        $type = 'Dbce.Wheel.Support.SupportReport' -as [type]
    }
    $metadata = @($type.Assembly.GetCustomAttributesData() | Where-Object {
        $_.AttributeType.FullName -ceq 'System.Reflection.AssemblyMetadataAttribute' -and
        $_.ConstructorArguments[0].Value -ceq 'DbceSupportSourceSha256'
    })
    if ($metadata.Count -ne 1 -or $metadata[0].ConstructorArguments[1].Value -cne $fingerprint) {
        throw 'A different report component is already loaded. Reopen setup before preparing a report.'
    }
    return $type
}

function Assert-LocalSupportPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or $Path.StartsWith('\\') -or $Path.StartsWith('//')) {
        throw 'Choose a known local text log or local export path.'
    }
}

function Format-SupportRedactedText([AllowEmptyString()][string]$Value, [switch]$SingleLine) {
    $type = Initialize-DbceSupportReport
    $cleaned = $type::Redact($Value, [NullString]::Value)
    if ($SingleLine) { return ([regex]::Replace($cleaned, '\s+', ' ')).Trim() }
    return $cleaned
}

function Read-SupportLogTail([string]$Path, [ValidateRange(256,24000)][int]$MaxBytes = 24000) {
    Assert-LocalSupportPath $Path
    $type = Initialize-DbceSupportReport
    # Core owns link/encoding/binary validation, partial-line discard and bounded IO.
    $content = $type::Redact($type::LogTail($Path, $MaxBytes),[NullString]::Value)
    return [pscustomobject]@{ Included=$true; FileName=[IO.Path]::GetFileName($Path); Content=$content;
        Truncated=$content.StartsWith("[Last $MaxBytes bytes only; partial first line omitted]`n") }
}

function New-PreparedSupportReport(
    [string]$ProductName, [string]$ProductVersion, [string]$Repository, [string]$EntryPoint,
    [string]$Title, [string]$Description, $EnvironmentSummary, $SelectedSummary, [string[]]$LogPaths = @()
) {
    $type = Initialize-DbceSupportReport
    foreach ($path in $LogPaths) { Assert-LocalSupportPath $path }
    $productType = 'Dbce.Wheel.Support.ReportProduct' -as [type]
    $client = if ([string]::IsNullOrWhiteSpace($EntryPoint)) { $null } else { $EntryPoint }
    $product = [Activator]::CreateInstance($productType, [object[]]@($ProductName,$ProductVersion,$Repository,$client,'d-b-c-e/dbce-wheelkit'))
    $details = $Description
    foreach ($pair in @(@('Environment',$EnvironmentSummary),@('Selected diagnostics',$SelectedSummary))) {
        if ($null -eq $pair[1]) { continue }
        $details += "`n`n## $($pair[0])"
        foreach ($key in $pair[1].Keys) { $details += "`n- ${key}: $($pair[1][$key])" }
    }
    # Return Core's immutable snapshot, not a mutable copy of its title/body/preview.
    return $type::Prepare($product,$Title,$details,[string[]]$LogPaths,[NullString]::Value)
}

function Assert-PreparedSupportReport($PreparedReport) {
    $null = Initialize-DbceSupportReport
    $preparedType = 'Dbce.Wheel.Support.PreparedReport' -as [type]
    if ($null -eq $PreparedReport -or $PreparedReport -isnot $preparedType) { throw 'Prepare and review a report snapshot first.' }
}

function New-SupportBrowserSubmission($PreparedReport, [switch]$UserReviewed) {
    Assert-PreparedSupportReport $PreparedReport
    $submission = $PreparedReport.CreateBrowserSubmission([bool]$UserReviewed)
    return [pscustomobject]@{ Url=$submission.Url.AbsoluteUri; BodyIncludedInUrl=!$submission.NeedsAttachment;
        NeedsAttachment=$submission.NeedsAttachment; Notice=$submission.Instructions; IssueBody=$submission.IssueBody }
}

function Save-SupportReviewFile($PreparedReport, [string]$Path, [switch]$UserReviewed) {
    Assert-PreparedSupportReport $PreparedReport
    if (!$UserReviewed) { throw 'Review this report snapshot before exporting it.' }
    Assert-LocalSupportPath $Path
    $PreparedReport.WriteReviewFile($Path)
}
