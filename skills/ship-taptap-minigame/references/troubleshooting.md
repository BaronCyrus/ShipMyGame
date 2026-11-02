# 排查：现象 → 原因 → 解决

按阶段排列。`[实测-失败]` 是真实发布中失败过的做法，写了它为什么不行、该怎么做。

## 1. 包体准入测试没通过：先看日志

拿到日志：
1. 打开「包体管理 →（失败版本）查看报告」。
2. 在设备表里点失败设备的「下载日志」：先告诉用户文件名、来源、大小，用户同意后再下载。
3. 每台设备一个 zip，解压到 `publish/taptap/logs/<版本号>/<设备序号>/`。

按下面的顺序看 `[实测]`：

| 顺序 | 看什么 | 能说明什么 |
|---|---|---|
| 1 | 截图（每 2 秒一张，加上 `test_over.jpg`） | 画面停在 TapTap 加载页（例如「资源编译中」），说明 Unity 根本没启动；整屏空白、只有右上角胶囊按钮，说明连加载页都没出来 |
| 2 | `js_log.log` 里的 `[error]` 行 | JS 报错链。有时这个文件不存在 |
| 3 | `logcat.txt` | **宿主会把游戏的 console 输出镜像进 logcat，tag 是 `bridgeApm`**，所以没有 `js_log.log` 时，也能在这里看到 JS 日志。先找承载小游戏的进程（`Start proc …`、InstantGame 相关的 Activity），按它的 PID 过滤，只看加载开始之后的警告和错误。TapTap 主应用自己的插件框架日志、启动时的 `System.err` 都是噪音 |
| 4 | 报告里的性能数据 | 冷启动耗时有数值，但「初始化完成耗时」是 0.00 s，说明宿主在运行，引擎没起来 |
| 5 | `list_traversal_*.log` | 自动遍历测试的记录，里面有 OCR 文字。退出原因 `all_actions_ineffective` 表示点哪里都没反应 |

两种异常类型 `[实测]`：
- **「黑白屏」**：画面停在加载页，一般有 JS 日志可查。
- **「小游戏加载失败」**：启动后约 60 秒内没加载完就判为失败，常常连 `js_log.log` 都没有，只能看 logcat。

**修好后**：换一个更高的版本号，重新构建、上传、提交。准入测试每次都会换设备，不要只针对报告里的几台机型调 `[实测]`。

## 2. Unity 6 路线（实验性，尚未通过准入）

**结论**：Unity 6000.6 加 SDK 2.0.21，连续 3 版准入测试安卓都是 0/5 `[实测-失败]`。

**根因**：SDK 的转换工具只改写了 Unity 6000.6 框架代码（`webgl.wasm.framework.unityweb.js`）的一部分，编译、链接、运行三层都要补。

**仓库现状**：`scripts/fetch_tapsdk.sh` 带编译和链接层的补丁；`scripts/ShipTapBuild.cs` 只带到 1.0.4 那一版的运行补丁，1.0.4 本身没通过。下面的内容是给坚持用 Unity 6 的人的线索。默认请用官方支持的 Unity 2022.3 LTS 或团结引擎（见 [build.md](build.md) 第 1 节）。

### 2.1 编译和链接阶段（已在 `fetch_tapsdk.sh` 里）`[实测]`

| 现象 | 原因 | 解决 |
|---|---|---|
| 首次构建一分钟内失败，报 CS1061：`BuildProfile` 没有 `miniGameSettings` | SDK 的调试工具 `TapMiniGameConfigHelper.cs` 用了只有团结引擎 1.5+ 才有的接口，却用 `UNITY_6000_0_OR_NEWER` 判断版本 | 改成 `TUANJIE_1_5_OR_NEWER`（脚本补丁 1） |
| 链接失败，报 `writeStackCookie`、`checkStackCookie` | Unity 6 用的新版 Emscripten 不认这两个导出 | 从 `EXPORTED_RUNTIME_METHODS` 删掉（补丁 2） |
| 插件写资源包时找不到 `Module.FS`；音频读不到 HEAP | 6000.x 默认不再把 FS 和 HEAP 视图挂到 Module 上 | 导出 `FS`、`HEAP8`、`HEAPU8`、`HEAP16`、`HEAPU16`、`HEAP32`、`HEAPF32`（补丁 3） |
| 引擎代码被链接器剪掉；JS 调 `_malloc` 失败 | SDK 只在 2021.2.5 下导出 `_main`，而且追加的位置落进了错误的参数 | 在 `EXPORTED_FUNCTIONS` 前面插入 `_main,_malloc,_free`（补丁 4） |

### 2.2 运行阶段：三次上传的经过

| 版本 | 做了什么 | 准入结果 | 日志里的根因 |
|---|---|---|---|
| 1.0.3 | 只把 `Module.dynCall_vi/vii/viii/viiiii` 挂出来（SDK 的音频结束回调要用） | `[实测-失败]` 安卓 0/5「黑白屏」 | `wasm streaming compile failed: TypeError: fetch is not a function` → `both async and sync fetching of the wasm failed` → `Cannot read properties of undefined (reading 'createDataFile')`。TapTap 安卓的 `game.js` 把全局 `fetch` 置空了，而 6000.6 的 `createWasm` 只有在 `Module.instantiateWasm` 存在时才不走 fetch |
| 1.0.4（**`ShipTapBuild.cs` 里就是这一版**） | 增加：在 `unity-namespace.js` 里给安卓注入 `instantiateWasm`，用包内路径调 `WebAssembly.instantiate`；插件会把它拷进 Module | `[实测-失败]` 安卓 0/5：4 台「黑白屏」，1 台「小游戏加载失败」 | wasm 能从包内路径编译了，但链接失败：`LinkError: WebAssembly.instantiate(): Import #… module="env" function="JS_Sound_Create_With_Buffer": function import requires a callable`。插件还报 `reading 'createDataFile' of undefined`：插件读的是 `unityNamespace.FS`，但没人给它赋值 |
| 1.0.5（补丁不在仓库里） | 再补 8 处，见下 | `[实测-失败]` 安卓 0/5，换了一批设备，全部「小游戏加载失败」（60 秒内没加载完），性能测试没跑 | 见 2.3 |

1.0.5 在 framework 里加的 8 处（都没能让它通过）`[实测-失败]`：
1. 把 `FS`、`IDBFS` 也赋给 `GameGlobal.unityNamespace.FS` 和 `GameGlobal.unityNamespace.IDBFS`。插件写资源包、挂载 `/idbfs` 存档目录都要用它们。
2. `callMain` 的 finally 里设置 `Module.calledMainCb`。插件靠这个回调知道 main 已经运行；微信的转换工具会加，TapTap 的漏了。
3. SDK 缺失的 `window.TJWASMSDK._xxx` 导入一律补占位函数（返回 0，只警告一次），共 32 处。6000.6 新增、SDK 里真正缺的是 `JS_Sound_Create_With_Buffer` 和 `JS_MobileKeyboard_GetIgnoreBlurEvent`。
4. MEMFS 写入时给 `HEAP8` 加判空：资源包可能在 wasm 内存就绪之前写入。
5. `JS_Init_CopyPaste` 在 TapTap 宿主下直接返回（小游戏画布没有 id）。
6. `GL.createContext` 里针对 Safari 的 WebGL2 检查，在 TapTap 宿主下跳过。
7. 没有 `crypto.getRandomValues` 时用 `Math.random` 兜底。
8. 没有 `alert` 时改用 `console.error`。

另外：安卓加载器改成先调用 `wasmInstantiated()` 通知插件，再调用 `receiveInstance`；每个替换锚点都断言「恰好出现一次」，避免 SDK 升级后补丁悄悄失效。

### 2.3 1.0.5 的根因和 1.0.6 计划

以下是实测分析，还没验证：
- **根因**：插件在处理完资源包（`processRawData`）后，会移除运行依赖 `"wasm-preloadAssets"`，但 6000.6 的框架从来没有添加过这个依赖，计数被减成 -1。实际结果有两种说法：Unity 永远不启动；或者 `main()` 抢在资源包写进 FS 之前运行，约 30 秒后插件报 `still waiting runDependencies`。两种说法都是 Unity 没正常起来。
- **1.0.6 计划**（还没上传测试）：
  1. 在 framework 的 `return createWasm();` 前面加 `addRunDependency("wasm-preloadAssets")`。
  2. 补上 `Module.SystemInfo`：插件把它放在 `unityNamespace.UnityLoader.SystemInfo`，框架里拿不到。
  3. 修掉画布没有 id 时 `jsCanvasSelector` 抛错的问题。

### 2.4 上传前先在本机预检

用 node 写一个启动模拟脚本，能在上传前复现链接错误和运行依赖问题 `[实测]`。做法：
- 给 `GameGlobal`、`tj` 等全局对象打桩；
- 用导出物里真实的 wasm 和数据包；
- 执行包里真实的 `unity-namespace.js` 和打过补丁的 framework；
- 提供一个没有 id 的 canvas；
- 依次模拟写入资源包、挂载 idbfs、进入 main。

每次上传前跑一遍，比等 10 分钟的准入测试快得多。注意桩写得不对也会制造假错误：实测在 `InitWebGLPlayer` 里出现过内存越界，初步怀疑是桩的问题。这个脚本不在仓库里。

### 2.5 坚持走 Unity 6 时的建议

1. 每次只改一处，构建前跑本机预检，预检过了再上传。
2. 记好每版的补丁清单和准入结果，写进进度档案。
3. 让用户知道这条路可能要好几轮，每轮约 30 分钟加上等用户点「提交」的时间。实测从开始到第三次失败用了大半天。
4. 走通以后，把补丁改进 `ShipTapBuild.cs`，并把这一节改成 `[实测]`（见 MAINTAINING.md）。

## 3. 构建

| 现象 | 原因 | 解决 |
|---|---|---|
| `fetch_tapsdk.sh` 退出，提示先删掉 `Packages/com.qq.weixin.minigame` | 微信 SDK 和 TapTap SDK 都定义了 `WeChatWASM` 类型（CS0433） | TapTap 渠道单开分支或工作区 `[实测]` |
| `PATCH NOT APPLIED` | SDK 升级后代码变了 | 读新版 SDK 的 CHANGELOG 和对应文件，确认补丁是否还需要，按 MAINTAINING.md 更新 |
| Unity 自带的 git 拉不下 SDK | Unity 的 git 不走系统代理 | 用 `fetch_tapsdk.sh`，GitHub 慢时加 `https_proxy` 环境变量 `[实测]` |
| 报「Linear color space 需使用 Webgl2，终止导出」 | 线性色彩空间需要 WebGL 2 | Player Settings → Graphics APIs 只留 WebGL 2，或改用 Gamma [官方](https://github.com/taptap/minigame-sdk-unity) |
| `No MiniApp ID` / `Set version` | `publish/taptap/build.json` 缺字段 | 补上 `appid` 和 `version` |
| 中文显示成方块或空白 | WebGL 没有系统字体；字体子集漏扫了文案 | 重新运行 `make_cjk_font.py`，扫描范围要包括全部中文文案 `[实测]` |
| 游戏没有声音 | 场景里没有 `AudioListener` | 见 [project-check.md](../../ship-my-game/references/project-check.md) `[实测]` |
| `game.zip` 超过 20 MB | 资源或代码太大 | 见 [build.md](build.md) 第 6 节 |

## 4. 上传与后台

| 现象 | 原因 | 解决 |
|---|---|---|
| 小游戏页面全被弹窗挡住 | 《TapTap 小游戏开发者协议》更新了，要重新同意 | 讲清要点，由用户点同意 `[实测]` |
| 「小游戏开通文档（必看）」打不开 | 链接失效（404） | 不用管 `[实测]` |
| 浏览器工具传不了 14 MB 的 zip | 工具单次最多 10 MB | 用 CLI 上传，或者用[本机中转](../../ship-my-game/references/uploads.md) `[实测]` |
| 抽屉一直停在「正在上传包体，请勿离开当前页面」 | 在抽屉里点「上一步」换包，后台 `complete-upload-task` 返回 500 `[实测-失败]` | 「取消上传」→ 关掉抽屉 → 重新点「上传包体」 `[实测]` |
| 上传过的包在开发版本里找不到 | 抽屉开着时页面跳走，没提交的上传作废了 `[实测-失败]` | 需要去别的页面时另开标签页，传完当场「提交」 |
| 点「提交」被拒，提示 `Unrequested Commit in a Connected App` | agent 的权限系统不允许它在第三方应用里提交 | 不要设法绕过。停在按钮上，告诉用户在哪个标签页点哪个按钮 `[实测]` |
| 页面跳走时弹「Leave site?」 | 商店资料草稿页有离开拦截 | 内容是自动保存的；去别的页面另开标签页 `[实测]` |
| 「隐私保护及权限设置未填写」的黄条一直在 | 指引审核期间仍显示「未填写」 | 看指引页的状态是不是「审核中」，是就不用管 `[实测]` |
| 读页面文字的工具返回空 | 页面结构问题 | 在页面里执行 `document.querySelector('main').innerText` `[实测]` |
| 网址被工具打码成 `[BLOCKED: …]` | 输出里带查询串 | 先去掉 `?` 后面的部分再输出 `[实测]` |
| 成就表单按引用点「提交」，结果点到了「取消」 | 元素引用对不上 | 用 JS 读按钮坐标后按坐标点；上传图标后等 2 秒再填字 `[实测]` |

## 5. CLI

| 现象 | 原因 | 解决 |
|---|---|---|
| 用户说「链接你没发我啊」 | 链接写在了处理过程的中间，用户没看到 | 单独发一条消息，链接放在最后，并说明 5 分钟内有效 `[实测]` |
| `token_expired`：「等待授权超时」 | 设备链接 5 分钟内没授权 | 重新运行 `auth login --no-wait --json`，用新链接；旧的 `device_code` 不能再用 `[实测]` |
| `context_missing` | 没带 `--dev-id` / `--app-id`，也没保存上下文 | 补上参数，或者先 `app +select` [官方](https://developer.taptap.cn/v3/cli/command-reference.md) |
| 退出码 10 | 高风险写操作缺 `--yes` | 先 `--dry-run` 给用户看，用户同意后再加 `--yes` [官方](https://developer.taptap.cn/v3/cli/command-reference.md) |
| 上传时提示文件路径被拒 | 文件不在当前目录里 | `cd` 到 `Builds/taptap` 再传相对路径 `[实测]` |
| 包体列表里看不到准入测试结果 | CLI 不返回准入结果 | 去网页「包体管理 → 查看报告」`[实测]` |
| 旧版 CLI 报要求升级 | 服务端有最低客户端版本 | `taptap-cli update` [官方](https://www.npmjs.com/package/@taptap/cli) |

## 6. 能力接入

| 现象 | 原因 | 解决 |
|---|---|---|
| 排行榜报 1025 / 104 / 103 | 用了好友榜，但隐私指引没声明「好友关系」，或者用户没同意或没授权 | 改用公开榜（`LeaderboardCollection.PUBLIC`），或者在隐私保护指引里勾「好友关系」[官方](https://developer.taptap.cn/minigameapidoc/dev/tutorial/open-capabilities/leaderboard-tutorial/) |
| 排行榜报 500001 / 500002 | 排行榜 ID 写错，或者不属于这个游戏 | 从「排行榜 ID」列复制，不要用名称 [官方](https://developer.taptap.cn/minigameapidoc/dev/tutorial/open-capabilities/leaderboard-tutorial/) |
| 提交了更低的分数，排名没变 | 更新策略是「保留历史最高分」或「周期内最佳」 | 这是正常行为 |
| 成就回调 80002 / 80010 | 没登录或登录失效 | 用户同意隐私政策后先 `Tap.Login`，再解锁成就 [官方](https://github.com/taptap/minigame-sdk-unity) |
| 测试时成就、排行榜没有数据 | 测试账号不在测试名单里 | 把用户的 TapTap 用户 ID 加进成就「测试」页签和排行榜「测试白名单」`[实测]` |
| 成就发布按钮不可用 | 普通成就少于 5 个，或者还有在审核中的 | 至少 5 个都「已审核」再发布 [官方](https://developer.taptap.cn/docs/sdk/achievement/features/) |
| 云存档报 400001 | 上传太频繁 | 只在关键节点同步 [官方](https://developer.taptap.cn/minigameapidoc/dev/tutorial/open-capabilities/cloud-save-tutorial/) |
| 广告不显示 | 广告位 ID 错、没有广告填充、请求太频繁被限流、广告位没审核通过 | 先用测试广告位，并处理好加载失败；广告位 ID 为空时隐藏广告入口 [官方](https://developer.taptap.cn/minigameapidoc/dev/tutorial/open-capabilities/ad/ad/) |
| 「添加到桌面」在扫码自测时没反应 | 自测环境不支持这个功能 | 只能在线上版本里测 [官方](https://developer.taptap.cn/minigameapidoc/dev/tutorial/open-capabilities/desktop-folder/) |

## 7. 前置备案和审核

| 现象 | 原因 | 解决 |
|---|---|---|
| 备案申请被自动退回 | 初审后工信部发的短信，24 小时内没核验 | 重新提交；提前提醒用户留意短信 [官方](https://developer.taptap.cn/minigameapidoc/quick-start/guide/pre-filing/) |
| 提交备案时找不到包体可选 | 还没上传过任何开发版本 | 先上传一个版本（可以是 DEMO 包）`[实测]` |
| 审核失败 | 见驳回原因 | 按 [review.md](review.md) 第 4 节处理 |

## 8. 还没确认的事

把实际结果写进进度档案的「与 skill 不符」，回馈给本仓库。
1. Unity 6000.x 能不能通过准入测试：1.0.6 的计划还没验证。
2. 没接「添加到桌面」会不会卡审核。
3. 用户不同意隐私政策就不登录，算不算「正确接入 TapTap 登录」（5.2.4）。
4. 行为规范要求设置页提供账号注销入口；游戏没有自己的账号体系时，是否必须做。
5. 没有软著、「软件著作权人」填本人姓名，备案会不会被退回。
6. 备案时选了一个准入测试没通过的包体，有没有影响。
7. iOS 从来没在真机上测过。准入报告里 iOS 的「通过」只是占位。
8. 包体大小以 20 MB 还是 60 MB 为准：页面写 20 MB，接入指南写 60 MB。
9. 前置备案没通过时，能不能先提交商店审核。
10. CLI 的 `submit-mini-app-package` 建出的开发版本，准入测试和网页提交的有没有区别。
