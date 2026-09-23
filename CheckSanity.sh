#!/usr/bin/env bash
# Validates C# source files for code-sanity issues.

set -uo pipefail

root="${1:-.}"
has_errors=0

# Directories skipped during recursion. osu.Game.Resources is the optional local checkout of the separate g0v0-resources repository.
ignore_paths=(".git" "bin" "obj" "Migrations" "packages" "osu.Game.Resources")

if [[ -f "$root/.cfsignore" ]]; then
    while IFS= read -r line || [[ -n "$line" ]]; do
        [[ -n "$line" ]] && ignore_paths+=("$line")
    done < "$root/.cfsignore"
fi

# Search upward from the given directory for the nearest *.licenseheader file and
# return the header block for the .cs extension, with CRLF line endings.
get_license_header() {
    local path="$1"
    while true; do
        local file
        file=$(find "$path" -maxdepth 1 -type f -name "*.licenseheader" -print -quit)
        [[ -n "$file" ]] && break

        local parent
        parent=$(dirname "$path")
        [[ "$parent" == "$path" ]] && return
        path="$parent"
    done

    perl -0777 -ne '
        if (/extensions: \.cs\s*\r?\n(.*)/s) {
            my $rest = $1;
            for my $line (split /\r?\n/, $rest) {
                last unless $line =~ /^\/\//;
                print $line, "\r\n";
            }
        }
    ' "$file"
}

# Check a single .cs file for the four rules: line endings, license header,
# trailing whitespace, tabs, and filename/type name match.
check_file() {
    local file="$1"
    local dir filename basename_first
    dir=$(dirname "$file")
    filename=$(basename "$file")
    basename_first="${filename%.cs}"
    basename_first="${basename_first%%.*}"

    # Skip designer and AssemblyInfo files.
    [[ "$filename" == *.designer.* ]] && return
    [[ "$filename" == "AssemblyInfo.cs" ]] && return

    local license_header
    license_header=$(get_license_header "$dir")

    env LICENSE_HEADER="$license_header" perl -e '
        my $file = $ARGV[0];
        my $basename = $ARGV[1];
        my $license = $ENV{LICENSE_HEADER};
        my $has_errors = 0;

        local $/;
        open my $fh, "<", $file or die $!;
        my $text = <$fh>;

        while ($text =~ /\r(?!\n)/g) {
            my $line = ($` =~ tr/\n//) + 1;
            print "$file:$line: Incorrect line endings\n";
            $has_errors = 1;
        }

        if (length($license) > 0 && index($text, $license) != 0) {
            print "$file:0: Licence header missing\n";
            $has_errors = 1;
        }

        while ($text =~ /^(?!.*\/\/\/).* \r\n/gm) {
            my $line = ($` =~ tr/\n//) + 1;
            print "$file:$line: White space needs to be trimmed\n";
            $has_errors = 1;
        }

        while ($text =~ /\t/g) {
            my $line = ($` =~ tr/\n//) + 1;
            print "$file:$line: Found tab character\n";
            $has_errors = 1;
        }

        my $escaped = quotemeta($basename);
        if ($text !~ /\b(enum|struct|class|interface|record)\s+$escaped/) {
            print "$file:0: Filename does not match contained type.\n";
            $has_errors = 1;
        }

        exit $has_errors;
    ' "$file" "$basename_first"

    if [[ $? -ne 0 ]]; then
        has_errors=1
    fi
}

# Build a find expression that prunes the ignored directories.
find_args=("$root")
for p in "${ignore_paths[@]}"; do
    find_args+=(-path "*/$p" -prune -o)
done
find_args+=(-type f -iname "*.cs" -print0)

while IFS= read -r -d "" file; do
    check_file "$file"
done < <(find "${find_args[@]}")

if [[ $has_errors -ne 0 ]]; then
    exit 1
fi
