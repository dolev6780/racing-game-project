#!/bin/bash
# Compile-checks every game script against Unity's API without needing the Unity editor.
#
# Requires the .NET SDK (8+). Downloads Unity 2021.3 reference assemblies from NuGet on first run,
# maps the few Unity 6 API renames back (linearVelocity -> velocity, PhysicsMaterial -> PhysicMaterial, ...)
# on a temporary copy, and builds three passes:
#   legacy      - old Input Manager
#   inputsystem - new Input System (ENABLE_INPUT_SYSTEM, using Stubs/InputSystemStubs.cs)
#   editor      - editor scripts too (UNITY_EDITOR, using Stubs/EditorStubs.cs)
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
  if [ ! -d "$REF/$3" ]; then
    curl -sfL -m 300 -o "$REF/$3.nupkg" "https://api.nuget.org/v3-flatcontainer/$1/$2/$1.$2.nupkg" || { echo "download failed: $1"; exit 2; }
    mkdir -p "$REF/$3" && (cd "$REF/$3" && unzip -qo "../$3.nupkg")
  fi
}
fetch unityengine.modules 2021.3.33 modules
fetch unity3d.unityengine.ui 2020.3.21 ui

rm -rf "$WORK/src" && cp -r "$ROOT/Assets/Scripts" "$WORK/src"
find "$WORK/src" -name "*.cs" -exec sed -i \
  -e 's/\.linearVelocity/.velocity/g' \
  -e 's/linearDamping/drag/g' \
  -e 's/angularDamping/angularDrag/g' \
  -e 's/PhysicsMaterialCombine/PhysicMaterialCombine/g' \
  -e 's/\bPhysicsMaterial\b/PhysicMaterial/g' {} +

REFS=""
for f in "$REF"/modules/lib/netstandard2.0/*.dll "$REF"/ui/lib/UnityEngine.UI.dll; do
  REFS="$REFS<Reference Include=\"$(basename "$f" .dll)\"><HintPath>$f</HintPath></Reference>"
done

status=0
for pass in legacy inputsystem editor; do
  case $pass in legacy) DEF="";; inputsystem) DEF="ENABLE_INPUT_SYSTEM";; editor) DEF="UNITY_EDITOR;ENABLE_INPUT_SYSTEM";; esac
  EXCLUDE='Exclude="../src/Editor/**"'; [ $pass = editor ] && EXCLUDE=""
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
  <ItemGroup>$REFS</ItemGroup>
</Project>
PROJ
  out=$(dotnet build "$WORK/$pass/Check.csproj" -nologo -v q -clp:NoSummary 2>&1)
  errors=$(echo "$out" | grep -E "error|warning CS" | sed -E 's|.*/src/|Assets/Scripts/|; s| \[.*||' | sort -u)
  if [ -z "$errors" ]; then echo "PASS  $pass"; else echo "FAIL  $pass"; echo "$errors"; status=1; fi
done
exit $status
