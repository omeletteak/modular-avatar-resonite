dotnet build Resonite~\ResoniteHook\ResoPuppetSchema.csproj
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Copy-Item -Path "Resonite~\ResoniteHook\ResoPuppetSchema\bin\Debug\*" -Destination ".\Managed" -Recurse -Force
