# 排查：现象 → 原因 → 解决

除特别标注外，下面都是 `[实测]`：在 Unity 6000.6 + SDK v0.1.34 + Stable 版开发者工具上碰到过，并按表里的办法解决了。Unity 6 补丁的完整对照表见 [build.md](build.md#10-unity-6-补丁症状--对应补丁)。

## 构建和导出

| 现象 | 原因 | 解决 |
|---|---|---|
| Package Manager 从 GitHub 拉 SDK 报 `SSL_ERROR_SYSCALL` `[实测-失败]` | Unity 内部的 git 不走终端代理 | 改用 `fetch_wxsdk.sh`，从 Gitee 镜像拉成内嵌包 |
| `fetch_wxsdk.sh` 输出 `PATCH NOT APPLIED`，退出码 1 | SDK 代码变了。通常是升级了 SDK，或者改了脚本里固定的版本 | 回到 v0.1.34；或者按 build.md 第 10 节逐条判断补丁是否还需要 |
| 切到 WebGL 后，Google Play 游戏服务、AdMob 相关脚本编译失败 | 这些插件只支持安卓 | 包进 `#if UNITY_ANDROID` |
| CS0619 `GetInstanceID`、emcc undefined symbol、wasm 只有几十 KB | Unity 6 和 SDK 不兼容 | 在 Unity 6 工程上重跑 `fetch_wxsdk.sh`（补丁 F1～F4） |
| 导出日志里没有 `framework patched` | 编辑器不是 6000.x，或者导出失败了 | 先看 `export=` 的结果；2022.3 和团结引擎本来就不需要这些补丁 |
| 导出失败，日志里有 `[Ship] framework patch expected 1x but found 0x: wasmExports=await (createWasm());run();` | 换了 Unity 版本，emscripten 生成的启动代码和 6000.6 不一样，B7 找不到下手的位置 | 不要删掉这个检查，否则包会在第一次打开时黑屏。按 build.md 第 10 节「升级 Unity 或 SDK 以后」找到新框架里启动和等待依赖的写法，改 `WaitForDataPackage`，再在模拟器里复现一次第一次打开 |
| 导出日志里有几十条 `UnMatched WXReplaceRules rule: ...` | SDK 的改写规则是照 Unity 2021、2022 的引擎代码写的，6000.6 上有 92 条匹配不到 | 多数无关紧要，和启动、依赖有关的由 B7 补上。升级 Unity 后按 build.md 第 10 节逐条看 |
| `Font not found` 或 `Missing ... (run make_cjk_font.py)` | build.json 里的字体路径不对，或者还没生成字体子集 | 先运行 `make_cjk_font.py`，再检查 `cjkFont`、`latinFonts` |
| Unity 异常退出，退出码 6，堆栈在字体设置附近 | 序列化字段名少了 `m_` 前缀 | 用仓库里的 `ShipWeChatBuild.cs`，它已经处理了 |

## 开发者工具和模拟器

| 现象 | 原因 | 解决 |
|---|---|---|
| 报 `UnityManager is not a constructor`；或者 game.js 打印「插件需要申请才可使用 / 请勿使用测试AppID」 `[实测-失败]` | 用了测试号或游客号，或者没开通快适配 | 换正式 AppID，开通快适配（[onboarding.md](onboarding.md)），重新导出 |
| 「插件未授权」「插件申请成功，待审核确认」 `[官方]`（[Unity QA](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/DevelopmentQAList.html)） | 没开通快适配 | 同上 |
| 「app.json 或自定义编译条件错误」，卡在编译 `game.json` | SDK 模板把 `"current"` 拼成了 `"currentL"` | 用打过补丁的 SDK（F6）；临时办法是在编译下拉里切回「普通编译」 |
| 重启工具后模拟器白屏，「预览」按钮是灰的 | 工具还在初始化 | 等大约 1.5 分钟 |
| 弹「模拟器长时间没有响应」 | 在编译 wasm | 关掉弹窗继续等 |
| 点击没反应，日志里有 `Object.defineProperty called on non-object`（weapp-adapter.js） | 适配层注入失败，偶尔出现 | 用菜单「项目 → 重新打开此项目」或者「工具 → 编译」 |
| 点工具栏的「编译」没生效 | 按坐标点击没点中 | 改用菜单操作 |
| 一次白屏时，模拟器的基础库比手机旧 | 也可能只是加载时间还没等够 | 在「详情 → 本地设置」把调试基础库改成和手机一致，再等一会儿 |
| 弹出「优化建议[可交互]」窗口 | SDK 的最佳实践检测，开发版和体验版会弹 | `ShipWeChatBuild` 已设 `showMonitorSuggestModal = false`；还在弹就说明是旧的导出，重新导出 |
| 自己写的触摸桥接导致只响应第一次点击，手机上报 `_malloc` `[实测-失败]` | 和适配层重复处理触摸，而且 6000.x 没导出 `_malloc` | 删掉桥接，依赖适配层自带的 DOM 触摸事件（见 [build.md](build.md#7-必改代码)） |
| UnityPlugin 升到 1.3.16 后起不来，报 `gameManager.onLaunchProgress is not a function` `[实测-失败]` | 插件版本和 SDK v0.1.34 不配套 | 回退到导出时的 1.3.14；以后不要点「插件有更新」 |
| `cli islogin` 报「工具的服务端口已关闭」 `[实测-失败]` | 服务端口默认关闭 | 请用户在「设置 → 安全设置」里打开；或者改用 miniprogram-ci，或者用 computer use 操作界面 |
| computer use 点模拟器没反应 | 后台无障碍点击对 canvas 无效 | 申请全屏控制，做真实的鼠标点击；拖动要用按下、移动、抬起的事件序列 |
| 接了广告后，控制台报 `SystemError (appServiceSDKScriptError) {"err_code":-1,"errMsg":"operateWXDataForAd:fail system apperror"}` `[实测]` | 模拟器自己的广告数据上报失败，栈全在 `WAGame.js` 里 | 可以忽略。游戏打的广告错误日志才要看 |
| 模拟器里出现了真实的广告 `[实测]` | 开发者工具用真实广告位 ID 拉到的就是真实素材 | 每种只看一两次，不点广告内容：自己制造曝光和点击是违规，见 [capabilities.md](capabilities.md#先读规范再接) |
| 想在自己电脑上看到审核员看到的第一次打开 | 自己的手机和模拟器里都有资源缓存，资源包也常常先到 | 用 `scripts/wx_cold_start.py` 删缓存、让资源包晚到 12 秒，再编译、看结论、还原，步骤见 [build.md](build.md#b7不用手机复现第一次打开) |
| 「工具 → 编译」后模拟器没变化，`wx_cold_start.py log` 显示的还是上一次启动的时间 | 实测那次开发者工具的登录已经过期：右上角显示「未登录」和登录二维码 | 请用户扫码重新登录，再编译 |

## 真机

| 现象 | 原因 | 解决 |
|---|---|---|
| 第一次打开一直黑屏，只剩右上角胶囊，再进一次又正常，以前记成「安卓偶尔黑屏」。日志里有 `no boot config - using default values`，30 秒后有 `[PLUGIN ERROR] still waiting runDependencies, ids= []`。提审驳回「需是一个完成品，要求可以打开、可以运行」、附图整屏黑的，就是这个 | Unity 6000.6 的引擎不等资源包就启动。资源包比引擎晚到时（新设备、网慢），引擎在空的文件系统上启动。见 build.md 第 10 节 B7 | 用当前的 `ShipWeChatBuild.cs` 重新导出，日志里要有 `[Ship] B7`；再用 `wx_cold_start.py` 在模拟器里复现第一次打开，确认先出加载页再进主页。「重新进入」只能临时恢复，审核员不会这么做 |
| 用真机调试时黑屏，预览却正常 `[实测-失败]` | 和上一条同一个原因：那次是新包第一次打开 | B7 修好后应该不再出现，还没在真机调试里复测。验收仍用「二维码预览」，官方也要求用预览，不用真机调试 |
| 推送到 Mac 微信后，约 1 分钟窗口自己关掉，弹「需在手机上完成登录」 `[实测-失败]` | Mac 微信的登录态被要求在手机上确认 | 不要用 Mac 端真机调试或 PC 端自动预览；截图改由用户在手机上截 |
| 预览面板开着「自动预览」，上传后直接推到用户手机上 | 面板记住了上次的选项 | 上传前切回「二维码预览」 |
| 预览码扫不开 | 码大约 25 分钟后失效 | 用户在场时再生成 |
| 中文显示成方块；设置里出现「语言：中」，日历缺字 | 没有字体子集；或者子集里缺少运行时才拼出来的字 | 运行 `make_cjk_font.py`，用 `--text` 把缺的字补进去，重新导出 |
| 没有声音 | 场景里没有 `AudioListener` | 每个场景放一个，见总入口的体检清单 |
| 顶栏被刘海、胶囊或游戏圈按钮挡住；第一次进入时顶部重叠 | `Screen.safeArea` 不避让这些；第一帧 `scaleFactor` 还没算好 | 按 [capabilities.md](capabilities.md#安全区和胶囊按钮) 用微信接口计算，用 referenceResolution 换算 |
| 关掉再进后，最后几步进度没了 | 关闭小游戏时不一定会触发 `OnApplicationPause` | 每步操作后立即用 `WX.StorageSetStringSync` 保存 |
| 「隐私政策」「给游戏评分」点了没反应 | 小游戏里不能打开外链和应用商店 | 在微信版里隐藏这些入口 |
| 下面这些日志 | 正常启动时也会出现 | 可以忽略：`animateCoverView fail`、带 `errMsg ok` 的 `reportUserBehavior`、URP 着色器被裁剪的警告。**`still waiting runDependencies` 不能忽略**：它说明引擎没等资源包（B7，依赖计数停在 -1）。以前在能进游戏的启动里也出现，只是因为那几次资源包恰好先到 |

## 后台（mp.weixin.qq.com）

| 现象 | 原因 | 解决 |
|---|---|---|
| 读页面只能读到左侧菜单 | 正文在 shadow DOM 或微前端里 | 用 JS 遍历 `shadowRoot` 取文字；或者截图、放大后再读 |
| 备案的上传框用 `find` 或文件上传工具都找不到 `[实测-失败]` | `input[type=file]` 藏在 shadow root 里，而且是隐藏的 | 用本机中转法，再用 `DataTransfer` 赋值，见 [filing.md](filing.md#小游戏备案) |
| 连续上传几栏截图，前面几栏的图被覆盖 `[实测-失败]` | 并行上传时，后传的覆盖了先传的 | 一次只传一栏，等缩略图出现再传下一栏 |
| 点「保存」提示「保存失败」 | MP 登录过期了（实测页面开了约 17 小时） | 请用户扫码重新登录；草稿还在。长流程要趁用户在场时做完 |
| 打开一个后台地址，显示「登录超时，请重新登录」，别的标签页却还登录着 `[实测-失败]` | 地址里的 token 是以前记下的，已经不是这次会话的 | 重新打开 mp.weixin.qq.com 首页，从当前地址里现读 token，见 [console.md](console.md#读后台的技巧) |
| 进流量主页面后什么都点不了 `[实测]` | 弹着「服务协议更新，请确认同意」 | 同意协议留给用户点，点完再继续 |
| 关掉一个过期的标签页，用户正在看的页面也没了 `[实测-失败]` | 关标签时把整个标签组都关了 | 只关自己新建的、确认无关的标签页 |
| 备案第 2 步的系统 1 是「货币系统」 | 这是默认值 | 游戏里没有货币，就改成实际的系统 |
| 小程序备案的「开始备案」、版本提审的「前往提交」是灰的 | 前面的步骤还没完成：资质、小游戏备案、微信认证 | 按 [filing.md](filing.md) 的顺序推进 |
| 小程序备案提交后，「基本设置」里还是「去完善」，流程指引页还是「开始备案」 `[实测]` | 这两处不显示审核状态 | 看首页「备案与认证」里小程序备案的标签，见 [filing.md](filing.md#小程序备案icp) |
| 隐私保护指引里「处理了用户信息」选不了 | 网络类型是单机 | 需要隐私接口时，先在「版本管理 → 网络类型设置」改成联网。备案前就要定好 |
| 调用隐私接口报 104、112、1025、1026 `[官方]`（[隐私合规开发指南](https://developers.weixin.qq.com/minigame/dev/guide/open-ability/privacy.html)） | 隐私指引里没声明这个接口，或者用户没同意 | 见 [capabilities.md](capabilities.md#隐私授权弹窗联网游戏才需要) |
| 流量主「开通」点不了 `[官方]`（[小游戏流量主开通流程](https://ad.weixin.qq.com/docs/237)、[广告变现](https://developers.weixin.qq.com/minigame/introduction/commercialization/ad/)） | 累计 UV 没超过 100（开放文档还写 500，见 [capabilities.md](capabilities.md#开通)），或者资格要第二天才刷新 | 上线后等用户量达标，第二天再试 |
| 想用接口提交审核，返回 87006 或 86000 `[官方]`（[提交代码审核](https://developers.weixin.qq.com/doc/oplatform/openApi/miniprogram-management/code-management/api_submitaudit.html)） | 提审接口只给第三方平台用，而且小游戏不能用它提交 | 在网页上提交，见 [automation.md](automation.md#不能自动化的环节) |
| 小游戏备案驳回，原因是「同名游戏已获批」 `[官方]`（[小游戏备案 Q4](https://developers.weixin.qq.com/minigame/introduction/guide/nrjs.html)） | 同名游戏已经先通过了主管部门审批 | 改名，新名字要和玩法相关；改名后小游戏备案要重新走 |
