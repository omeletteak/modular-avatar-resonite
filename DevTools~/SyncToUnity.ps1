dotnet build Resonite~\ResoniteHook\ResoniteHook.sln --no-restore
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

New-Item -Path ".\ResoPuppet~" -ItemType Directory -Force
Copy-Item -Path "Resonite~\ResoniteHook\ResoPuppetSchema\bin\Debug\*" -Destination ".\Managed" -Recurse -Force
Copy-Item -Path "Resonite~\ResoniteHook\Launcher\bin\Debug\net9.0\*.dll" -Destination ".\ResoPuppet~" -Recurse -Force
Copy-Item -Path "Resonite~\ResoniteHook\Launcher\bin\Debug\net9.0\*.exe" -Destination ".\ResoPuppet~" -Recurse -Force
Copy-Item -Path "Resonite~\ResoniteHook\Launcher\bin\Debug\net9.0\*.pdb" -Destination ".\ResoPuppet~" -Recurse -Force
Copy-Item -Path "Resonite~\ResoniteHook\Launcher\bin\Debug\net9.0\*.json" -Destination ".\ResoPuppet~" -Recurse -Force
Copy-Item -Path "Resonite~\ResoniteHook\Puppeteer\bin\Debug\net9.0\*.dll" -Destination ".\ResoPuppet~" -Recurse -Force
Copy-Item -Path "Resonite~\ResoniteHook\Puppeteer\bin\Debug\net9.0\*.pdb" -Destination ".\ResoPuppet~" -Recurse -Force
