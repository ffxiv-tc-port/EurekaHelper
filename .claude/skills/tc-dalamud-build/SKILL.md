---
name: tc-dalamud-build
description: Build and localize this Dalamud plugin (EurekaHelper) for the Traditional Chinese (TC) FFXIV client. Use when asked to compile a TC/繁中 build, fix CS0234 "'Dalamud' does not contain 'Bindings'" or MissingMethodException on plugin load against the TC client, or translate the plugin UI to zh-TW.
---

# Building EurekaHelper for the TC client

## Current state (read this first)

- Branch **`tc-7.20`**, Dalamud **API level 13**, TFM `net9.0-windows`, `DalamudPackager` 13.0.0,
  `ECommons` `3.0.0.18-api13`.
- The source is **fully migrated to modern `Dalamud.Bindings.ImGui`** — there is no `using ImGuiNET`
  left anywhere in the repo. `Utils.cs`, `Windows/PluginWindow.cs`, `Windows/AlarmWindow.cs`,
  `Windows/RelicWindow.cs` all use `Dalamud.Bindings.ImGui`.
- `tc-7.15` is a **frozen API12 archive**. Don't commit to it.
  (GitHub's `origin/HEAD` still points at `tc-7.15`, so a fresh clone lands on the old branch —
  `git checkout tc-7.20` right after cloning.)

> **歷史（已不適用）**：這份文件原本整篇的前提是「TC 的 Dalamud 落後 global 好幾個月，只有
> 傳統的 `ImGui.NET.dll`/`ImGuiScene.dll`，所以要從 pre-migration 的舊 commit 開分支、
> cherry-pick 修正，不要手動把 `Dalamud.Bindings.ImGui` 往回改成 `ImGuiNET`」。
> **那是 API12 / `tc-7.15` 時代的做法，現在完全不適用**：TC 7.20 起走 API13，我們自己維護的
> Dalamud（上游 **`yanmucorp`**，不是 `Dalamud-DailyRoutines`）就有 `Dalamud.Bindings.ImGui`，
> 本 repo 也早已完成 API13 移植。看到叫你 `git log -S "using Dalamud.Bindings.ImGui"` 去找
> 「移植前那顆 commit」當基底的段落，直接忽略。

## 🔴 CS0234：`DALAMUD_HOME` / 預設 `DalamudLibPath` 指向的是舊 Dalamud

這是現在最常踩、也最容易被舊文件誤導的一個坑。

`EurekaHelper.csproj` 裡寫死了：

```xml
<DalamudLibPath>$(appdata)\FFXIVSimpleLauncher\Dalamud\Injector\</DalamudLibPath>
```

**那個目錄是 FFXIVSimpleLauncher 自帶的 Dalamud 12.0.2.0**（實測 FileVersion，且**沒有**
`Dalamud.Bindings.ImGui.dll`）。直接 `dotnet build` 會得到：

```
error CS0234: 命名空間 'Dalamud' 中沒有類型或命名空間名稱 'Bindings'
```

更麻煩的是：csproj 裡讀 `$(DALAMUD_HOME)` 的那個 `PropertyGroup` **有 Linux 條件**
（`IsOSPlatform(Linux)`），所以在 Windows 上**設 `DALAMUD_HOME` 環境變數是沒有用的**，
它不會被讀到。必須用 MSBuild 全域屬性覆蓋：

```powershell
dotnet build EurekaHelper\EurekaHelper.csproj -c Release -p:DalamudLibPath="<pin目錄>\"
```

（結尾那個反斜線要留著——csproj 的 `<HintPath>$(DalamudLibPath)Dalamud.dll</HintPath>` 沒有自己補分隔符。）

本機實測可用的 API13 Dalamud 有兩處：

| 路徑 | 版本 | 用途 |
| --- | --- | --- |
| `%APPDATA%\xivlauncher\addon\Hooks\dev` | 13.0.0.6 | 跟 CI 釘的同版，要重現 CI 就用這個 |
| `D:\ffxiv-tc-port\Dalamud\bin\Release` | 13.0.0.16 | TC 遊戲執行期實際載入的那份 |

**不要**去覆寫 `%APPDATA%\FFXIVSimpleLauncher\Dalamud\Injector`（CI 在自己的 runner 上覆寫沒差，
本機那份跟實際遊戲載入有關）。

## ⚠️ CI 釘 13.0.0.6，執行期是 13.0.0.16

`.github/workflows/{build-check,release}.yml` 都會下載
`ffxiv-tc-port/DalamudPluginsTC` 的 release `dalamud-pin-v13.0.0.6/dalamud-api13-net9.zip`，
解壓到 `%AppData%\FFXIVSimpleLauncher\Dalamud\Injector\` 之後才建置。

所以**「本機編得過」不等於「CI 編得過」**。最具體的地雷是 `ClientLanguage.TraditionalChinese`(=7)
這個列舉名，13.0.0.6 **沒有**（已用二進位字串驗證），要用就得寫數值 `is 4 or 5 or 7`，
寫列舉名 CI 必炸。

`release.yml` 只吃 `workflow_dispatch`，build 時用 `-p:Version=<tag> -p:AssemblyVersion=… -p:FileVersion=…`
從 tag 覆蓋版本，然後把 `latest.zip` 改名成 `EurekaHelper.zip` 發佈。

## Verify — don't trust "0 build errors"

C# happily binds to whatever overload is available at compile time; a clean build doesn't prove the
plugin loads against the Dalamud the game actually has. 真的要確認就去看產出的 IL / AssemblyRef：

```bash
rm -rf EurekaHelper/bin EurekaHelper/obj
dotnet build EurekaHelper/EurekaHelper.csproj -c Release -p:DalamudLibPath="<pin目錄>/"
ilspycmd -il -t "EurekaHelper.Windows.PluginWindow" "EurekaHelper/bin/Release/EurekaHelper.dll" | grep "Window::.ctor"
```

這條「不要相信 0 errors、要驗二進位」的原則在 API13 遷移期救過好幾個 repo（有 repo 表面編得過，
實際是綁到了錯的 Dalamud），值得保留。

## 輸出路徑：`x64` 陷阱（仍然有效，不要退回）

`EurekaHelper.csproj` 宣告 `<Platforms>x64</Platforms>`，MSBuild 預設的
`AppendPlatformToOutputPath=true` 會多插一層 `x64`，讓產物跑到 `bin\x64\Release\EurekaHelper.dll`，
載入時就會出現：

```
System.Exception: Plugin DLL file at '...\EurekaHelper\bin\Release\EurekaHelper.dll' did not exist, cannot load.
```

已經在 csproj 加了 `<AppendPlatformToOutputPath>false</AppendPlatformToOutputPath>`
（連同 `<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>`）修掉，
輸出是扁平的 `bin\Release\`。**不要**在複製別的 repo 的 `PropertyGroup` 時把這兩行弄丟。

> **歷史（已不適用）**：舊版寫「一定要 `-c Release`，因為
> `%appdata%\FFXIVSimpleLauncher\Dalamud\Config\dalamudConfig.json` 的 `DevPluginLoadLocations`
> 指向 `bin\Release\EurekaHelper.dll`」。**實測現在那個 `DevPluginLoadLocations` 清單是空的**
> ——外掛是從 feed 安裝的，不是 devPlugin。也就是說本機 build 出來的 dll 根本不會進遊戲，
> reload 也沒用。要在遊戲裡驗證改動，要嘛走正常發版流程，要嘛自己把 `bin\Release` 加回 devPlugin 清單。
> （真的加回去的話，仍然建議 `-c Release`：遊戲裡載入的 Dalamud 是 Release 組態。）

## .NET SDK version

本機實測 `dotnet --list-sdks`：`7.0.400` / `8.0.406` / `9.0.315` / `11.0.100-preview.5`。
**沒有 SDK 10**。所以目標框架維持 `net9.0-windows`（本 repo 已經是），
如果從上游拿到寫 `net10.0-windows` 的 csproj 要降回 `net9.0-windows`
（若用 `Dalamud.NET.Sdk/15.0.0` 還要補 `<LangVersion>13.0</LangVersion>`，
因為它預設 C# 14、需要 .NET 10 編譯器）。

## Localizing to zh-TW

翻譯字典**已經在 repo 裡**：`EurekaHelper/Loc.cs`（`internal static class Loc`）。
直接改那支就好，不需要再去反編譯舊的 TC 版 dll。

- `Loc.Text(string)` — 直接查表，查不到就原樣回傳輸入
- `Loc.Format(string format, params object[] args)` — 先 `Loc.Text` 再 `string.Format`
- `Loc.Enum<T>(T)` / `Loc.EnumNames<T>()` — 列舉顯示值 / combo 選項
- `Loc.TryEurekaName(...)`、`Loc.RelicStage(...)` — NM/王/魂武階段等領域專用查表

> **歷史（已不適用）**：舊版教你 `ilspycmd -p -o <outdir> <舊的 EurekaHelper.dll>` 去把別人 TC 版的
> 翻譯字典反編譯出來重用，並且說「要在 old-API 分支上翻譯，不要翻 modern `main`」。
> 現在 `Loc.cs` 已經是本 repo `tc-7.20` 上的一等公民，沒有 old-API 分支可言。

另外別漏掉 `EurekaHelper/EurekaHelper.yaml`——它餵給 `DalamudPackager` 產生 manifest 的
`name` / `punchline` / `description` / `tags`，跟 ImGui 視窗完全分開，很容易忘。

> 觀察（未處理）：`EurekaHelper.yaml` 的 `icon_url` 目前還指向 **`tc-7.15`** 分支的 raw 路徑。
> 舊分支還在所以圖示沒壞，但那是遷移前留下來的；若之後 archive/刪掉 `tc-7.15` 就會破圖。

## Auto-incrementing build number

`EurekaHelper.csproj` 用一個受 git 追蹤的計數檔 `BuildNumber.txt`（在 csproj 旁邊），
以純 MSBuild 屬性讀取／遞增（**不放在 `<Target>` 裡**，這樣 `DalamudPackager` 蓋 `$(Version)` 時才讀得到）：

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

`BuildNumber.txt` 要 commit（不要 gitignore），計數才會跨機器/clone 延續。但要知道：

- **這個版本號跟實際發版無關**：`release.yml` 用 `-p:Version=<tag>` 從 git tag 覆蓋，
  所以 `VersionPrefix` 至今還是 `7.15.0`、feed 上卻是 `v7.20.0.x`，**這不是漏改**，不要去動它。
- 每次 build 都會改到 `BuildNumber.txt`、**弄髒工作區**。它是建置副產物，
  直接 `git checkout -- EurekaHelper/BuildNumber.txt` 還原即可，小心 `git add -A`。
- ⚠️ 工作區髒掉會讓 `release_plugin.py` 判定「有未提交變更」而**直接跳過這個外掛不發版**。
- `release_plugin.py` 本來就是平行執行（`ThreadPoolExecutor`），而且**只推 tag**，
  不推分支、也不 commit `repo.json`，那兩件事要自己做。

## Coordination note

If delegating chunks to background agents, give each an isolated `git worktree`
(`isolation: "worktree"`). Running multiple agents on the same checkout while also editing manually
causes branch-switch clobbering and concurrent-write races. For a repo this size, doing
localization/build work directly (no agents) is often just as fast and safer.

另外：多個 agent 平行建置容易留下 MSBuild reuse node 鎖住 DLL（`MSB3026`/`MSB3027`），
解法是 `dotnet build-server shutdown`，不要去 kill 個別 PID。
