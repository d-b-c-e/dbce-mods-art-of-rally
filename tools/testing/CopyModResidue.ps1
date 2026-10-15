# Save-bindings residue in the owned copy's mod folder (sana 132976). The host's Save replaces dbce-controls.ini with
# File.Replace, which keeps the prior profile as dbce-controls.ini.bindings-backup-<guid> beside it. The runner records
# the backups present before launch (path and hash). After the run it archives, then removes, only the backups created
# during it; every earlier backup must still be there with the same hash, or restoration fails. Nothing else is touched.
function Get-BindingBackups([string]$ModDir) {
    @(Get-ChildItem -LiteralPath $ModDir -File -Filter 'dbce-controls.ini.bindings-backup-*' |
        Sort-Object Name | ForEach-Object { [pscustomobject]@{ path = $_.FullName; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
}

function Move-NewBindingBackups([string]$ModDir, [object[]]$Before, [string]$Archive) {
    $Before = @($Before | Where-Object { $null -ne $_ })
    $known = @($Before | ForEach-Object { $_.path })
    foreach ($old in @($Before)) {
        if (!(Test-Path -LiteralPath $old.path -PathType Leaf) -or (Get-FileHash -LiteralPath $old.path -Algorithm SHA256).Hash -ne $old.sha256) {
            throw "A binding backup that existed before the run changed or vanished: $($old.path)"
        }
    }
    $moved = [Collections.Generic.List[object]]::new()
    foreach ($new in @(Get-BindingBackups $ModDir | Where-Object { $_.path -notin $known })) {
        New-Item -ItemType Directory -Path $Archive -Force | Out-Null
        $keep = Join-Path $Archive ([IO.Path]::GetFileName($new.path))
        if (Test-Path -LiteralPath $keep) { throw "Archive already holds $keep" }
        Copy-Item -LiteralPath $new.path -Destination $keep
        if ((Get-FileHash -LiteralPath $keep -Algorithm SHA256).Hash -ne $new.sha256) { throw "Archived copy differs: $keep" }
        Remove-Item -LiteralPath $new.path
        $moved.Add([pscustomobject]@{ name = [IO.Path]::GetFileName($new.path); sha256 = $new.sha256 })
    }
    return ,($moved.ToArray())
}
