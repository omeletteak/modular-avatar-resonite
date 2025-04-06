dotnet build Resonite~\ResoniteHook\ResoPuppetSchema\ResoPuppetSchema.csproj --no-restore
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

New-Item -Path ".\ResoPuppet~" -ItemType Directory -Force
Copy-Item -Path "Resonite~\ResoniteHook\ResoPuppetSchema\bin\Debug\*" -Destination ".\Managed" -Recurse -Force
Copy-Item -Path "Resonite~\ResoniteHook\Launcher\bin\Debug\*" -Destination ".\ResoPuppet~" -Recurse -Force
Copy-Item -Path "Resonite~\ResoniteHook\Puppeteer\bin\Debug\*" -Destination ".\ResoPuppet~" -Recurse -Force
