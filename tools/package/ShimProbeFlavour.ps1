# Which flavour an ArtOfSimRally.Mod.dll is (sana 47008), read from its metadata without loading it: the
# AssemblyMetadata("ArtShimProbe") value the project stamps, and which of the shim probe's types are compiled in.
# A normal build must carry none of them; a probe build all of them. PowerShell 7 (System.Reflection.Metadata).
$script:ShimProbeTypes = @('ArtOfSimRally.Mod.ShimDrive', 'ArtOfSimRally.Mod.Shim.DriveTrial', 'ArtOfSimRally.Mod.Shim.LatchedTrial', 'ArtOfSimRally.Mod.Shim.ProbeNoForce')

function Get-ShimProbeFlavour([Parameter(Mandatory)][string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    try {
        $pe = [Reflection.PortableExecutable.PEReader]::new($stream)
        $md = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        $names = @(foreach ($h in $md.TypeDefinitions) {
            $t = $md.GetTypeDefinition($h); $ns = $md.GetString($t.Namespace)
            if ($ns) { "$ns.$($md.GetString($t.Name))" } else { $md.GetString($t.Name) } })
        $value = $null
        foreach ($h in $md.GetAssemblyDefinition().GetCustomAttributes()) {
            $a = $md.GetCustomAttribute($h)
            if ($a.Constructor.Kind -ne [Reflection.Metadata.HandleKind]::MemberReference) { continue }
            $ctor = $md.GetMemberReference([Reflection.Metadata.MemberReferenceHandle]$a.Constructor)
            if ($ctor.Parent.Kind -ne [Reflection.Metadata.HandleKind]::TypeReference) { continue }
            if ($md.GetString($md.GetTypeReference([Reflection.Metadata.TypeReferenceHandle]$ctor.Parent).Name) -ne 'AssemblyMetadataAttribute') { continue }
            $blob = $md.GetBlobBytes($a.Value)
            # Custom attribute blob: prolog 0x0001, then the key and value as SerStrings (compressed length, UTF-8).
            $at = 2; $pair = @()
            for ($k = 0; $k -lt 2; $k++) {
                $b = $blob[$at]
                if ($b -eq 0xFF) { $pair += $null; $at++; continue }
                if (($b -band 0x80) -eq 0) { $len = $b; $at += 1 } elseif (($b -band 0xC0) -eq 0x80) { $len = (($b -band 0x3F) -shl 8) -bor $blob[$at + 1]; $at += 2 }
                else { $len = (($b -band 0x1F) -shl 24) -bor ($blob[$at + 1] -shl 16) -bor ($blob[$at + 2] -shl 8) -bor $blob[$at + 3]; $at += 4 }
                $pair += [Text.Encoding]::UTF8.GetString($blob, $at, $len); $at += $len
            }
            if ($pair[0] -eq 'ArtShimProbe') { $value = $pair[1] }
        }
        [pscustomobject]@{ Metadata = $value; ProbeTypes = @($script:ShimProbeTypes | Where-Object { $names -contains $_ }); ShimStartup = ($names -contains 'ArtOfSimRally.Mod.ShimStartup') }
    } finally { $stream.Dispose() }
}

# Throws unless the assembly is exactly the requested flavour: its stamp, and all or none of the probe's types.
function Assert-ShimProbeFlavour([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][bool]$Probe) {
    $f = Get-ShimProbeFlavour $Path
    $want = if ($Probe) { 'true' } else { 'false' }
    $types = if ($Probe) { $script:ShimProbeTypes.Count } else { 0 }
    if ($f.Metadata -ne $want -or $f.ProbeTypes.Count -ne $types -or -not $f.ShimStartup) {
        throw "ArtOfSimRally.Mod.dll is not the $(if ($Probe) { 'probe' } else { 'normal' }) flavour: ArtShimProbe=$($f.Metadata), probe types [$($f.ProbeTypes -join ', ')]"
    }
    return $f
}
