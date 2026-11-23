#!/bin/bash
set -e

chmod +x ./CheckSanity.sh

dotnet tool restore
# clean is required to ensure all code style errors are (re-)raised by compiler
dotnet clean ./osu.Desktop.slnf --verbosity=q
dotnet build -c Debug -warnaserror ./osu.Desktop.slnf -p:EnforceCodeStyleInBuild=true
./CheckSanity.sh
dotnet jb inspectcode "osu.Desktop.slnf" --no-build --format=Text --stdout --caches-home="inspectcode" --verbosity=WARN
