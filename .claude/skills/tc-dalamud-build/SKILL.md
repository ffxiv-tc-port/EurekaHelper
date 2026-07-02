---
name: tc-dalamud-build
description: Build and localize this Dalamud plugin (EurekaHelper) for the Traditional Chinese (TC) FFXIV client via FFXIVSimpleLauncher. Use when asked to compile a TC/繁中 build, fix "MissingMethodException" on plugin load against the TC client, or translate the plugin UI to zh-TW.
---

# Building EurekaHelper for the TC client

## Core problem: TC Dalamud lags mainline Dalamud

TC (Taiwan) client uses **FFXIVSimpleLauncher**, which bundles its own Dalamud
at `%appdata%\FFXIVSimpleLauncher\Dalamud\Injector\`, months behind global
XIVLauncher's `%appdata%\XIVLauncher\addon\Hooks\dev\`.

Global Dalamud already ships modern `Dalamud.Bindings.ImGui` + a
`Window(string, ImGuiWindowFlags, bool)` 3-arg ctor. TC only has classic
`ImGui.NET.dll`/`ImGuiScene.dll`. Building `main` (modern API) and loading in
TC throws:

```
System.MissingMethodException: Method not found: 'Void Dalamud.Interface.Windowing.Window..ctor(...)'
```

Real API-surface mismatch — not fixable via csproj alone. Don't try to keep
the modern namespace and juggle references.

## Check what TC Dalamud actually has, first

```bash
ls "$APPDATA/FFXIVSimpleLauncher/Dalamud/Injector" | grep -i imgui
ilspycmd -t "Dalamud.Interface.Windowing.Window" "$APPDATA/FFXIVSimpleLauncher/Dalamud/Injector/Dalamud.dll" | grep -n "protected Window"
```
`ImGui.NET.dll` + `ImGuiScene.dll` (no `Dalamud.Bindings.ImGui.dll`) = old API
generation, plugin source must match it.

## Fix: branch from git history, don't hand-port the API backward

Don't hand-rewrite `Dalamud.Bindings.ImGui` calls to `ImGuiNET` across every
window file. Instead, this repo has a real historical commit already on the
classic ImGuiNET API (pre-migration):

```bash
git log --oneline --all -S "using Dalamud.Bindings.ImGui" -- EurekaHelper/Windows/PluginWindow.cs | tail -5
```
The commit **before** the one introducing `Dalamud.Bindings.ImGui` is the
base (verify with `-S` search each time — don't trust a cached hash).

```bash
git checkout -b tc-<version> <old-api-commit>
git cherry-pick <real-fix-commit>   # only functional fixes, not reformatting/verbump noise — check with `git show <hash> --stat`
```

## csproj changes on the old-API base

Old-API commit already has explicit `<Reference>` HintPath entries (see
`D:\LatihasChocobo-master\LatihasChocobo.csproj`,
`D:\FFTriadBuddyDalamud\TriadBuddy.csproj` for confirmed-working examples).
Only change needed:

```xml
<DalamudLibPath>$(appdata)\FFXIVSimpleLauncher\Dalamud\Injector\</DalamudLibPath>
```

If instead using a *modern*-SDK csproj (`Sdk="Dalamud.NET.Sdk/..."`):
`Sdk.props` bakes `AssemblySearchPaths` from the default `DalamudLibPath`
before your `PropertyGroup` override is evaluated, so reassigning
`<DalamudLibPath>` alone won't redirect the implicit `Dalamud` reference — you
need explicit `<Reference Remove="Dalamud"/>` + a HintPath override. The
old-API-base approach above avoids this entirely.

## Verify — don't trust "0 build errors"

C# happily binds to whatever `Window` ctor overload is available at compile
time; a clean build doesn't prove the plugin loads. Inspect emitted IL:

```bash
rm -rf EurekaHelper/bin EurekaHelper/obj
dotnet build EurekaHelper/EurekaHelper.csproj -c Debug
ilspycmd -il -t "EurekaHelper.Windows.PluginWindow" "EurekaHelper/bin/Debug/EurekaHelper.dll" | grep "Window::.ctor"
```
Confirm it matches TC's actual `Dalamud.dll` signature from the check above.

## .NET SDK version

Machine only has .NET SDK 9 (`dotnet --list-sdks`). If target csproj says
`net10.0-windows`, drop to `net9.0-windows` (and add
`<LangVersion>13.0</LangVersion>` if using `Dalamud.NET.Sdk/15.0.0`, which
defaults to C# 14 requiring the .NET 10 compiler). Not needed on the old-API
base, already `net9.0-windows`.

## Localizing to zh-TW

If a prior TC build's `EurekaHelper.dll` exists, decompile and reuse its
translation dictionary instead of translating from scratch:

```bash
dotnet tool install -g ilspycmd   # if needed
ilspycmd -p -o <outdir> <path-to-their-EurekaHelper.dll>
```

Look for a `Loc.cs`-style static class with an English→zh-TW
`Dictionary<string,string>`:
- `Loc.Text(string)` — direct lookup, falls back to input if missing
- `Loc.Format(string format, params object[] args)` — `Loc.Text` then `string.Format`
- `Loc.Enum<T>(T)` / `Loc.EnumNames<T>()` — enum display values / combo options
- `Loc.TryEurekaName(...)`, `Loc.RelicStage(...)` — domain-specific NM/boss/relic-stage lookups

Localize the **old-API branch**, not modern `main` — historical TC builds
were compiled from that structure, so line-by-line mapping is far cleaner.

Also check `EurekaHelper.yaml` (feeds `DalamudPackager`'s manifest: `Name`,
`Punchline`, `Description`, `Tags`) — separate from ImGui windows, easy to miss.

## Auto-incrementing build number

Use the build-counter pattern from `D:\Saucy\Saucy\Saucy.csproj`: a
`BuildNumber.txt` next to the csproj, read/incremented via plain MSBuild
properties (not inside a `<Target>`, so it's ready before `DalamudPackager`
stamps `$(Version)`):

```xml
<PropertyGroup>
    <BuildNumberFile>$(MSBuildProjectDirectory)\BuildNumber.txt</BuildNumberFile>
    <_PreviousBuildNumber Condition="Exists('$(BuildNumberFile)')">$([System.IO.File]::ReadAllText('$(BuildNumberFile)').Trim())</_PreviousBuildNumber>
    <_PreviousBuildNumber Condition="'$(_PreviousBuildNumber)' == ''">0</_PreviousBuildNumber>
    <BuildNumber>$([MSBuild]::Add($(_PreviousBuildNumber), 1))</BuildNumber>
    <VersionPrefix>7.15.0</VersionPrefix>
    <Version>$(VersionPrefix).$(BuildNumber)</Version>
    <AssemblyVersion>$(Version)</AssemblyVersion>
    <FileVersion>$(Version)</FileVersion>
</PropertyGroup>

<Target Name="PersistBuildNumber" AfterTargets="Build">
    <WriteLinesToFile File="$(BuildNumberFile)" Lines="$(BuildNumber)" Overwrite="true" />
</Target>
```

Commit `BuildNumber.txt` (don't gitignore it) so the counter persists across
machines/clones.

## Coordination note

If delegating chunks to background agents, give each an isolated `git
worktree` (`isolation: "worktree"`). Running multiple agents on the same
checkout while also editing manually causes branch-switch clobbering and
concurrent-write races. For a repo this size, doing localization/build work
directly (no agents) is often just as fast and safer.
