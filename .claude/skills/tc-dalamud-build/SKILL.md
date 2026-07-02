---
name: tc-dalamud-build
description: Build and localize this Dalamud plugin (EurekaHelper) for the Traditional Chinese (TC) FFXIV client via FFXIVSimpleLauncher. Use when asked to compile a TC/繁中 build, fix "MissingMethodException" on plugin load against the TC client, or translate the plugin UI to zh-TW.
---

# Building EurekaHelper for the TC client

## The core problem: TC Dalamud lags mainline Dalamud

The TC (Taiwan) client is launched via a third-party tool, **FFXIVSimpleLauncher**,
which manages its own Dalamud build at
`%appdata%\FFXIVSimpleLauncher\Dalamud\Injector\`. That build tracks upstream
Dalamud on its own schedule and can be **months behind** the global
`%appdata%\XIVLauncher\addon\Hooks\dev\` install.

Concretely (as of this writing): global XIVLauncher's Dalamud already ships the
modern `Dalamud.Bindings.ImGui` binding and a `Window(string, ImGuiWindowFlags,
bool)` 3-arg constructor. The TC build only ships the classic `ImGui.NET.dll` /
`ImGuiScene.dll` packages. If you build EurekaHelper's `main` branch (which
targets the modern API) and load it in the TC client, it throws at runtime:

```
System.MissingMethodException: Method not found: 'Void Dalamud.Interface.Windowing.Window..ctor(...)'
```

This is **not fixable by editing the .csproj alone** — it's a real API-surface
mismatch. Don't try to keep the modern namespace and just juggle references.

## Before touching anything: check what TC Dalamud actually has

```bash
ls "$APPDATA/FFXIVSimpleLauncher/Dalamud/Injector" | grep -i imgui
```

If you see `ImGui.NET.dll` + `ImGuiScene.dll` (not `Dalamud.Bindings.ImGui.dll`),
TC is on the old API generation and the plugin source must match it.

Confirm the actual `Window` ctor available:
```bash
ilspycmd -t "Dalamud.Interface.Windowing.Window" "$APPDATA/FFXIVSimpleLauncher/Dalamud/Injector/Dalamud.dll" | grep -n "protected Window"
```

## The fix: use git history, don't hand-port the API backward

Don't manually rewrite `Dalamud.Bindings.ImGui` calls back to `ImGuiNET` by
hand across every window file — it's large, error-prone, and there's a much
safer path: **this repo has an actual historical commit that already targets
the classic ImGuiNET API**, from before the upstream author migrated.

Find it:
```bash
git log --oneline --all -S "using Dalamud.Bindings.ImGui" -- EurekaHelper/Windows/PluginWindow.cs | tail -5
```
The commit **before** the one that introduced `Dalamud.Bindings.ImGui` (check
with `git show <that-commit>~1:EurekaHelper/Windows/PluginWindow.cs | head`) is
your base. At the time of writing this was `b1f665d` ("remove test
artifacts") — verify it's still current by re-running the `-S` search, don't
assume the hash.

Branch off it, and cherry-pick forward only the commits between there and
`main` that are real functional fixes (not reformatting/verbump/SDK-upgrade
noise — check each with `git show <hash> --stat`, most of the diff between an
old commit and current `main` is `csharpier` reformatting churn):

```bash
git checkout -b tc-<version> b1f665d
git cherry-pick <real-fix-commit>   # resolve conflicts by keeping the newer side
```

Then apply TC-specific csproj changes on top (see below) and rebuild.

## csproj changes needed on the old-API base

The old-API commit already has the right shape (explicit `<Reference>` HintPath
entries for `Dalamud`, `ImGui.NET`, `ImGuiScene`, etc. — see
`D:\LatihasChocobo-master\LatihasChocobo.csproj` and
`D:\FFTriadBuddyDalamud\TriadBuddy.csproj` for confirmed-working examples of
this exact pattern against the same TC Dalamud). You only need to change:

```xml
<DalamudLibPath>$(appdata)\FFXIVSimpleLauncher\Dalamud\Injector\</DalamudLibPath>
```

(instead of the default `$(appdata)\XIVLauncher\addon\Hooks\dev\`).

If working from a *modern*-SDK csproj instead (`Sdk="Dalamud.NET.Sdk/..."`),
note that `Dalamud.NET.Sdk`'s `Sdk.props` bakes `AssemblySearchPaths` from the
SDK-default `DalamudLibPath` **before** any later `PropertyGroup` override
takes effect (MSBuild property expansion is immediate/textual at the point
each line is evaluated, not late-bound) — so just reassigning
`<DalamudLibPath>` is not enough to redirect the implicit `<Reference
Include="Dalamud"/>`. You'd need explicit `<Reference Remove="Dalamud"/>` +
`<Reference Include="Dalamud"><HintPath>...` overrides. This is one more
reason the old-API-base approach above is simpler than fighting the modern SDK.

## Verify before declaring success — don't trust "0 build errors"

C# happily binds to whatever `Window` constructor overload is available
without complaint at compile time. A clean build is not proof the plugin will
load. After building, inspect the actual emitted IL:

```bash
rm -rf EurekaHelper/bin EurekaHelper/obj
dotnet build EurekaHelper/EurekaHelper.csproj -c Debug
ilspycmd -il -t "EurekaHelper.Windows.PluginWindow" "EurekaHelper/bin/Debug/EurekaHelper.dll" | grep "Window::.ctor"
```
Confirm the printed signature matches what's actually in TC's `Dalamud.dll`
(from the `ilspycmd -t "Dalamud.Interface.Windowing.Window" ...` check above).

## .NET SDK version note

This machine only has .NET SDK 9 installed (`dotnet --list-sdks`). If the
target commit's csproj says `net10.0-windows`, drop it to `net9.0-windows`
(and if using `Dalamud.NET.Sdk/15.0.0`, add `<LangVersion>13.0</LangVersion>`
since that SDK defaults to C# 14 which requires the .NET 10 compiler). Not
needed on the old-API base above, which already targets `net9.0-windows`.

## Localizing to zh-TW (Traditional Chinese)

If a prior TC build of this plugin exists (e.g. someone's shipped
`EurekaHelper.dll`), decompile it and reuse its translation dictionary instead
of translating from scratch:

```bash
dotnet tool install -g ilspycmd   # if not already installed
ilspycmd -p -o <outdir> <path-to-their-EurekaHelper.dll>
```

Look for a `Loc.cs`-style static class with an English→zh-TW `Dictionary<string,string>`
— that's directly portable. Its typical API:
- `Loc.Text(string englishLiteral)` — direct string lookup, falls back to input if missing
- `Loc.Format(string format, params object[] args)` — `Loc.Text` the format string, then `string.Format`
- `Loc.Enum<T>(T value)` / `Loc.EnumNames<T>()` — for enum display values / combo box options
- `Loc.TryEurekaName(...)`, `Loc.RelicStage(...)` — domain-specific lookups (NM/boss names, relic stage names)

**Match the decompiled reference's code generation to your target branch.**
If the reference was built from old-ImGuiNET-era source and you're localizing
current `main` (modern API), the file structure will differ enough that
line-by-line diffing is unreliable — you'll get much cleaner mapping by
localizing the **old-API branch** (see above) instead, since it's structurally
closer to what any historical TC build was compiled from.

After wiring up `Loc.*` calls, also check `EurekaHelper.yaml` — it feeds
`DalamudPackager`'s generated `EurekaHelper.json` manifest (`Name`,
`Punchline`, `Description`, `Tags`) shown in the plugin installer UI. This is
separate from the in-game ImGui windows and easy to miss.

## Auto-incrementing the build/version number

To avoid manually bumping `<Version>` on every local TC build, use the
build-counter pattern from `D:\Saucy\Saucy\Saucy.csproj`: a `BuildNumber.txt`
file next to the csproj, read/incremented/persisted via plain MSBuild
properties (not inside a `<Target>`, so it's evaluated in time for
`DalamudPackager` to pick up `$(Version)` when stamping the manifest):

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

Commit `BuildNumber.txt` to git (Saucy does — don't gitignore it) so the
counter persists across machines/clones instead of silently resetting to 0.

## Coordination note (learned the hard way)

If delegating chunks of this to background agents, give each one an isolated
`git worktree` (`isolation: "worktree"` on the Agent tool call). Running
multiple agents against the *same* checkout while also doing manual `git
checkout`/edits yourself causes branch-switch clobbering and concurrent-write
races on the same files. For a repo this size, doing the localization/build
work directly (without spawning agents) is often just as fast and much safer.
