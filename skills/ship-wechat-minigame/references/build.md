# 工程改造与构建：Unity → WebGL → 微信小游戏

整个链路是：Unity 构建 WebGL，微信转换工具把它转成小游戏包（`minigame/`），再用微信开发者工具运行、预览、上传。下面除特别标注外都是 `[实测]`：Unity 6000.6、URP、新 Input System、竖屏单机游戏，SDK v0.1.34，开发者工具 Stable 2.02.2608080（macOS arm64）。

## 1. 选引擎版本

| 情况 | 做法 |
|---|---|
| 新项目，或者工程能降级 | 用 Unity 2022.3 LTS 或团结引擎。这是官方推荐，不需要本仓库的 Unity 6 补丁 `[官方]` [推荐引擎版本](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/UnityVersion.html) |
| 已有 Unity 6 工程 | 走本仓库的补丁路线，见下面的第 3、4 节和第 10 节 |

- 官方对 Unity 6 的态度：SDK 更新日志写「支持Unity6，预览版本不建议上线使用」，推荐版本表里没有 Unity 6 `[官方]`（[CHANGELOG](https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk/blob/main/CHANGELOG.md)）。
- 实测 6000.6 能在真机上正常玩。以前记的「安卓偶尔黑屏」已经查明：引擎不等资源包就启动，新设备第一次打开时，资源包只要比引擎晚到就一直黑屏。第一次提审就因此被驳回。第 10 节的 B7 修好了它。
- 两者冲突时以官方说法为准：把风险告诉用户，并写进进度档案。

同一个工程直接加 WebGL 目标就行，不用开分支：
- 平台差异（广告、成就、评分、隐私）放进平台层的条件编译里 `[实测]`。
- 验证阶段可以用 `git worktree` 另开一个目录，主目录继续出安卓包。

## 2. 装 WebGL 模块

先告诉用户下载量和占用空间，得到同意后再装：

```bash
unity editors module add <Unity 版本> -m webgl --accept-eula --non-interactive
```

- Apple 芯片的 Mac 再加 `-a arm64`。
- 实测下载约 2.1GB，装好后约 6.3GB，用时约 6 分钟。
- 也可以在 Unity Hub 里加装「Web Build Support」。

## 3. 拉取 SDK 并打补丁：`scripts/fetch_wxsdk.sh`

```bash
bash <本 skill 目录>/scripts/fetch_wxsdk.sh <工程目录>              # 默认固定到 v0.1.34
WXSDK_REPO=https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk.git \
  bash <本 skill 目录>/scripts/fetch_wxsdk.sh <工程目录>             # Gitee 不通时改用 GitHub
```

脚本做的事：
- 从官方 Gitee 镜像克隆 SDK，检出脚本里固定的提交（v0.1.34），放进 `<工程>/Packages/com.qq.weixin.minigame/` 作为内嵌包。
- 工程是 Unity 6000.x 时，再打 6 处源码补丁（第 10 节的 F1～F6）。

为什么不用 Package Manager 的 git 地址安装：
- Unity 内部的 git 不走终端代理，从 GitHub 拉会报 `SSL_ERROR_SYSCALL` `[实测-失败]`。
- 所以 SDK 作为本地内嵌包放进工程。

要做的事：
- 把 `Packages/com.qq.weixin.minigame/` 加进工程的 `.gitignore`。
- 每次新 checkout 工程后重跑一次脚本。

脚本输出怎么看：
- `patched ...`：补丁打上了。
- `already fixed upstream ...`：官方已经修了，这条补丁不再需要。
- `PATCH NOT APPLIED ...`：SDK 代码变了，退出码是 1。这时不要继续导出，先看第 10 节判断这个补丁还需不需要。

版本规则：
- 固定用 v0.1.34。官方稳定版就是 v0.1.34（2026-09-16）`[官方]` [更新日志](https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk/blob/main/CHANGELOG.md)。
- v0.1.35 是预览版，「转换插件升级为 WX-WASM-SDK-V3」，补丁可能失效。等它转成稳定版并重新验证以后再升。
- 旧仓库 `minigame-unity-webgl-transform` 已被 GitHub 禁用，不要用。

## 4. 导出脚本：`scripts/ShipWeChatBuild.cs`

1. 把它复制到 `<工程>/Assets/Editor/ShipWeChatBuild.cs`。
2. 写 `<工程>/publish/wechat/build.json`，模板是 `templates/build.json`，字段见第 5 节。AppID 也可以用环境变量 `WX_APPID` 传入，环境变量优先。
3. 导出：

   ```bash
   bash <ship-my-game 目录>/scripts/unity_build.sh <工程目录> WebGL ShipWeChatBuild.Export wechat
   ```

   日志在 `<工程>/Logs/ship/wechat.log`，实测每次导出约 1 分钟。
4. 看日志：
   - 成功时有 `[Ship] wechat export=SUCCEED dst=.../minigame appid=... version=...`。
   - Unity 6 工程还有 `[Ship] framework patched: ...`，以及 `[Ship] B7: ...` 或 `[Ship] B7 not needed: ...` 其中一条。
   - 失败时退出码是 1。
5. 在模拟器里复现一次第一次打开，步骤见第 10 节 B7 下面的小节。

脚本设置的内容：
- PlayerSettings：托管代码裁剪 Medium、IL2CPP Release、异常支持 `ExplicitlyThrownExceptionsOnly`、`stripEngineCode`、关闭 Splash。
- 关掉「WebAssembly 2023」和 BigInt（补丁对照表的 B6）。
- SDK 配置：
  - `assetLoadType = 1`：首包资源放在小游戏包内，不需要 CDN 服务器。
  - `compressDataPackage = true`：Brotli 压缩。
  - `Webgl2 = true`：Unity 6 必须开（补丁对照表的 B0）。
  - `showMonitorSuggestModal = false`：关掉「优化建议」弹窗。这个弹窗会出现在开发版、体验版里，也会出现在备案截图里。
- Unity 6 工程还会给导出后的 framework JS 打 6 处补丁（第 10 节的 B1～B5 和 B7）。
- 不用另外设置压缩格式：SDK 导出时自己会把 Player Settings 的 Compression Format 设成 Disabled。官方 QA 也要求「保持为不压缩」。

切到 WebGL 平台后，只在安卓用的代码会编译失败，例如 Google Play 游戏服务、AdMob 的编辑器和运行时脚本。处理方法：
- 把这些代码包进 `#if UNITY_ANDROID`。
- 还没装微信 SDK 的分支，在 Scripting Define Symbols 里加 `SHIP_NO_WECHAT`，导出脚本就会整体跳过。

## 5. `publish/wechat/build.json` 字段

| 字段 | 取值 | 说明 |
|---|---|---|
| `appid` | 正式小游戏 AppID | 在「开发管理 → 开发设置」里取。不能用测试号或游客号，否则插件加载失败 |
| `projectName` | 游戏名，纯中文 | 开发者工具里显示的项目名。留空时用 `PlayerSettings.productName` |
| `orientation` | `Portrait` | `WXScreenOritation` 的成员名，共四个：`Portrait`、`Landscape`、`LandscapeLeft`、`LandscapeRight`。实测只用过 `Portrait` |
| `dst` | `Builds/wechat` | 导出目录，相对工程根目录。小游戏项目在 `<dst>/minigame` |
| `cjkFont` | `Assets/Fonts/CJKSubset.ttf` | `make_cjk_font.py` 的输出 |
| `cjkFamily` | `CJK Subset` | `make_cjk_font.py` 写入的字体族名 |
| `latinFonts` | uGUI 用到的 TrueType 字体路径数组 | 脚本会给这些字体加上中文 fallback。用 TextMeshPro 时留空，改为在 TMP 的 fallback 里加动态字体资产 |

## 6. 中文字体

WebGL 没有系统字体回退，中文会显示成方块。SDK 在 2026-08 的更新里又关掉了 iOS 上拉取微信系统字体的功能。所以字体必须自带。

```bash
pip install fonttools
python3 <ship-my-game 目录>/scripts/make_cjk_font.py --font NotoSansSC[wght].ttf --out Assets/Fonts/CJKSubset.ttf \
  --scan Assets --text "简体中文一二三四五六七八九十月年"
```

- 字体用 Noto Sans SC（SIL OFL 许可，从 Google Fonts 下载），游戏里附上它的 OFL.txt。
- `--text` 用来补代码运行时才拼出来的字，例如语言名、日期里的月份。实测漏了这些字时，界面上出现过「语言：中」，日历标题缺了「十」。
- 脚本输出一般 300～900KB；实测 610 字时 167KB。
- Unity 6000.6 的坑：`TrueTypeFontImporter` 的 `fontNames` 和 `fontReferences` 赋值写不进 `.meta`。`ShipWeChatBuild` 改为直接写序列化字段 `m_FontNames` 和 `m_FallbackFontReferences`，不用手工处理。

## 7. 必改代码

| 项 | 做法 |
|---|---|
| 初始化顺序 | 在 `WX.InitSDK` 的回调里创建游戏，再初始化广告。主页出现后调用一次 `WX.ReportGameStart()`（SDK 的提示） |
| 存档 | `persistentDataPath` 上的文件不可靠，改用 `WX.StorageSetStringSync` 和 `WX.StorageGetStringSync`。关闭小游戏时不一定会走到 `OnApplicationPause`，所以每步关键操作后立即存。实测原来每 6 步存一次，最后几步丢过 |
| 触摸 | 不要自己写触摸桥接。微信的适配层会把 `wx.onTouch*` 转成 DOM 触摸事件，Unity 的 WebGL 输入（包括新 Input System）能直接读到。自己写桥接反而出过三个问题：每次触摸报 `_malloc`、和适配层冲突、模拟器只响应第一次点击。旧输入模块配 uGUI 时，官方做法是在 EventSystem 上挂 `WXTouchInputOverride` `[官方]` [输入适配](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/InputAdaptation.html) |
| 安全区和胶囊 | 小游戏里 `Screen.safeArea` 等于整个屏幕，不避让刘海和胶囊，要改用微信接口计算，见 [capabilities.md](capabilities.md#安全区和胶囊按钮) |
| 外链和商店 | 小游戏里打不开外部网页和应用商店。要隐藏「隐私政策」外链、「给游戏评分」、评分弹窗，以及依赖 Google Play 游戏服务的成就和排行榜 |
| 名称 | 主页标题要显示已注册的中文游戏名，因为备案截图要求能看到当前小游戏名。英文 logo 只给其他渠道用 |
| 广告 | 广告位 ID 为空时什么都不创建，看广告的按钮显示「暂时没有可播放的视频」，见 [capabilities.md](capabilities.md#广告流量主) |
| 声音 | 每个会启动的场景里有且只有一个 `AudioListener`，见总入口的体检清单 |

## 8. 导出产物和包体

```
<dst>/minigame/
├── game.js  game.json  project.config.json  weapp-adapter.js  webgl.wasm.framework.unityweb.js  unity-sdk/  images/ …   主包
├── wasmcode/        wasm 代码分包（Brotli）
└── data-package/    首包资源分包（Brotli）
<dst>/webgl/         WebGL 原始产物（首包资源放包内时不用部署）
```

- `game.json` 的内容：
  - 声明了 `wasmcode`、`data-package` 两个分包，并且都写进了 `parallelPreloadSubpackages`；
  - 声明了插件 `UnityPlugin`，v0.1.34 配的是 1.3.14，以导出的 `game.json` 为准；
  - `deviceOrientation` 和 `iOSHighPerformance: true`。
- `project.config.json` 的 `packOptions.ignore` 已经排除了 `.symbols.unityweb`、`.symbols.unityweb.br` 和 `.plugincache`，8MB 左右的符号文件不会打进包。
- 实测大小：
  - 原始 wasm 32.5MB，压缩后的 wasm 分包 7.3MB；
  - 数据分包 4.6～4.7MB；
  - 主包不到 1MB；
  - 开发者工具显示的代码包 13～14MB；
  - 引擎由 UnityPlugin 提供，不占包体。
- 上限 `[官方]`：
  - 主包 ≤ 4MB，所有包合计 ≤ 30MB，单个普通分包不限大小（[代码包](https://developers.weixin.qq.com/minigame/dev/guide/base-ability/code-package.html)、[分包加载](https://developers.weixin.qq.com/minigame/dev/guide/base-ability/subPackage/useSubPackage.html)）。
  - Unity 文档对「首包资源放小游戏包内」写的是「wasm代码+首包资源>20M时，资源包不可再使用小游戏分包加载」（[UsingLoader](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/UsingLoader.html)）。
  - 两处说法不同，按 20MB 控制。
- 超过 20MB 怎么办：把首包资源改放 CDN（`assetLoadType = 0`）。CDN 域名必须做过 ICP 备案、使用 HTTPS，并加到「开发管理 → 开发设置 → 服务器域名」的下载合法域名里 `[官方]`（[网络](https://developers.weixin.qq.com/minigame/dev/guide/base-ability/network.html)）。这条路本仓库没有实测过。

## 9. 微信开发者工具：导入、模拟器、预览

**导入**
1. 菜单「项目 → 导入项目」。启动页停在「创建小游戏」时也用这个菜单。
2. 选 `<dst>/minigame`，不要选导出根目录。
3. 导入页会读出项目名和 AppID。AppID 显示成游客号 `touristappid` 时，说明 build.json 没写对，回去改了重新导出。
4. 点导入，弹窗里选「信任并运行」。「发现 git 仓库」的提示可以直接关掉。
5. 之后每次重新导出，工具会自己检测到文件变化并重载。没有重载时，用菜单「项目 → 重新打开此项目」。

**模拟器的正常现象**
- 重启工具后白屏、「预览」按钮变灰，约 1.5 分钟后自己恢复。
- wasm 编译期间常弹「模拟器长时间没有响应」，关掉弹窗继续等。
- 模拟器画面是 canvas，用 computer use 操作时要申请全屏控制做真实点击。后台无障碍点击对 canvas 无效。

**预览**
1. 点工具栏的「预览」，面板选「二维码预览」页签。
2. 面板会记住「自动预览 → 启动手机端自动预览」，开着它时，每次上传都会直接推到用户手机上。实测有一次黑屏就发生在这种推送里：新包在手机上第一次打开，正是 B7 的情形。
3. 约 20 秒出码。码下面写着「将于 MM/DD HH:MM 失效」，实测约 25 分钟后失效。
4. 你截图并放大预览面板，裁出二维码发给用户；或者请用户直接扫开发者工具窗口里的码。
5. 用户扫过一次以后，可以从微信下拉的「最近使用」或者「小程序助手」再进开发版。

不要用这些方式做验收 `[实测-失败]`：
- **真机调试**：画面右下角有 vConsole 按钮，和用户看到的不一样；官方也要求用「预览」做真机验证（[来源](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/Transform.html)）。修 B7 之前，真机调试第一次打开就黑屏，日志里有 `still waiting runDependencies`，原因就是 B7。
- **Mac 端真机调试或 PC 端自动预览**：大约 1 分钟后游戏窗口自己关掉，Mac 微信弹「需在手机上完成登录」。

**插件版本**：开发者工具提示 UnityPlugin 可以从 1.3.14 升到 1.3.16 时，**不要升** `[实测-失败]`。升级后报 `gameManager.onLaunchProgress is not a function`，插件版本要跟着 SDK 走。

## 真机验证清单

请用户用预览码在手机上逐项确认，结果记进进度档案：

- [ ] 有声音，背景音乐和音效都有。
- [ ] 中文都正常显示，包括设置、日期、语言名这些运行时拼出来的字。
- [ ] 触摸、拖动都有响应，多指操作正常。
- [ ] 划掉微信再进，存档还在。
- [ ] 顶栏不被刘海和右上角胶囊挡住，胶囊左边的圆形「游戏圈」按钮也不挡。这个按钮只在真机上出现，开发者工具里看不到。
- [ ] 没有打不开的外链、评分、应用商店入口。
- [ ] 广告按钮在没有广告位 ID 时提示「暂时没有可播放的视频」，游戏不卡住。
- [ ] 主页显示已注册的中文游戏名，没有「优化建议」弹窗。
- [ ] 第一次打开不黑屏：先在模拟器里按第 10 节 B7 后面的步骤复现过。手机上再删掉微信「最近使用」里的这个小游戏，扫码打开一次，应该先出加载页，再进主页。手机上没黑屏不能证明没问题，资源包先到就看不出来，以模拟器复现为准。黑屏时不要靠「重新进入」过关：审核员只会打开一次。

## 10. Unity 6 补丁：症状 → 对应补丁

一共 12 处补丁：F1～F6 是 `fetch_wxsdk.sh` 改的 SDK 源码，B1～B5 和 B7 是 `ShipWeChatBuild.PatchFramework` 导出后改的 `webgl.wasm.framework.unityweb.js`。B0 和 B6 是导出配置，不算在 12 处之内。每一处都是在 Unity 6000.6 上碰到构建失败、真机黑屏或提审驳回后找到的 `[实测]`：B7 在模拟器里做过修复前后的对比，其余都在手机上验证过。升级 SDK 或 Unity 后再看到同样的症状，先查这张表。

| # | 症状 / 报错 | 补丁 | 在哪 |
|---|---|---|---|
| F1 | 编译错误 CS0619：`GetInstanceID` 已废弃（「Use GetEntityId instead」） | `WXRuntimeExtDef.cs` 改用 `GetHashCode()` | fetch_wxsdk.sh |
| F2 | emcc 报 undefined exported symbol `writeStackCookie` / `checkStackCookie` | 从 `WXConvertCore.cs` 的 EXPORTED_RUNTIME_METHODS 里删掉这两项，SDK 的 JS 不用它们 | fetch_wxsdk.sh |
| F3 | 插件用 `Module.FS` 读数据包失败；音频报 `HEAPU8` 未定义 | EXPORTED_RUNTIME_METHODS 加上 `FS`、`HEAP8`、`HEAPU8`、`HEAP16`、`HEAPU16`、`HEAP32`、`HEAPF32` | fetch_wxsdk.sh |
| F4 | 导出的 wasm 只有几十到一百多 KB（正常是几十 MB），说明引擎被链接器裁掉了；`SendMessage`、`ConnectToProfiler` 未定义 | `_main` 导出条件改成 `#if UNITY_2021_2_5 \|\| UNITY_6000_0_OR_NEWER` | fetch_wxsdk.sh |
| F5 | 手机上每次触摸都报 `Module._malloc is not a function`，SDK 的触摸、视频、socket JS 都要用到它 | `emscriptenArgs` 的 `,_main` 改成 `,_main,_malloc,_free` | fetch_wxsdk.sh |
| F6 | 开发者工具报「app.json 或自定义编译条件错误」，卡在编译 `game.json` | 模板 `project.config.json` 的 `"currentL"` 改成 `"current"` | fetch_wxsdk.sh |
| B0 | 黑屏，URP 着色器全部报「not supported on this GPU」 | `CompileOptions.Webgl2 = true`。不开时 SDK 选 GLES2 和 WebGL1 画布，而 Unity 6 已经没有 WebGL1 | ShipWeChatBuild（导出配置） |
| B6 | Unity 6000.6 新建的工程默认开启 WebAssembly 2023，64 位整数改用 BigInt 传给 JS。同样的开启状态在 TapTap 包的本机预检里让 `main()` 报 `Cannot convert a BigInt value to a number`；微信上没有单独验证 | 关掉 `PlayerSettings.WebGL.wasm2023` 和 `webAssemblyBigInt`，和实测在真机跑通的工程保持一致（两项都是关的） | ShipWeChatBuild（导出配置） |
| B1 | 音频、视频、输入法走了 Unity 自己的 Web Audio 路径，最后报 `alert is not defined` | 6000.x 生成的是 `var _JS_Sound_X=function(){...}` 写法，SDK 只替换 `function _JS_Sound_X(` 写法。改为把 `_JS_(Sound\|Video\|MobileKeyboard)_*` 都指向 `window.WXWASMSDK` | ShipWeChatBuild.PatchFramework |
| B2 | `alert is not defined`，Unity 的致命错误弹窗 | 加 `alert` 垫片，改成输出到 console.error | ShipWeChatBuild.PatchFramework |
| B3 | 导出 `ConnectToProfiler` 时报未定义 | 导出处先用 `typeof` 判断 | ShipWeChatBuild.PatchFramework |
| B4 | 音频回调里 `getWasmTableEntry` 未定义 | 回退到 `wasmTable.get(cb)` | ShipWeChatBuild.PatchFramework |
| B5 | 触摸回调要用 `Module.dynCall_viii`，6000.x 没有把它挂到 Module 上 | 把 `dynCalls` 里的 `dynCall_*` 全部挂到 Module | ShipWeChatBuild.PatchFramework |
| B7 | 新设备第一次打开一直黑屏，只剩右上角胶囊；再打开一次又正常，所以以前看起来像「安卓偶尔黑屏」。日志里有 `no boot config - using default values`，30 秒后有 `[PLUGIN ERROR] still waiting runDependencies, ids= []`。第一次提审就因此被驳回 | 插件把资源包解进文件系统以后，才调用 `removeRunDependency("wasm-preloadAssets")` 放行引擎。这个依赖本来由 SDK 加上：它改写 `addRunDependency("wasm-instantiate")`，编译完再调用 `Module.wasmInstantiated`。6000.6 的 emscripten 已经改成 `wasmExports=await (createWasm());run();`，这几条改写规则匹配不到，导出日志里显示为 `UnMatched WXReplaceRules`，引擎从不等资源包。补丁在这一句前加上依赖，编译完调用 `Module.wasmInstantiated`，并让 `monitorRunDependencies` 带上依赖名。找不到这一句时导出失败；SDK 的规则已经生效时（旧版 emscripten）跳过 | ShipWeChatBuild.PatchFramework（`WaitForDataPackage`） |

### B7：不用手机复现「第一次打开」

审核员用新设备第一次打开游戏。开发者自己的手机上有缓存，又常常是资源包先到，所以 B7 这类问题在自己手机上几乎看不到。`scripts/wx_cold_start.py` 在模拟器里造出「没有缓存、资源包下得慢」的第一次打开。目录按 macOS 版开发者工具写，其他系统用 `--root` 指定。

```bash
python3 <本 skill 目录>/scripts/wx_cold_start.py clear <AppID>                          # 删掉模拟器缓存的资源包
python3 <本 skill 目录>/scripts/wx_cold_start.py hold <工程>/Builds/wechat/minigame      # 让资源包晚到 12 秒
# 开发者工具里：菜单「工具 → 编译」
python3 <本 skill 目录>/scripts/wx_cold_start.py log <AppID>                            # 启动约 40 秒后看结论
python3 <本 skill 目录>/scripts/wx_cold_start.py release <工程>/Builds/wechat/minigame   # 预览、上传前一定要做
```

- 正常：模拟器先显示「正在加载资源（首次加载请耐心等待）」，约 12 秒后进主页；`log` 报 `OK`，插件日志里有 `no dependencies, invoke run`。
- 有问题：一直黑屏；`log` 报 `BROKEN`，30 秒后插件日志里有 `still waiting runDependencies`。
- 只查补丁在不在，不用 hold：随便启动一次再跑 `log`，结论一样。
- `hold` 改的是导出目录里的 `game.js`，`release` 后和导出的文件逐字节相同；重新导出也会覆盖它。上传前确认 `game.js` 里没有 `ship-hold`。
- 开发者工具要先登录。实测登录过期时（右上角显示「未登录」和登录二维码），点「工具 → 编译」后没有启动新的一次，`log` 读到的还是上一次启动。`log` 第一行会打印日志文件最后写入的时间，早于你点编译的时间就说明这次没启动。
- 实测情况：B7 修复前后的对比是用同样的手工步骤做的（删缓存、包一层 `wx.loadSubpackage` 让 `data-package` 晚到 12 秒），修复前黑屏，修复后先出加载页再进主页。脚本把这些步骤固化了下来；`hold`、`release`、`clear`、`log` 都对真实的导出目录和模拟器日志跑过，整套命令连起来在模拟器里还没跑过一轮。

### 升级 Unity 或 SDK 以后

- 导出日志里的 `UnMatched WXReplaceRules rule: ...` 表示 SDK 的改写规则在这个 Unity 版本上没匹配到。6000.6 上有 92 条，多数无关紧要，B7 就藏在里面。升级后先看和 `RunDependency`、`wasm-instantiate`、`doRun`、`callMain` 有关的几条。
- 日志只给出匹配串，替换成什么在 `wx-editor.dll` 里做了混淆。要看全文：写一个临时编辑器脚本，用反射读 `WeChatWASM.ReplaceRules` 的静态字段 `rules`（v0.1.34 有 133 条），打印每条的 `old` 和 `newStr`，用完删掉。
- UnityPlugin 的 JS 不在导出目录里。模拟器运行过一次以后，它在开发者工具的 Chromium 缓存里：`~/Library/Application Support/微信开发者工具/Partitions/*/Cache/Cache_Data`，键名以 `/plugin.js` 结尾的那一项。
- 每次升级后都按上一节复现一次第一次打开。

## 11. 版本号

- `PlayerSettings.bundleVersion` 会写进导出日志，方便对照。
- 后台显示的版本号，是上传时填的「版本号」，见 [review.md](review.md#上传开发版本)。两者保持一致，便于回溯。
