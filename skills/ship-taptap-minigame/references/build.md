# 工程改造与构建

本文对应 SKILL.md 的 P2。命令里的 `<skill>` 指本 skill 目录（`skills/ship-taptap-minigame`），`<工程>` 指用户的 Unity 工程根目录。

## 1. 选引擎路线

| 路线 | 依据 | 本仓库验证情况 |
|---|---|---|
| **A. Unity 6000.x** | 官方没有声明支持 Unity 6 | `[实测]`：Unity 6000.6 加 SDK 2.0.21，前 4 版逐个暴露问题没过准入测试，第 5 版（1.0.7）通过。`fetch_tapsdk.sh` 的 4 处编译补丁和 `ShipTapBuild.cs` 的运行补丁就是通过的那一套；游戏代码还要遵守第 4 节的规则。经过见 [troubleshooting.md](troubleshooting.md) 第 2 节 |
| **B. Unity 2022.3 LTS 或团结引擎 1.2.0+** | 官方原文：「建议使用 Unity 2022+ 版本，团结引擎 1.2.0+ 版本」[官方](https://developer.taptap.cn/minigameapidoc/dev/engine/unity-adaptation/unity-webGL/)。兼容性页的推荐表只有 2021（2021.1.* 及以上）、2022（推荐，2022.3.* 及以上）、团结引擎 WebGL（1.0.0 及以上）[官方](https://developer.taptap.cn/minigameapidoc/dev/engine/unity-adaptation/engine-suggestions/) | 未实测。`fetch_tapsdk.sh` 在 Unity 6 以下版本不打补丁；`ShipTapBuild.cs` 里 Unity 6 专用的后处理也会自动跳过。脚本在 2022.3 上能否编译还没试过 |

怎么选：
- **已有 Unity 6 工程**：走 A，不用降级。
- **新项目**：两条都行。A 有本仓库的实测；B 是官方支持范围。把这一点告诉用户，选择写进进度档案。

## 2. 把 SDK 嵌进工程：`fetch_tapsdk.sh`

```bash
bash <skill>/scripts/fetch_tapsdk.sh <工程>            # 默认拉 tag 2.0.21
bash <skill>/scripts/fetch_tapsdk.sh <工程> <tag>      # 指定版本
https_proxy=http://127.0.0.1:<代理端口> bash <skill>/scripts/fetch_tapsdk.sh <工程>   # GitHub 慢时走代理
```

脚本做的事 `[实测]`：
- 把 `https://github.com/taptap/minigame-sdk-unity`（可以用 `TAPSDK_REPO` 环境变量覆盖）浅克隆到 `<工程>/Packages/com.taptap.minigame`，并删掉其中的 `.git`。
- 发现 `Packages/com.qq.weixin.minigame`（微信 SDK）时直接退出：两个 SDK 都定义了 `WeChatWASM` 类型，放在一起会报 CS0433。
- 引擎是 Unity 6000.x 时，自动打 4 处补丁：
  1. 调试工具里的 `#if UNITY_6000_0_OR_NEWER` 改成 `TUANJIE_1_5_OR_NEWER`。不改的话，Unity 6 每次编译都报 CS1061。
  2. 去掉 `writeStackCookie`、`checkStackCookie`。新版 Emscripten 遇到它们会链接失败。
  3. 在 Module 上导出 `FS` 和各个 HEAP 视图。
  4. 导出 `_main`、`_malloc`、`_free`。
- 补丁没打上时会输出 `PATCH NOT APPLIED` 并以失败退出，通常是 SDK 升级后代码变了。输出 `already fixed upstream` 表示官方已经修好了这一处。遇到这两种输出，按 [MAINTAINING.md](../../../MAINTAINING.md) 核对。

为什么不直接用 Package Manager 的 git URL：Unity 自带的 git 不走系统代理，国内经常拉不下来；嵌进 `Packages/` 后，补丁也能和工程一起保留 `[实测]`。

接下来要做 `[实测]`：
- 把 `Packages/com.taptap.minigame/` 加进 `.gitignore`。换机器或重新检出工程后，再跑一次这个脚本。
- **渠道隔离**：TapTap 渠道单独开 git 分支或工作区。
  - 没有 TapTap SDK 的分支，在 Player Settings → Scripting Define Symbols 里加 `SHIP_NO_TAPTAP`，`ShipTapBuild.cs` 就不会参与编译。
  - 微信渠道的构建脚本对应的宏是 `SHIP_NO_WECHAT`。
  - 游戏代码里调用 TapTap 接口的地方，建议用 TapTap 分支专用的宏（例如 `TAPTAP`）包起来。

## 3. 导出脚本：`ShipTapBuild.cs`

1. 复制 `<skill>/scripts/ShipTapBuild.cs` 到 `<工程>/Assets/Editor/`。
2. 照 `<skill>/templates/build.json` 写 `<工程>/publish/taptap/build.json`：

| 字段 | 填什么 |
|---|---|
| `appid` | MiniApp ID（小游戏管理 → 开放能力 → 小游戏基本信息）。环境变量 `TAP_APPID` 优先于这里 |
| `version` | TapTap 渠道自己的版本号，例如 `1.0.0`。**必须比上一次点过「提交」的版本高**，上传时填的版本号要和它一致 |
| `orientation` | `Portrait`（竖屏）或 `Landscape`（横屏）。构建面板只提供这两个选项；枚举里还有 `LandscapeLeft`、`LandscapeRight` |
| `dst` | 导出目录，默认 `Builds/taptap` |
| `cjkFont`、`cjkFamily`、`latinFonts` | 中文字体子集和要挂回退的拉丁字体，见第 6 节。不用 uGUI 的 Text 时可以留空数组 |

3. 运行导出（编辑器开着同一个工程时会失败，先请用户关掉编辑器）：

```bash
<仓库>/skills/ship-my-game/scripts/unity_build.sh <工程> WebGL ShipTapBuild.Export taptap
```

脚本会做这些事 `[实测]`：
- 设 `bundleVersion = version`：游戏内显示的版本号和后台一致。
- 设 IL2CPP Release、Managed Stripping = Medium、异常 = ExplicitlyThrownExceptionsOnly、`stripEngineCode`，关闭 Unity 启动画面。
  - Unity 2022.3 个人版不允许关掉 Unity 启动画面（Unity 6 起个人版才可以关），所以这一项在 2022.3 个人版上可能不生效。这条是推断，未实测。
- 写 SDK 的转换配置（`Assets/TapTapMiniGame/Editor/MiniGameConfig.asset`）：
  - `assetLoadType = 1`：首包资源放在小游戏包内。TapTap 的 CDN 只对部分合作游戏开放 [官方](https://developer.taptap.cn/minigameapidoc/dev/dev-support/CDN/)。
  - 开 Brotli 压缩，开 WebGL 2，关掉性能建议弹窗。
- 调用 SDK 的 `TapTapBuildWindowHelper.BuildTapTapMiniGame` 导出 `game.zip` 和 `game_wasm_split.zip`。
- **只在 Unity 6000.x 上**改写导出物，再重新打包两个 zip。这就是 1.0.7 通过准入测试时用的那一套 `[实测]`：
  - 把 `Module.dynCall_vi/vii/viii/viiiii` 挂出来：SDK 的音频结束、触摸、视频要用到它们。
  - 把 `FS`、`IDBFS` 赋给 `unityNamespace`：插件写资源包、挂载 `/idbfs` 存档目录都靠它们。
  - 给 MEMFS 写入加 `HEAP8` 判空：资源包可能在 wasm 内存就绪之前写入。
  - 在 `createWasm()` 前添加运行依赖 `"wasm-preloadAssets"`：插件写完资源包会移除它，没有它 `main()` 会抢在资源前面运行。
  - `Module.SystemInfo` 改从 `unityNamespace.UnityLoader.SystemInfo` 读：小游戏不跑 Unity 的网页加载器。
  - `callMain` 结束后调用 `Module.calledMainCb`：插件靠它知道游戏已启动。
  - SDK 缺少的 `TJWASMSDK` 导入一律补占位函数：Unity 6 新增了 `JS_Sound_Create_With_Buffer` 等接口，缺一个 wasm 就链接失败。
  - 画布没有 id 时，Unity 的画布选择器改用 `"canvas"`；跳过 Safari 的 WebGL2 检查、复制粘贴初始化；没有 `crypto` 时用 `Math.random` 兜底；补 `alert`。
  - 在 `unity-namespace.js` 里给安卓注入 `instantiateWasm` 加载器：TapTap 安卓环境去掉了 `fetch`。

  每个锚点都要求在导出物里恰好出现一次，找不到就让构建失败，避免 SDK 或 Unity 升级后补丁悄悄失效。只有两处例外：功能没被链接进来时本来就不存在的锚点（复制粘贴、`crypto`），会在日志里记一句跳过；框架里没有 `dynCalls` 表时（开着 WebAssembly 2023 才会这样），改走 `getWasmTableEntry`，这条兜底路径没在设备上验证过。
- 导出前关掉「WebAssembly 2023」和 BigInt：实测跑通的包两项都是关的，Unity 6000.6 新建工程默认开启 WebAssembly 2023。日志里有 `[Ship] turned off PlayerSettings.WebGL.wasm2023`，见 [troubleshooting.md](troubleshooting.md) 2.4。

完成后看日志：
- 成功时有一行 `[Ship] taptap export=SUCCEED zip=… size=…MB version=… appid=…`。
- 失败时找 `[Ship] taptap export failed`，以及往上第一条 `error CS` 或 `Exception`。

增量构建本机约 35～80 秒，新工作区第一次要完整导入资源，会久很多 `[实测]`。

### 检查产物

```bash
unzip -l <工程>/Builds/taptap/game.zip | head -30    # 应有 game.js、game.json、unity-namespace.js、wasmcode/、data-package/ 等
unzip -p <工程>/Builds/taptap/game.zip game.json      # deviceOrientation 要和游戏方向一致
ls -l <工程>/Builds/taptap/game.zip                   # 看大小
```

参考：一个约 12 MB 资源的竖屏 2D 游戏，导出的 `game.zip` 是 14.0 MB，其中 wasm 代码包 7.24 MB（.br）`[实测]`。

## 4. TapTap 构建里不要调用微信适配层 `[实测]`

TapTap SDK 是微信 SDK 的分支，`WeChatWASM.WX` 在 TapTap 工程里也能编译，但运行时会出事：

- SDK 初始化完成后，JS 会把「Inited」消息依次广播给 `TapSDKManagerHandler` 和 `WXSDKManagerHandler`。
- 游戏只要在启动流程里调用过任何 `WeChatWASM.WX` 接口（存档、窗口信息、胶囊按钮……），就会创建微信的那个处理器；第二条「Inited」随即调用它从没设置过的回调。导出时关掉了空值检查（`ExplicitlyThrownExceptionsOnly`），空委托直接让 wasm 崩溃，第一帧就黑屏（1.0.6 的失败原因）。

做法：用渠道宏（例如 `TAPTAP`）把这些调用切到 `TapTapMiniGame.Tap` 的同名接口，例如 `StorageGetStringSync`、`StorageSetStringSync`、`GetWindowInfo`、`GetMenuButtonBoundingClientRect`。广告代码如果还走 `WX`，在填广告位 ID 之前改掉。导出前在代码里搜一遍 `WeChatWASM.WX`，TapTap 构建里应该一处都没有。

## 5. 合规界面和 TapTap 登录：`ShipCompliance.cs`

审核和行为规范的要求 [官方](https://developer.taptap.cn/minigameapidoc/tap-operation/operation-standards/behavior-standards/)：
- 第一次收集个人信息前弹窗征得同意，有「同意 / 不同意」，不得默认勾选（审核规范 8.1）。
- 游戏启动页要有适龄标识，以及《隐私政策》全文的入口按钮，按钮不小于 40×40 像素。
- 用户拒绝授权时，不得限制基础功能。
- 健康游戏忠告：TapTap 文档没有写明，属于行业惯例。

用法：
1. 复制 `<skill>/scripts/ShipCompliance.cs` 到 `<工程>/Assets/Scripts/`。
2. 复制 `<skill>/templates/compliance.zh-Hans.txt` 到 `<工程>/Assets/Resources/ShipCompliance/zh-Hans.txt`，替换所有 `{{占位符}}`：
   - `{{游戏名}}`、`{{开发者名称}}`（和厂商名一致）、`{{联系邮箱}}`、日期、适龄等级、玩法说明。
   - **只写游戏真实做了的事**：没接的登录、成就、排行榜、广告段落要删掉。
   - 改了隐私政策就把 `policy_version` 改成新日期，玩家会被重新询问。
3. 在启动场景里接上。下面是最小写法：只有在用户同意之后才初始化和登录。

```csharp
using UnityEngine;
#if TAPTAP && UNITY_WEBGL && !UNITY_EDITOR
using TapTapMiniGame;
#endif

public class ShipBoot : MonoBehaviour
{
    public Font cjkFont;              // make_cjk_font.py 生成的中文子集
    public RectTransform badgeRow;    // 启动页上放「8+ 适龄提示」和「隐私政策」两个按钮的一行
    public RectTransform footer;      // 启动页底部，放健康游戏忠告

    void Start()
    {
        ShipCompliance.Font = cjkFont;
        ShipCompliance.AgeRating = "8+";          // 和商店「适龄分级」一致
        ShipCompliance.LaunchBadges(badgeRow);
        ShipCompliance.HealthAdvice(footer);
        ShipCompliance.Run(agreed => { if (agreed) StartTapTap(); });   // 不同意也能继续玩，只是不登录
    }

    void StartTapTap()
    {
#if TAPTAP && UNITY_WEBGL && !UNITY_EDITOR
        Tap.InitSDK(_ =>
        {
            Tap.Login(new LoginOption
            {
                success = res => Debug.Log("[Tap] login ok"),   // res.code 5 分钟有效，没有服务器就不用存
                fail = err => Debug.LogWarning("[Tap] login failed: " + err.errMsg)
            });
        });
#endif
    }
}
```

- 状态：这套写法在真实游戏里编译通过，同意弹窗、文案加载都正常；界面还没上真机，也还没过审 `[实测：编译]`。
- `badgeRow` 的高度要让按钮在真机上不小于 40×40 像素。实测 116 个画布单位（参考分辨率 1080×1920）约等于 42 CSS 像素 `[实测]`。
- 设置页也要能再次打开隐私政策和适龄提示：调用 `ShipCompliance.ShowPolicy()`、`ShipCompliance.ShowAgeTip()`。
- 未确认：用户点「不同意」后不登录，是否满足审核规范 5.2.4「正确接入 TapTap 登录」。目前还没有过审结果。
- 未确认：`ShipCompliance` 用 `PlayerPrefs` 记住「已同意」。官方建议小游戏里不用 PlayerPrefs，改用平台存储 [官方](https://developer.taptap.cn/minigameapidoc/dev/tutorial/performance/runtime/unity-webGL-memory/)。真机上如果每次启动都重新弹窗，就是没存住。

## 6. 中文字体

WebGL 没有系统字体可以回退，中文会显示成方块 `[实测]`。生成字体子集：

```bash
python3 <仓库>/skills/ship-my-game/scripts/make_cjk_font.py \
  --font <NotoSansSC 可变字体.ttf> --out <工程>/Assets/Fonts/CJKSubset.ttf \
  --scan <工程>/Assets --text "<只在运行时出现的字>"
```

- 合规文案在 `Assets/Resources/ShipCompliance/` 里，`--scan Assets` 已经包含它。文案放在别处时，要另外 `--scan` 那个位置。漏扫的话，新加的字会显示成空白 `[实测]`。
- uGUI Text：把 `build.json` 的 `cjkFont` 填成子集字体的路径，`latinFonts` 填游戏用的拉丁字体。导出时脚本会给它们挂上中文回退。
- TextMeshPro：用子集做一个动态 `TMP_FontAsset`，加进 fallback 列表。
- 记得一起带上字体的 OFL 许可文件。

## 7. 包体大小

- **目标 20 MB 以内**。包体管理页和审核规范 5.2.2 写的是 20 MB；接入指南写首包压缩后小于 60 MB，20 MB 算优秀；上传框写小于 300 MB `[实测]` [官方](https://developer.taptap.cn/minigameapidoc/dev/engine/unity-adaptation/guide/)。
- 先看大头：`Builds/taptap/minigame/wasmcode/*.wasm.br` 是代码，`data-package/*.webgl.data.unityweb.bin.br` 是首包资源。
- 代码超过 6 MB 建议开 Wasm 分包，超过 10 MB 必须开 [官方](https://developer.taptap.cn/minigameapidoc/dev/tutorial/performance/launch/wasm-subpackage/)：
  - 上传 `game_wasm_split.zip`；
  - 在包体管理里「开启分包」；
  - 安卓、iOS 各用 2 台以上设备扫采集码，走遍新手和前几关；
  - 点「生成分包」，等 3～5 分钟后「提交」。

  本仓库没实测分包，一个实测包上传时页面提示「当前小游戏不支持分包」。
- 资源瘦身 [官方](https://developer.taptap.cn/minigameapidoc/dev/engine/unity-adaptation/guide/)：
  - 纹理用 ASTC 压缩，减小尺寸；
  - 去掉没用的资源和插件；
  - 首场景只放必需的内容。
- 用 AssetBundle、Addressables 远程加载时要有自己的 CDN，域名要做 ICP 备案并加进「域名配置」；单机小游戏一般不值得这样做。

## 8. 版本号

- 上传时填的「该包体对应版本号」是 `V x.y.z`，三个整数框，每个 0～999。只能用更高的版本号覆盖低版本，提审或发布的版本号不能和已上线、审核中的重复 `[实测]` [官方](https://developer.taptap.cn/minigameapidoc/quick-start/guide/creation-improvement/)。
- TapTap 渠道单独维护版本号，不要和安卓版共用 `[实测]`。
- 每次点过「提交」的上传都要换新号；上传了但没点「提交」就放弃的，不占用版本号 `[实测]`。
- 「该包体最低支持小游戏库版本」上传后由平台自动识别，不能改 `[实测]`。

## 9. 其他调试工具

以下来自官方文档，本仓库未实测：
- SDK 菜单「TapTap小游戏/Tools/Dev Test Tools」：在本机起一个服务（默认端口 8081）并生成二维码，手机和电脑在同一局域网里扫码运行 [官方](https://developer.taptap.cn/minigameapidoc/dev/engine/unity-adaptation/unity-tools/)。
- 「TapTap小游戏/Tools/调试工具」：勾选 Enable TapSDK Debug 后，在 Editor 里通过手机上的调试客户端调用 Tap 接口 [官方](https://developer.taptap.cn/minigameapidoc/dev/engine/unity-adaptation/unity-debug/)。
- 真机调试 [官方](https://developer.taptap.cn/minigameapidoc/dev/dev-support/debugging/)：
  - 安卓：`adb forward tcp:9229 tcp:9229`，再打开 `chrome://inspect`；
  - iOS：vConsole、macOS「控制台」里筛选子系统 `com.taptap.instantGame`，或用 Safari 网页检查器。

没有 PC 端模拟器。真正的「能不能跑」只能靠扫码自测和包体准入测试来判断。
