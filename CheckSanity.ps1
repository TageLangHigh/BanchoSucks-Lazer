param([string]$Root = ".")

# Validates C# source files for code-sanity issues.

# Directories skipped during recursion. osu.Game.Resources is the optional local checkout of the separate g0v0-resources repository.
$ignorePaths = @(".git", "bin", "obj", "Migrations", "packages", "osu.Game.Resources")
$script:hasErrors = $false

$cfsIgnore = Join-Path $Root ".cfsignore"
if (Test-Path $cfsIgnore) {
    $ignorePaths += (Get-Content $cfsIgnore) | Where-Object { $_ -ne "" }
}

function Get-LicenseHeader {
    param([string]$Path)
    while ($true) {
        $license = Get-ChildItem -Path $Path -Filter "*.licenseheader" -File | Select-Object -First 1
        if ($license) { break }
        $parent = Split-Path $Path -Parent
        if (-not $parent) { return $null }
        $Path = $parent
    }

    $started = $false
    $header = ""
    foreach ($line in Get-Content $license.FullName) {
        if ($started) {
            if (-not $line.StartsWith("//")) { break }
            $header += "$line`r`n"
        }
        if ($line -eq "extensions: .cs") {
            $started = $true
        }
    }
    return $header
}

function Get-LineNumber {
    param([string]$Text, [int]$Index)
    ($Text.Substring(0, $Index).ToCharArray() -eq "`n").Count + 1
}

function Find-MatchingLines {
    param(
        [string]$Text,
        [string]$Pattern,
        [System.Text.RegularExpressions.RegexOptions]$Options = [System.Text.RegularExpressions.RegexOptions]::None
    )
    $regex = [regex]::Matches($Text, $Pattern, $Options)
    $regex | ForEach-Object { Get-LineNumber $Text $_.Index }
}

function Report {
    param([string]$File, [string]$Message, [int]$Line = 0)
    Write-Host "$File`:$Line`: $Message"
    $script:hasErrors = $true
}

function CheckFile {
    param([string]$FilePath, [string]$DisplayPath, [string]$LicenseHeader)

    $filename = [System.IO.Path]::GetFileName($FilePath)
    if ($filename -like "*.designer.*") { return }
    if ($filename -eq "AssemblyInfo.cs") { return }

    $text = [System.IO.File]::ReadAllText($FilePath)

    $lines = Find-MatchingLines $text "\r(?!\n)"
    foreach ($line in $lines) { Report $DisplayPath "Incorrect line endings" $line }

    if ($LicenseHeader -and -not $text.StartsWith($LicenseHeader)) {
        Report $DisplayPath "Licence header missing"
    }

    $lines = Find-MatchingLines $text "^((?!///).)* \r\n" ([System.Text.RegularExpressions.RegexOptions]::Multiline)
    foreach ($line in $lines) { Report $DisplayPath "White space needs to be trimmed" $line }

    $lines = Find-MatchingLines $text "`t"
    foreach ($line in $lines) { Report $DisplayPath "Found tab character" $line }

    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($FilePath).Split('.')[0]
    $escapedBaseName = [regex]::Escape($baseName)
    $pattern = "\b(enum|struct|class|interface|record)\s+$escapedBaseName"
    $matches = Find-MatchingLines $text $pattern
    if ($matches.Count -eq 0) {
        Report $DisplayPath "Filename does not match contained type."
    }
}

function CheckDirectory {
    param([string]$Path)
    $dirName = Split-Path $Path -Leaf
    if ($ignorePaths -contains $dirName) { return }

    foreach ($sub in Get-ChildItem -Path $Path -Directory) {
        CheckDirectory (Join-Path $Path $sub.Name)
    }

    $license = Get-LicenseHeader $Path

    foreach ($file in Get-ChildItem -Path $Path -Filter "*.cs" -File) {
        $displayPath = if ($Path -eq $Root) { $file.Name } else { Join-Path $Path $file.Name }
        CheckFile $file.FullName $displayPath $license
    }
}

Push-Location $Root
CheckDirectory "."
Pop-Location

if ($script:hasErrors) { exit 1 }
