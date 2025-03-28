dotnet build Resonite~\ResoniteHook\ResoPuppetSchema\ResoPuppetSchema.csproj --no-restore
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Copy-Item -Path "Resonite~\ResoniteHook\ResoPuppetSchema\bin\Debug\*" -Destination ".\Managed" -Recurse -Force
