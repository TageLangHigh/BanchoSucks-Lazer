#!/bin/bash

chmod +x ./CheckSanity.sh

dotnet tool restore
dotnet jb inspectcode "osu.Desktop.slnf" --no-build --output="inspectcodereport.xml" --caches-home="inspectcode" --verbosity=WARN
dotnet nvika parsereport "inspectcodereport.xml" --treatwarningsaserrors
