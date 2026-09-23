# AGENTS.md

面向在本仓库工作的 AI 编码代理（以及人类贡献者）。
本文件只写**在这个 fork 里必须知道、且从上游 osu! 资料看不出来**的内容；通用架构与玩法逻辑请直接读代码。

---

## 1. 这是什么项目

- `g0v0.osu.Game` 是 [ppy/osu](https://github.com/ppy/osu)（osu!lazer）的 fork，由 **GooGuTeam** 维护，客户端对外名称是 **g0v0!**，对接 [g0v0-server](https://github.com/GooGuTeam/g0v0-server)。
- 相比上游的主要改动：自定义 API 服务器、Relax / Autopilot 特殊规则集、规则集哈希校验、服务器信息通知、更新源与错误上报切换到 GooGuTeam。逐条说明见 `README.md`，代码位置见第 6 节。
- 许可：代码 MIT（上游部分见 `LICENCE-OSU`，本 fork 新增部分见 `LICENCE`）；游戏资源 CC-BY-NC 4.0，仅限非商业使用。

### Git 布局（重要）

| 远端 / 分支 | 说明 |
|---|---|
| `origin` | `https://github.com/GooGuTeam/g0v0.git` |
| `upstream` | `https://github.com/ppy/osu.git`，只读，用于同步 |
| **`v2`** | **默认开发分支**，日常改动都落在这里；与上游的差距用 `git rev-list --left-right --count upstream/master...v2` 自查 |
| ~~`master`~~ | 已删除：GitHub 上该分支早已不存在，本地分支也已清理；仅剩失效的 `origin/master` 跟踪引用，**不要基于它开发** |
| 标签 | 发布标签形如 `v2026.910.2-g0v0`；同步前自行打基线标签（历史遗留的 `backup/*` 系列已于 2026-09 清理） |
| 资源仓库 | 资源已拆到独立仓库 `GooGuTeam/g0v0-resources`，以 NuGet 包 `g0v0.osu.Game.Resources` 被本仓库引用；主仓库用 `G0V0ResourcesVersion` 锁定版本 |

---

## 2. 环境与常用命令

需要 .NET SDK **10.0.100+**（`global.json` 固定 10.0.100，`rollForward: latestFeature`）；C# 12、`Nullable` 已开启。

```bash
dotnet restore osu.Desktop.slnf      # 自动从 nuget.org 还原 g0v0.osu.Game.Resources
dotnet build -c Debug -warnaserror osu.Desktop.slnf                                   # 与 CI 一致
dotnet build -c Debug -warnaserror osu.Desktop.slnf -p:EnforceCodeStyleInBuild=true   # 额外开启代码风格分析器

bash CheckSanity.sh <目录|文件>   # 代码规范体检（Windows 用 ./CheckSanity.ps1）
bash InspectCode.sh              # ReSharper 静态检查（Windows 用 ./InspectCode.ps1，需先构建）

# 测试：CI 先构建，再对构建产物跑 dotnet test
OSU_EXECUTION_MODE=SingleThread dotnet test osu.Game.Tests/bin/Debug/**/osu.Game.Tests.dll
```

注意：

- `CheckSanity.sh` / `InspectCode.sh` 在 checkout 里**没有可执行位**，请用 `bash <script>`，或先 `chmod +x`（CI 就是这么做的）。
- 方案过滤器：`osu.Desktop.slnf`（桌面 + 测试）、`osu.Android.slnf`、`osu.iOS.slnf`。不要直接构建 `osu.sln`。
- 完整测试项目清单与矩阵见 `.github/workflows/ci.yml`：`osu.Game.Tests`、`osu.Game.Rulesets.{Osu,Taiko,Catch,Mania}.Tests`、`osu.Game.Tournament.Tests`、`Templates/**/*.Tests`，跑 Windows + Linux × `SingleThread`/`MultiThreaded` 两种线程模型。
- `bin/`、`obj/` 已在 `.gitignore` 中，不要提交。

---

## 3. 代码规范（CI 会拦）

### 格式

`.editorconfig`：`.cs` 使用 **CRLF**、4 空格缩进、去行尾空白、文件末尾换行；`*.csproj|props|targets` 使用 **UTF-8 BOM + CRLF + 2 空格**。

### `CheckSanity.sh` 的四条硬性检查（逐 `.cs`）

1. 行尾必须是 CRLF（不允许出现孤立的 `\r`）；
2. 不允许 tab 字符；
3. 不允许行尾空格（`///` 文档注释行豁免）；
4. **文件名必须与文件内定义的类型名一致**（`Foo.cs` 中需有 `class/struct/record/interface/enum Foo`）。

`*.designer.cs` 与 `AssemblyInfo.cs` 跳过；扫描忽略 `.git`、`bin`、`obj`、`Migrations`、`packages`、`osu.Game.Resources`（本地独立资源仓库 clone，可选）。

### 版权头（按文件来源三种）

```csharp
// 1) GooGuTeam 全新创建的文件
// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// 2) 修改了上游文件
// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

// 3) 未改动的上游文件（版权行保留 ppy 原文，第二行指向 LICENCE-OSU）
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.
```

- 全仓库统一**英式拼写**（`Licence` / `LICENCE`），与上游 ppy/osu 一致；不要写成美式 `License` / `LICENSE`。
- 根目录许可文件是 `LICENCE`（GooGuTeam 部分）与 `LICENCE-OSU`（上游部分），头里的引用必须与文件名一致：GooGuTeam 自己的文件指 `LICENCE`，纯上游文件指 `LICENCE-OSU`，两者都涉及就都写。
- 第 3 类与上游字面上只差第二行的文件名，这是有意为之；同步时若该行产生冲突，按本文件的口径解决。
- ppy 的版权署名是 MIT 的要求，**不是**商标问题，不要删（详见第 5 节）。

### 分析与命名

- `CodeAnalysis/osu.globalconfig` + `CodeAnalysis/BannedSymbols.txt`（被禁用的 API 会让构建报错）；CI 使用 `-warnaserror -p:EnforceCodeStyleInBuild=true`。
- 公共/受保护成员 PascalCase，私有成员 camelCase，私有 `const` 与 `static readonly` 全小写加下划线。

### 提交前自检（对应 CI 的 `inspect-code`）

```bash
bash CheckSanity.sh <改动目录>    # 规范体检；Windows: ./CheckSanity.ps1
bash InspectCode.sh               # ReSharper 静态检查；Windows: ./InspectCode.ps1
```

- **两个都要跑。** `InspectCode.ps1`（Windows）第一行就会调用 `CheckSanity.ps1`，但 `InspectCode.sh`（Linux/macOS）只是 `chmod +x CheckSanity.sh` 而**不执行它** —— 在 Linux 上必须自己再跑一次 `CheckSanity.sh`。
- `InspectCode` 脚本用 `--no-build` 跑，**必须先构建**（第 2 节的 `dotnet build -c Debug osu.Desktop.slnf`），否则找不到程序集。
- 报告经 `dotnet nvika parsereport --treatwarningsaserrors` 汇总 —— 这里 InspectCode 的 warning **等同 error**，脚本退出码非 0 就是没过。脚本自带 `dotnet tool restore`（工具版本见 `.config/dotnet-tools.json`），首次运行需要联网拉取工具。
- 改动很小时至少跑 `CheckSanity.sh`；碰到公共 API、可空性、性能写法、分析器规则时，把 `InspectCode` 一起跑掉再提交。

### 用户可见字符串（本地化）

1. 在 `osu.Game/Localisation/<X>Strings.cs` 里按现有模式加 `getKey(@"snake_case_key")` + 英文原文；
2. 同时在资源仓库 `g0v0-resources` 的 `osu.Game.Resources/Localisation/<X>.resx`（英文源文件，无 locale 后缀）补上同名 `<data name="...">` 条目 —— 缺了这个 key，翻译管线拿不到它；
3. `<X>.<locale>.resx` 由 Crowdin 生成，**不要手写**；
4. 资源改动在 `g0v0-resources` 仓库提交、打 tag、发布新的 `g0v0.osu.Game.Resources` 包；主仓库再把 `G0V0ResourcesVersion` 更新到该版本。

### 测试

NUnit 4 + `osu.Framework` 的 `TestScene`：可视化测试放 `osu.Game.Tests/Visual/**`，纯逻辑测试放 `osu.Game.Tests/NonVisual/**`。fork 的破坏性改动使部分上游测试被 `[Ignore]`，恢复前先搞清楚原因（`git log` 里搜相关提交）。

---

## 4. 同步上游（例行操作，最容易踩坑）

1. `git fetch upstream`，把 `upstream/master` 合并进 `v2`；解冲突时**顺手丢弃品牌字符串**比事后清理省事。
2. 资源仓库单独同步：进入独立的 `g0v0-resources` clone，确认其 `upstream` 远端为 `https://github.com/ppy/osu-resources`，同样合并 `upstream/master`；如有资源变更，发新包并同步更新主仓库 `G0V0ResourcesVersion`。
3. 合并后检查 `.github/workflows/*.yml` 与 `.github/actions/*.sh` 中的 `dotnet-version` / `--framework`（例如 `net8.0-*` → `net10.0-*`）是否与 `global.json`、csproj 对齐 —— 不对齐会直接让 CI 构建失败（历史上发生过）。
4. 确认主仓库 `G0V0ResourcesVersion` 指向的 `g0v0.osu.Game.Resources` 包已经在 nuget.org 上存在，且与本次发布 tag 版本一致。
5. 同步**前**先打一个基线标签（如 `sync-baseline-YYYYMMDD`）再动手，方便 diff 与商标扫描；历史遗留的 `backup/*` 标签已在 2026-09 清理，`trademark-check` 会退回到 `git merge-base HEAD upstream/master`。

---

## 5. 品牌与商标（硬性规则）

> **删除 / 重命名任何 osu!、ppy 商标内容之前，必须先获得人类确认。**

这个 fork 以 **g0v0!** 名义发布，不能把上游品牌呈现为自己的；但许可证要求的署名必须保留。

- 流程、报告模板与只读检测脚本都在 `.agents/skills/trademark-check/`：
  - `SKILL.md` —— 何时使用、clean/keep 对照表、报告格式；
  - `scripts/detect.sh` —— 只读检测，支持 `--base <ref>`、`--incoming`；
  - `references/known-outstanding.md` —— 已分类的既有项，避免重复上报。
- 触发时机：从上游同步后、更新并发布资源包后、打 `v*-g0v0` 发布标签前、新增用户可见字符串或图片素材时。
- 口径速记 —— **要清理**：用户可见的 `osu!` 字样、吉祥物 / logo / 宣传素材、商业字体、上游推广与联系链接；**要保留**：MIT 版权头、NuGet 包名 `ppy.osu.Framework` / 本 fork 自己的 `g0v0.osu.Game.Resources`（上游对应包 `ppy.osu.Game.Resources` 仅作来源说明）、`osu.Game.*` 命名空间与类型名、`.osu` 格式以及 `osu!stable` / `osu!direct` 这类 nominative 用法。
- 产出永远是「清单 + 建议 + 提问」，拿到明确范围后才动文件。

---

## 6. Fork 改动分布（代码地图）

| 功能 | 主要位置 |
|---|---|
| 生产端点（`lazer.g0v0.top`） | `osu.Game/Online/ProductionEndpointConfiguration.cs`、`DevelopmentEndpointConfiguration.cs` |
| 自定义 API 服务器设置 | `osu.Game/OsuGameBase.cs`、`osu.Game/Configuration/OsuConfigManager.cs`、`osu.Game/Online/TrustedDomainOnlineStore.cs`、`osu.Game/Overlays/Settings/Sections/Online/ContentDownloadSettings.cs` |
| 规则集哈希校验 | `osu.Game/Rulesets/RulesetHashCache.cs`、`osu.Game/Online/API/APIAccess.cs`、`osu.Game/Online/HubClientConnector.cs`、`osu.Game/Screens/Play/{SubmittingPlayer,SoloPlayer,RoomSubmittingPlayer}.cs` |
| Relax / Autopilot | `osu.Game/Rulesets/Mods/ModRelax.cs`、`osu.Game.Rulesets.Osu/Difficulty/Skills/Relax.cs`、`osu.Game.Rulesets.Osu/Difficulty/Evaluators/Relax{Aim,Rhythm}Evaluator.cs` |
| 更新源（指向 GooGuTeam/osu） | `osu.Game/Updater/{MobileUpdateNotifier,NoActionUpdateManager}.cs`，桌面侧用 Velopack（`osu.Desktop`） |
| 错误上报 | `osu.Game/Utils/SentryLogger.cs`（仅当连接的是 g0v0.top 时启用） |
| 程序名 / 图标 | `osu.Desktop/osu.Desktop.csproj`（`AssemblyName` = `g0v0!`） |
| 奖章素材与新增奖章 | `osu.Game/Users/Medal.cs`（`lazer-data.g0v0.top`）、`osu.Game/Medals/Awarders/` |

改到 `g0v0-resources` 仓库里的素材或 `.resx` 时，记住它是要单独提交推送、单独打 tag 发布 NuGet 的另一个仓库；主仓库只通过 `G0V0ResourcesVersion` 引用发布后的包。

---

## 7. CI 与发布

- `.github/workflows/ci.yml`：`inspect-code`（构建 + CheckSanity + InspectCode）、`test`（Windows/Linux × 两种线程模型，120 分钟超时）、`build-only-android` 等。
- 主仓库 `.github/workflows/release.yml`：由 **`v*-g0v0`** 标签触发；NuGet 任务会先校验 `g0v0.osu.Game.Resources` 同版本包已发布，再发 `g0v0.osu.Game` / `g0v0.osu.Game.Rulesets.Osu`；各平台构建脚本在 `.github/actions/*.sh`。
- 资源仓库 `GooGuTeam/g0v0-resources` 的 `.github/workflows/release.yml`：同样由 **`v*-g0v0`** 标签触发，使用 NuGet trusted publisher 发布 `g0v0.osu.Game.Resources`。**先发资源包，再发主仓库包。**
- **推送标签时只推 `-g0v0` 的发布标签。** 绝不要用 `git push --tags`：本地约 930 个标签里绝大多数是随 `upstream` 一起 fetch 下来的上游历史标签，一条命令就会把它们全部推到 `origin`。始终用显式标签名：
  ```bash
  git tag -l 'v*-g0v0'                 # 本仓库自己的发布标签
  git push origin v2026.910.2-g0v0     # 只推这一个
  ```
  推错标签既不会触发 release 构建，又会污染 `origin` 的标签空间（`origin` 目前只有 8 个 `v*-g0v0` 标签，保持这样）。
- 依赖与 Actions 版本由 Dependabot 维护（`.github/dependabot.yml`）。
- 打标签、推送远端、发布这类对外可见且不可逆的动作，先问人。

---

## 8. 提交与协作

- 提交信息沿用上游风格：祈使句、首字母大写、句末不加句号，例如 `Fix code sanity failures`；来自上游的提交保持原样并保留 `(#PR号)`，便于对账。
- 新分支从 `v2` 切出，PR 提到 `GooGuTeam/g0v0`。
- 大范围重格式化提交要记入 `.git-blame-ignore-revs`，避免污染 blame。

---

## 9. 代理工作方式

- 改完先跑 `bash CheckSanity.sh <改动目录>`，涉及公共 API 或分析器规则时再加 `bash InspectCode.sh`（Windows 用 `./InspectCode.ps1`），然后跑与改动相关的测试项目，不要动辄全量。
- 判断某处差异是不是「fork 有意为之」时，查 `git log`、同步基线标签、`.agents/skills/trademark-check/references/known-outstanding.md`，别凭感觉改回去。
- 上游大版本升级后，优先核对 `global.json`、csproj 的 `TargetFramework` 与 CI 里的 SDK / framework 版本三者一致。
