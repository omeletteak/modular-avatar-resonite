# -----------------------------------------------------------------------------
# Sync forked MA Resonite changes to a local Unity test project.
#
# This script builds the necessary components and copies them to your
# Unity test project, ensuring the correct files are in the correct places.
#
# USAGE:
# 1. Set the $TestProjectPath variable below to the root of your Unity project.
# 2. Run this script from the root of the modular-avatar-resonite repository.
# -----------------------------------------------------------------------------

# --- CONFIGURATION ---
# !!! IMPORTANT !!!
# Change this path to point to the root directory of your Unity test project.
$TestProjectPath = "C:/Users/omelette_ak/Documents/GitProjects/modular-avatar-resonite-test"
# ---------------------


# --- SCRIPT BODY ---
Write-Host "Starting synchronization to Unity test project: $TestProjectPath" -ForegroundColor Cyan

# Verify test project path exists
if (-not (Test-Path -Path $TestProjectPath -PathType Container)) {
    Write-Error "Test project path not found: $TestProjectPath. Please update the path in this script."
    exit 1
}

# Define destination paths
$PackagePath = "$TestProjectPath/Packages/omelette_ak.nadena.dev.modular-avatar.resonite"
$ManagedPath = "$PackagePath/Managed"
$EditorPath = "$PackagePath/Editor"

if (-not (Test-Path -Path $PackagePath -PathType Container)) {
    Write-Error "Package path not found in test project: $PackagePath. Make sure the package is installed correctly."
    exit 1
}

Write-Host "
Step 1: Building Unity-compatible schema DLL..."

dotnet build "Resonite~/ResoniteHook/ResoPuppetSchema/ResoPuppetSchema.csproj"
if ($LASTEXITCODE -ne 0) { Write-Error "Schema build failed."; exit $LASTEXITCODE }

Write-Host "
Step 2: Building external launcher and other components..."

./DevTools~/SyncToUnity.ps1
if ($LASTEXITCODE -ne 0) { Write-Error "Main build via SyncToUnity.ps1 failed."; exit $LASTEXITCODE }

Write-Host "
Step 3: Cleaning destination 'Managed' folder..."

if (Test-Path $ManagedPath) {
    Get-ChildItem -Path $ManagedPath -Force | Remove-Item -Recurse -Force
    Write-Host "'Managed' folder cleared."
} else {
    New-Item -Path $ManagedPath -ItemType Directory
    Write-Host "'Managed' folder created."
}

Write-Host "
Step 4: Copying clean schema DLLs to 'Managed' folder..."

Copy-Item -Path "Resonite~/ResoniteHook/ResoPuppetSchema/bin/*" -Destination $ManagedPath -Recurse -Force

Write-Host "
Step 5: Copying ResoPuppet~ folder..."

Copy-Item -Path "ResoPuppet~" -Destination $PackagePath -Recurse -Force

Write-Host "
Step 6: Copying modified C# source files..."

Copy-Item -Path "Editor/UI/ResoniteBuildUI.cs" -Destination "$EditorPath/UI/" -Force
Copy-Item -Path "Editor/RPCClientController.cs" -Destination "$EditorPath/" -Force

Write-Host ""
Write-Host "Synchronization complete! You can now switch to the Unity Editor." -ForegroundColor Green
