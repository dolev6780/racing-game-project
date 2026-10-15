#!/bin/bash
# Compile-checks every game script against Unity's API without needing the Unity editor.
#
# Requires the .NET SDK (8+). Downloads Unity 2021.3 reference assemblies from NuGet on first run,
# maps the few Unity 6 API renames back (linearVelocity -> velocity, PhysicsMaterial -> PhysicMaterial, ...)
# on a temporary copy, and builds three passes:
#   legacy      - old Input Manager
#   inputsystem - new Input System (ENABLE_INPUT_SYSTEM, using Stubs/InputSystemStubs.cs)
#   editor      - editor scripts too (UNITY_EDITOR, using Stubs/EditorStubs.cs)
#   tests       - play mode tests in Assets/Tests (NUnit from NuGet, Stubs/TestToolsStubs.cs)
# The project files in Assets/ are never modified. Exit code is non-zero if any pass fails.
#
# Usage: Tools/CompileCheck/check.sh
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
WORK="$HERE/.work"
REF="$WORK/refs"
mkdir -p "$REF"

fetch() { # id version dir
  if [ ! -f "$REF/$3/.done" ]; then
    rm -rf "$REF/$3"
    curl -sfL -m 300 -o "$REF/$3.nupkg" "https://api.nuget.org/v3-flatcontainer/$1/$2/$1.$2.nupkg" || { echo "download failed: $1"; exit 2; }
    mkdir -p "$REF/$3"
    # nupkg files are zip archives; use Python so no unzip tool is required
    python3 -c "import zipfile,sys; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])" "$REF/$3.nupkg" "$REF/$3" \
      || { echo "extract failed: $1"; exit 2; }
    touch "$REF/$3/.done"
  fi
}
fetch unityengine.modules 2021.3.33 modules
fetch unity3d.unityengine.ui 2020.3.21 ui
fetch nunit 3.13.3 nunit

rm -rf "$WORK/src" && cp -r "$ROOT/Assets/Scripts" "$WORK/src" && cp -r "$ROOT/Assets/Tests" "$WORK/src/_Tests"
find "$WORK/src" -name "*.cs" -exec sed -i \
  -e 's/\.linearVelocity/.velocity/g' \
  -e 's/linearDamping/drag/g' \
  -e 's/angularDamping/angularDrag/g' \
  -e 's/PhysicsMaterialCombine/PhysicMaterialCombine/g' \
  -e 's/\bPhysicsMaterial\b/PhysicMaterial/g' {} +

shopt -s nullglob
MODULES=("$REF"/modules/lib/netstandard2.0/*.dll)
if [ ${#MODULES[@]} -lt 20 ] || [ ! -f "$REF/ui/lib/UnityEngine.UI.dll" ]; then
  echo "Unity reference assemblies missing (found ${#MODULES[@]} modules) - delete $WORK and retry"; exit 2
fi
echo "Using ${#MODULES[@]} Unity modules, .NET SDK $(dotnet --version)"
REFS=""
for f in "$REF"/modules/lib/netstandard2.0/*.dll "$REF"/ui/lib/UnityEngine.UI.dll; do
  REFS="$REFS<Reference Include=\"$(basename "$f" .dll)\"><HintPath>$f</HintPath></Reference>"
done

status=0
for pass in legacy inputsystem editor tests; do
  case $pass in
    legacy) DEF=""; EXCLUDE='Exclude="../src/Editor/**;../src/_Tests/**"';;
    inputsystem) DEF="ENABLE_INPUT_SYSTEM"; EXCLUDE='Exclude="../src/Editor/**;../src/_Tests/**"';;
    editor) DEF="UNITY_EDITOR;ENABLE_INPUT_SYSTEM"; EXCLUDE='Exclude="../src/_Tests/**"';;
    tests) DEF="UNITY_INCLUDE_TESTS;ENABLE_INPUT_SYSTEM"; EXCLUDE='Exclude="../src/Editor/**"';;
  esac
  EXTRA=""; [ $pass = tests ] && EXTRA="<Reference Include=\"nunit.framework\"><HintPath>$REF/nunit/lib/netstandard2.0/nunit.framework.dll</HintPath></Reference>"
  mkdir -p "$WORK/$pass"
  cat > "$WORK/$pass/Check.csproj" <<PROJ
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <DefineConstants>$DEF</DefineConstants>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>CS0649;CS0414;CS0169;CS1701;CS1702</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="../src/**/*.cs" $EXCLUDE />
    <Compile Include="$HERE/Stubs/*.cs" />
  </ItemGroup>
  <ItemGroup>$REFS$EXTRA</ItemGroup>
</Project>
PROJ
  out=$(dotnet build "$WORK/$pass/Check.csproj" -nologo -v q -clp:NoSummary 2>&1)
  errors=$(echo "$out" | grep -E "error|warning CS|warning MSB" | sed -E 's|.*/src/|Assets/Scripts/|; s| \[.*||' | sort -u)
  if [ -z "$errors" ]; then echo "PASS  $pass"; else echo "FAIL  $pass"; echo "$errors" | head -40; status=1; fi
done
exit $status
