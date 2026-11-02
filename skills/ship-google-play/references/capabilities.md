# 平台能力：Play 游戏服务、AdMob、评价与更新、内购

接哪个能力都按同一个顺序：后台配置 → Unity 代码 → 真机测试 → 同步数据安全申报和隐私政策 → 跟下一个版本一起送审。

**来源说明**：
- 后台配置部分 `[实测]`：2026-10 在两款游戏上做过。
- `scripts/ShipPlayGames.cs`、`ShipAds.cs`、`ShipPlayReview.cs` 是本仓库新写的：已经对 Unity 6000.3 和 2022.3 的引擎程序集以及插件源码编译通过，**还没在真机上运行**。实测游戏用的是同样接口的自写代码。

## 1. Play 游戏服务（PGS v2：自动登录、成就、排行榜、游戏存档）

插件：play-games-plugin-for-unity **v2.3.0**（2026-09-29；v2.2.0 起 minSdk 24）[官方](https://github.com/playgameservices/play-games-plugin-for-unity/releases)。v1（0.10.x）已弃用，新游戏不能用 [官方](https://developer.android.com/games/pgs/deprecation)。

- 下载前征得用户同意，下载后核对文件大小和 SHA-256。
- 用 **Assets > Import Package > Custom Package** 导入。工程里已有更新的 EDM4U 时，跳过包里自带的 EDM4U 1.2.182 `[实测]`。

### 1.1 入口和任务清单 `[实测]`

左侧导航「Play 游戏服务 → 设置和管理」下有：配置、成就、游戏统计数据、活动、排行榜、发布、测试用户数量、统计信息。帮助中心写的完整路径前面还有「扩大用户群」。

配置页（`games/configuration-summary`）显示「设置 Play 游戏服务：已完成 N 项任务，共 6 项」：
1. 请创建 Play 游戏服务项目
2. 在 Google Cloud Platform 中创建 OAuth 权限请求页面
3. 请创建凭证
4. 将 Play 游戏服务 SDK 添加到您的 APK
5. 请为您的项目添加测试人员
6. 请发布您的项目

### 1.2 PGS 项目、Cloud 项目、OAuth 同意屏幕

1. 页面 `games/setup` 的「您要使用哪个 Play 游戏服务项目?」，选「创建新的 Play 游戏服务项目」，再选「Cloud 项目 *」`[实测]`。
   - 提示原文：「每个云项目只能关联到 1 个 Play 游戏服务项目。您必须是云项目的“所有者”才能在此列表中看到它。」
   - 列表为空时点「创建新的云项目」，用同一个 Google 账号在新标签页打开 `https://console.cloud.google.com/projectcreate`，填项目名后点「创建」，约 8 秒建好。
   - 回到 Play 点「刷新 Cloud 项目」，选中刚建的项目，点「使用」。点「使用」表示同意 Play 游戏服务、GCP 和 Google API 的条款：你讲要点，由**用户点**。
2. 确认这个 Cloud 项目启用了 Google Play Game Services API（`gcloud services enable --project <云项目ID> games.googleapis.com`，或在 Cloud 的 API 库里启用）[官方](https://developer.android.com/games/pgs/console/setup)。
3. **OAuth 同意屏幕**（Google Auth Platform，`https://console.cloud.google.com/auth/overview?project=<云项目ID>`）`[实测]`：
   - 向导「应用信息 → 受众群体 → 联系信息 → 完成」：
     - 应用名称填商店名；
     - 用户支持邮箱填用户指定的邮箱；
     - 受众群体选「外部」；
     - 联系信息填用户指定的邮箱；
     - 勾选同意《Google API 服务：用户数据政策》（用户勾）；
     - 最后点「创建」。
   - 创建后是「测试」状态，测试用户数为 0。官方：测试状态最多 100 名测试用户，授权 7 天后过期 [官方](https://support.google.com/cloud/answer/15549945)。
   - 「发布应用」按钮一开始是灰的。先在「品牌塑造」页填好：应用首页（开发者网站）、应用隐私权政策链接、「已获授权的网域」（开发者网站的域名）、开发者联系信息。**不要上传应用徽标**，原文「上传徽标后，您需要提交您的应用进行验证…」。
   - 然后在「目标对象」页点「**发布应用**」：**由用户点**。实测 agent 的点击被权限系统拦下（账号级设置变更），用户点后状态变成「正式版」，没有要求额外验证 `[实测-失败（agent）/ 实测（用户）]`。
   - PGS 要求的权限范围是 `games`、`games_lite`、`drive.appdata`，都不需要应用验证。官方建议尽早发布同意屏幕，否则公众用户用不了任何 PGS 功能 [官方](https://developer.android.com/games/pgs/console/setup)。

### 1.3 凭据和证书指纹

**每个会用到的签名证书，都要有一个 Android OAuth 客户端和一条 PGS 凭据**，包名相同。证书指纹或包名不对，是登录失败最常见的原因 [官方](https://developer.android.com/games/pgs/android/troubleshooting)。

| 安装来源 | 签名证书 | SHA-1 从哪拿 |
|---|---|---|
| 从 Play 安装（任何测试轨道和正式版） | Play 应用签名密钥 | 应用签名页（[console.md](console.md) 第 7 节），用剪贴板接管读「复制」按钮 |
| 本地安装 release 包 | 上传密钥 | `make_upload_keystore.sh` 的输出，或 `keytool -list -v -keystore <keystore> -alias upload` |
| 调试构建 | 调试密钥 | `ShipAndroidBuild.Apk` 不用自定义密钥，用的是默认调试 keystore（通常是 `~/.android/debug.keystore`）：`keytool -list -v -keystore ~/.android/debug.keystore -storepass android` [官方](https://developer.android.com/games/pgs/console/setup) |
| 内部应用分享 | Play 生成的内部测试证书 | 「测试和发布 → 内部测试 → 内部应用分享 → 上传者和测试人员 → 内部测试证书」[官方](https://support.google.com/googleplay/android-developer/answer/9844679?hl=zh-Hans) |

**量子就绪型混合签名**：
- 2026 年起新应用默认启用。Google 持有三把密钥：给 Android 17+ 用的经典密钥和后量子密钥，以及给旧系统用的经典密钥。官方要求把**三把的指纹都登记**到 API 提供方 [官方](https://support.google.com/googleplay/android-developer/answer/9842756?hl=zh-Hans)。
- 实测的应用签名页上有「应用签名密钥 — 正在使用」和「之前的应用签名密钥 … 量子就绪（Beta 版）」两块，「复制所有证书指纹」会给出多个 SHA-1，包括量子就绪密钥的两个 `[实测]`。
- 实测两款游戏都只建了两条凭据：「正在使用」那把应用签名密钥和上传密钥。**PGS 要不要为量子就绪的密钥另建凭据未确认**；在真机上从测试轨道装好后的登录结果也没有记录。
- 稳妥做法：「复制所有证书指纹」里的每个 SHA-1 都建一条凭据；把「正在使用」那把设为「用于新安装应用」；分别在 Android 17 以下和 Android 17 及以上的真机上验证登录。

**建一条凭据**（`games/configuration-credentials` →「添加凭据」）`[实测]`：
1. 「类型 *」选 Android。
2. 「名称 *」（上限 30）改成商店里的游戏名。原文说用户会在「Google 我的活动记录」和 Play 游戏应用里看到这个名称。
3. 「启用反盗版功能 *」选「关闭」。官方：开启后，已发布游戏的非 Play 安装（例如 adb 侧载）调用 PGS 一律失败，返回 `LICENSE_CHECK_FAILED` [官方](https://developer.android.com/games/pgs/android/anti-piracy)。
4. 点「创建 OAuth 客户端」，弹窗「如何创建 OAuth 客户端」里直接列出类型、名称、指纹（Play 应用签名密钥的 SHA-1）和软件包名称。应用上传过第一个 AAB 后才有这个指纹。
5. 在 Cloud 的 `https://console.cloud.google.com/auth/clients/create?project=<云项目ID>`：「应用类型 *」选 Android，填「名称 *」「软件包名称 *」「SHA-1 证书指纹 *」→ 创建。原文提示「设置可能需要 5 分钟到几小时才会生效」。
6. 回到 Play 点「刷新 OAuth 客户端」，在「OAuth 客户端 *」里选中刚建的客户端 → 保存。第二条及以后的凭据有「用于新安装应用」选项：选「正在使用」的那把应用签名密钥。官方：只能有一条凭据开启这个选项。
7. 还没上传过 AAB 时，先只建上传密钥那条；首个 AAB 上传后，再补应用签名密钥的那条。
8. 凭据保存后，配置页会显示数字形式的应用 ID，资源 XML 里会用到。
- 有后端、需要 server auth code 时，另建一条「游戏服务器」凭据（Web 应用 OAuth 客户端），把它的客户端 ID 填进插件的 Android Setup [官方](https://developer.android.com/games/pgs/unity/unity-start)。

### 1.4 属性和翻译 `[实测]`

- 配置页「修改属性」：显示名称（和商店一致）、说明、类别、游戏图标（512×512，≤1 MB）、置顶大图（1024×500，≤1 MB）。
- 发布前要填完；测试阶段只需要显示名称 [官方](https://developer.android.com/games/pgs/console/setup)。
- 「管理翻译 → 添加或移除语言」：一次用 JS 按语言代码勾选成功了；另一次同样的对话框不认 JS 点击，只能在搜索框里逐个输入语言代码再勾选 `[实测-失败 → 已修复]`。然后切换语言下拉，逐个填名称和说明。
- **游戏存档**：「修改属性」里把「游戏存档」设为开启 → 保存，最长 24 小时生效 [官方](https://developer.android.com/games/pgs/console/enable-features)。实测游戏没开。

### 1.5 成就：用 ZIP 批量导入

1. 你来设计成就。官方质量清单把「至少 10 个可见成就」列为 Required，并要求至少 4 个在第一小时内能稳定达成 [官方](https://developer.android.com/games/pgs/quality)。
   - 旧文档写过「至少 5 个才能发布」，现行页面已经改了。控制台是否会硬性拦截少于 10 个的情况未确认，**按至少 10 个做**。
   - 实测做了 10 个和 11 个 `[实测]`。
2. 照 `templates/achievements.example.json` 写 spec，准备 `<key>.png` 图标（512×512，所有语言共用，不要带文字，显示时会被裁成圆形），然后：
   ```bash
   python3 scripts/pgs_achievements_zip.py publish/google-play/achievements.json publish/google-play/achievement-icons publish/google-play/achievements.zip
   ```
   - 脚本会检查：英文逗号（CSV 没有转义）、名称 ≤100、说明 ≤500、点数是 5 的倍数且在 5～200 之间、图标是否齐全；输出无表头的三个 CSV 加图标。
   - 脚本把总点数限制在 1000 以内；官方现行上限是 2000 [官方](https://developer.android.com/games/pgs/achievements)。
   - 本地化里的语言要先在 PGS「属性」里加好，默认语言不能写进本地化文件。
3. 「Play 游戏服务 → 设置和管理 → 成就 →「导入成就」」，上传 ZIP →「保存为草稿」`[实测]`：11 个成就、209 条译文、14 个文件全部识别。
4. 一张图里画多个徽章再切图时，先渲染一张「名称 + 图标」的核对图，确认没错再用 `[实测]`。
5. 发布后成就**不能删除**，类型和初始状态也不能改 [官方](https://developer.android.com/games/pgs/integrate-achievements)。

### 1.6 排行榜（逐个手工建）`[实测]`

「Play 游戏服务 → 设置和管理 → 排行榜 →「创建排行榜」」：

| 字段 | 填法 |
|---|---|
| 名称 *（0/100） | 排行榜名 |
| 格式 | 数字（可设小数位数、自定义单位）/ 时长（毫秒）等 |
| 图标 | 「JPEG 或 32 位 PNG，512 x 512 像素，大小上限为 1 MB」 |
| 排名顺序 | 「高分排名靠前 / 低分排名靠前」。原文「排行榜发布后，您将无法更改此设置」。用时榜选低分靠前 |
| 防篡改功能 | 开启 |
| 限值 | 可选的最低、最高得分，发布后不能改 |
| 列表排序 | 显示顺序 |

- 点「保存为草稿」。每个游戏最多 70 个排行榜 [官方](https://developer.android.com/games/pgs/leaderboards)。
- **多语言名称**：顶部的语言下拉要先用真实点击打开，选项才会渲染出来；然后用下面的帮助函数选语言并填名称。每次页面跳转后要重新定义这个函数。每批只做 3～4 种语言，批次太长浏览器扩展会断线。5 个榜 × 19 种语言共用了约 23 分钟。
  ```js
  window.__setLoc = async (code, name) => {
    const sleep = ms => new Promise(r => setTimeout(r, ms));
    const opt = [...document.querySelectorAll('[role=option], material-select-item')]
      .find(o => (o.innerText || '').trim().endsWith(' - ' + code));
    if (!opt) return 'no opt ' + code;
    opt.click(); await sleep(900);
    const inp = [...document.querySelectorAll('input')].find(i => i.offsetParent && i.getAttribute('aria-label') === '名称');
    inp.focus(); inp.value = name;
    inp.dispatchEvent(new Event('input', { bubbles: true })); inp.dispatchEvent(new Event('change', { bubbles: true })); inp.blur();
    return code + ' => ' + inp.value;
  };
  ```
- 页面布局偏移时，按坐标点击会落在输入框外，名称没填进去 `[实测-失败]`：每次都读回输入框的值核对。

### 1.7 测试人员 `[实测]`

- 左侧导航「测试用户数量」，页面标题「Play 游戏服务测试人员」→「添加发布轨道」→ 勾选封闭测试轨道（alpha）→「添加轨道」。
- 原文：「您需要先将应用发布到相应轨道，然后测试人员才能访问您的游戏项目」。
- 也可以按邮箱逐个添加测试人员 [官方](https://developer.android.com/games/pgs/console/publish)。
- **配置发布前只有测试人员能登录**；创建游戏的账号不会自动成为测试人员，要把用户自己的账号也加进来 [官方](https://developer.android.com/games/pgs/android/troubleshooting)。

### 1.8 接入 Unity

1. 凭据区点「获取资源」→「Android(XML)」标签 → 复制，存成 `publish/google-play/games-ids.xml`。里面有 `app_id`、`package_name` 和每个成就、排行榜的 ID。
2. 运行 `ShipPgsSetup`，生成 `GPGSIds.cs`、清单库和 `PlayGamesSettings.asset`，见 [build.md](build.md) 第 5 节。`mainTemplate.gradle` 会自动加上 `play-services-games-v2` 和 `play-services-nearby` `[实测]`。
3. 把 `scripts/ShipPlayGames.cs` 复制到 `Assets/Scripts/`，挂到首个场景的常驻物体上：
   - 启动时自动取回登录结果（v2 在游戏启动时已自动尝试登录）。`IsSignedIn` 为 false 时，显示「用 Play 游戏登录」按钮，按钮调 `ManualSignIn()`。
   - 成就：`Unlock(GPGSIds.achievement_xxx)`；增量成就用 `Increment` 或 `SetStepsAtLeast`；`ShowAchievements()` 打开系统界面。
   - 排行榜：`SubmitScore(GPGSIds.leaderboard_xxx, 分数)`（低于玩家最好成绩的分数，服务器会自动丢弃）；`ShowLeaderboard(id)`。
   - 游戏存档：`SaveToCloud(槽位, json, 累计时长)`、`LoadFromCloud(槽位, 回调)`。单个存档 ≤3 MB [官方](https://developer.android.com/games/pgs/savedgames)。只在关卡结束、暂停等关键节点存，不要每帧存。
   - 好友：`LoadFriends(回调)`；返回 -1 时会弹出系统的分享好友列表授权。
   - 所有 PGS 调用都包在 `#if UNITY_ANDROID` 里，其他构建目标（例如小游戏渠道的 WebGL）编译成空操作。
4. 不想让没有 PGS 资料的用户一启动就被弹出建档界面：在 `AndroidManifest.xml` 的 `<application>` 下加 `<meta-data android:name="com.google.android.gms.games.SUPPRESS_GAME_PROFILE_CREATION" android:value="true"/>`（插件 ≥2.1.0）[官方](https://developer.android.com/games/pgs/unity/unity-start)。
5. 官方文档的示例大量用 `Social.*`，但 Google 在插件仓库里的 Level Up skill 写着 Unity Social API 在 v2 下「deprecated」，要避免 [官方](https://github.com/playgameservices/play-games-plugin-for-unity/blob/master/skills/level-up-achievements/SKILL.md)。本仓库统一用 `PlayGamesPlatform.Instance`。

### 1.9 发布 PGS 配置（用户点）

- 「Play 游戏服务 → 设置和管理 → 发布」（页面 `games/publication`）：配置、成就、排行榜都显示「可以发布」后，点右上角「发布」。
- 确认框原文：「要发布游戏项目吗？所有用户都将可以使用您设置的 Play 游戏服务功能。发布过程可能需要几个小时。游戏项目发布之后，其中的某些部分便无法再修改，成就、活动和排行榜均不可删除。」→「发布游戏项目」。
- **由用户点**：实测 agent 的点击被权限系统拦下 `[实测-失败（agent）/ 实测（用户）]`。你把页面停在这里，告诉用户为什么要点：不发布的话，只有测试人员能登录。
- 官方：变更最长 2 小时生效，**游戏上架正式版前至少 2 小时就要发布**；PGS 配置要单独发布，它和发布 APK 是两回事 [官方](https://developer.android.com/games/pgs/console/publish)。
- PGS 的改动跟着游戏版本一起送审：实测「3 (1.0.2) + 数据安全」两项在 38 分钟内过审 `[实测]`。

### 1.10 同步数据安全和隐私政策 `[实测]`

- 数据安全：第 2 步「用户是否可以使用在应用之外创建的账号登录」改成「是 / 其他」；第 3 步加「名称、用户 ID、其他操作」，见 [console.md](console.md) 第 2.6 节。
- 隐私政策：保留 `templates/privacy-policy.en.html` 里的 Google Play Games 一节，在审核结束前发布上线。

### 1.11 测试和常见坑

- 真正的登录、成就、排行榜只能在真机上验证，而且要从测试轨道安装，交给用户做。用户的账号要同时在 PGS 测试人员和封闭测试名单里。
- 模拟器没登录 Google 账号时，日志是 `sign-in: Canceled`，游戏不会崩溃 `[实测]`。
- 排查时打开详细日志：`adb shell setprop log.tag.Games VERBOSE` 后看 `adb logcat`；用完恢复为 `INFO` [官方](https://developer.android.com/games/pgs/android/troubleshooting)。
- 资源里的 `app_id` 只能是数字项目 ID，不能是完整的客户端 ID。
- 游戏已上线但 PGS 配置没发布：非测试账号全部登录失败。
- Minify（R8 / ProGuard）打开时，按插件 README 把它的 proguard 配置合并进 `Assets/Plugins/Android/proguard-user.txt`。
- Google 在插件仓库里还发布了给 agent 用的成就、云存档 skill：`skills/level-up-achievements`、`skills/level-up-cloud-save`。想参加自愿的 Level Up 计划时对照它们 [官方](https://github.com/playgameservices/play-games-plugin-for-unity/tree/master/skills)。

## 2. AdMob

### 2.1 版本和安装

- Google Mobile Ads Unity 插件 **v11.6.0**（2026-10-13）：最低 Unity 2022.3 LTS，最低 Android API 24，GMA Android SDK 25.5.0，支持 Unity 官方 EDM 包 [官方](https://github.com/googleads/googleads-mobile-unity/releases)。
  - 官方快速入门页还写着「Unity 2019.4+」，与 Release Notes 冲突，以 Release Notes 为准。
  - 实测用的是 11.5.0。
- 安装（三选一，不要混用）[官方](https://developers.google.com/admob/unity/quick-start)：
  - `openupm add com.google.ads.mobile`；
  - 在 `Packages/manifest.json` 加 scopedRegistries（`https://package.openupm.com`，scope `com.google`）和依赖 `"com.google.ads.mobile": "11.6.0"`；
  - 从 GitHub Releases 下载 `.unitypackage` 导入。
- `.unitypackage` 版插件的 meta 文件格式偏旧（PluginImporter serializedVersion 1），实测要升级到 v3 格式 `[实测]`。

### 2.2 应用和广告单元 `[实测]`

1. 「应用 → 添加应用」（`admob.google.com/v2/apps/create`）：
   - 平台选 Android；
   - 「此应用是否已在受支持的应用商店中上架？」选「**否**」（封闭测试的应用不公开，关联不了）；
   - 「应用名称」上限 80；
   - 点「添加应用」→「完成」。
2. 进入应用 →「广告单元」→「选择广告格式」→ 在对应格式下点「选择」→ 填「广告单元名称」→「创建广告单元」。
   - 分清「插页式广告」「激励广告」「插页式激励广告」。
   - 激励广告的奖励数量和奖品名保持默认即可（必须是整数）。
   - 实测建了横幅、插页式、激励三个，共约 6 分钟。
3. 正式 App ID、广告单元 ID 只记进进度档案（或用户的私密备忘），**封闭测试期间不放进包里**。

### 2.3 测试 ID（Google 公开测试值）

官方：https://developers.google.com/admob/unity/test-ads 。以那一页的表格为准；激励、横幅页开头代码块里写的是插屏的 ID，不要照抄。

| 用途 | 值 |
|---|---|
| 示例 App ID（填进 GoogleMobileAdsSettings） | `ca-app-pub-3940256099942544~3347511713`（[Android 快速入门](https://developers.google.com/admob/android/quick-start) 的清单注释） |
| 激励 | `ca-app-pub-3940256099942544/5224354917` |
| 插屏 | `ca-app-pub-3940256099942544/1033173712` |
| 锚定自适应横幅 | `ca-app-pub-3940256099942544/9214589741` |

- 封闭测试期间一律用测试 ID `[实测]`。原因有两个：测试者为了「帮忙」点广告会被判为无效流量；新账号审核期间，真实 ID 的请求返回 `Account not approved yet` `[实测]`。
- 想在自己的设备上看真实广告，先在 AdMob「设置 → 测试设备」里添加这台设备（15 分钟到 24 小时生效），或在代码里设 `RequestConfiguration.TestDeviceIds` [官方](https://developers.google.com/admob/unity/test-ads)。

### 2.4 Unity 代码：`scripts/ShipAds.cs`

1. 复制到 `Assets/Scripts/`，挂到首个场景的常驻物体上。流程：
   1. 每次启动 `ConsentInformation.Update()`；
   2. `LoadAndShowConsentFormIfRequired()`；
   3. `CanRequestAds()` 为 true 后再 `MobileAds.Initialize()`；
   4. 预加载激励和插屏广告。
2. 文件顶部的三个正式广告单元 ID 测试期**留空**，留空时自动用测试 ID。正式版时再填，并把 App ID 一起换掉。
3. 给游戏用的接口：
   - `RewardedReady` 为 false 时，隐藏所有「看广告领奖励」入口（没有广告、没同意、账号审核中都会是 false）`[实测：同样的处理]`；
   - `ShowRewarded(amount => 发奖励)`：发奖励的回调已经切回主线程；
   - `ShowInterstitial()`：只在关卡结束这类自然停顿处调用；
   - `ShowBanner()` / `HideBanner()`；
   - `PrivacyOptionsRequired` 为 true 时，在设置页显示「隐私选项」按钮，按钮调 `ShowPrivacyOptions()`，这是用户撤回同意的入口。
4. 广告回调在后台线程上触发。实测直接在回调里操作 Unity 对象会出 `TypeInitializationException` `[实测-失败 → 已修复]`。所有回调都用 `MobileAdsEventExecutor.ExecuteInUpdate` 派回主线程。`MobileAds.RaiseAdEventsOnUnityMainThread` 在 11.x 源码里已标为过时。
5. 加载失败不要在回调里立刻无限重试；广告缓存超过 1 小时会过期 [官方](https://developers.google.com/admob/unity/rewarded)。

### 2.5 隐私消息（UMP）

**欧洲法规（GDPR）**：「隐私权和消息 → 欧洲法规 → 创建」`[实测]`，约 12 分钟：
1. 改消息名；选应用；填隐私权政策网址。
2. 「其他语言」下拉共 32 种，实测除英语外全选（31 种）。
3. **「不同意」按钮必须明确设置，否则「发布」一直是灰的**：打开「不同意」设置，用表头开关一次对所有国家开启，再确认。
4. 「样式」里打开图标开关（关联商店后会自动显示应用图标）。
5. 「定位」保持默认的「须遵守 GDPR 的国家/地区（EEA、英国和瑞士）」。原文：「选择“所有国家/地区”仅适用于已集成完整 UMP SDK 的应用。」
6. 点「发布」。确认框说明消息「最多可能需要 1 小时才会显示在应用内」。
- 消息绑定的是正式 App ID，用测试 App ID 的包可能不会弹出（推断）。要测试弹窗，用 `ConsentDebugSettings` 的 `DebugGeography = DebugGeography.EEA` 加测试设备哈希 ID [官方](https://developers.google.com/admob/unity/privacy)。

**美国州级法规**：同一入口 →「美国州级法规」卡片 → 创建 → 默认语言（英语或西班牙语）→ 打开「All current and future supported US states」→ 发布 [官方](https://support.google.com/admob/answer/10860309?hl=zh-Hans)。本仓库未实测。

### 2.6 app-ads.txt

1. 文件内容用 AdMob「应用 → 查看所有应用 → app-ads.txt →『如何设置 app-ads.txt』」里给出的那一行。实测形如 `google.com, pub-<发布商ID>, DIRECT, f08c47fec0942fa0`，最后一段是 AdMob 片段里自带的固定值 `[实测]`。
2. 放在 **Play「商店设置」联系信息里那个网站的域名根目录**：`https://<域名>/app-ads.txt` [官方](https://support.google.com/admob/answer/9363762)。
   - 爬虫会去掉 `www.` 和 `m.`，只往上找一级子域名。
   - 必须返回 HTTP 200，robots.txt 不能拦截。
   - Google Sites 做的网站不行。
3. GitHub Pages：**用户站点 `<用户名>.github.io` 的根目录可以用**；项目站点在 `/<仓库名>/` 下，不是根目录，不行 `[实测]`。
   - 免费账号的私有仓库不能开 Pages。实测用户付费升级后改成私有仓库，Pages 照常工作。
   - 推送后约 20 秒，`curl https://<域名>/app-ads.txt` 返回 200 `[实测]`。
4. 推送到用户的网站前先征得用户同意 `[实测]`。

### 2.7 关联商店，正式上线后（官方，未实测）

1. 正式版公开上架后：在 AdMob「应用 → 查看所有应用 →『商店』列 → 添加商店」，或在「应用设置 → 应用商店 → 添加商店」，按包名或商店网址搜索 → 添加 → 继续 → 添加应用。
   - 新上架的应用要 24～48 小时、最长一周才能搜到。
   - **不公开的 Google Play 应用不能关联** [官方](https://support.google.com/admob/answer/10037806?hl=zh-Hans)。
2. 「应用设置 → 验证应用 → 检查是否更新」：先通过 app-ads.txt 验证，然后进入「应用就绪审核」，通常 2～3 天。审核期间广告投放受限 [官方](https://support.google.com/admob/answer/14538460?hl=zh-Hans)、[官方](https://support.google.com/admob/answer/10564477)。
3. 付款门槛：满 US$100 付款 [官方](https://support.google.com/admob/answer/2772208)。

### 2.8 Play 管理中心要配合的声明

- 「应用内容」→ 广告：「是」；广告 ID：「是」，用途勾「分析」「广告或营销」「欺诈防范、安全和法规遵从」；数据安全：AdMob 的那几项。都见 [console.md](console.md)。
- 面向 13 岁以下：只能用家庭自认证的广告 SDK，并按插件 11.3+ 的 `RequestConfiguration.AgeRestrictedTreatment` 设置 [官方](https://support.google.com/googleplay/android-developer/answer/9893335)。

### 2.9 常见坑

- EDM：PGS 插件自带 EDM4U；AdMob 11.6 可以用 Unity 官方的 `com.unity.external-dependency-manager`。同一工程只保留一套，不然会重复解析或冲突（推断）。
- Gradle 连不上 Maven、多个会话共用 Gradle 守护进程被杀，见 [troubleshooting.md](troubleshooting.md)。

## 3. 应用内评价和应用内更新

安装：OpenUPM 的 `com.google.play.review` 和 `com.google.play.appupdate`（scope 要包含 `com.google.external-dependency-manager`、`com.google.play.common`、`com.google.play.core`），或从 GitHub 下载 `.unitypackage`。最新版本：review 1.8.4、appupdate 1.8.5（都是 2025-02）[官方](https://developer.android.com/guide/playcore/in-app-review/unity)、[官方](https://developer.android.com/guide/playcore/in-app-updates/unity)。

代码：`scripts/ShipPlayReview.cs`。

**应用内评价**：
- 在快到好时机时（例如关卡结算页前）`StartCoroutine(PrefetchReview())`，到了那个时机再 `StartCoroutine(LaunchReview())`。
- 有不公开的配额，**不要做成按钮**；弹出前不要问「喜欢吗」；API 不告诉你用户评没评，结束后照常继续 [官方](https://developer.android.com/guide/playcore/in-app-review)。
- 测试：从内部测试轨道安装（不受配额限制），账号要是设备主账号、从 Play 下载过、没评价过。内部应用分享能看到界面，但提交按钮是灰的 [官方](https://developer.android.com/guide/playcore/in-app-review/test)。

**应用内更新**：
- 脚本启动时自动检查。release 的 `inAppUpdatePriority` ≥ 4（可调）时请求立即更新，否则请求灵活更新。灵活更新下载完会调 `CompleteUpdate()`。
- 优先级只能在发布时用 Developer API 设置，发布后不能改。
- 测试：用内部应用分享。先装低版本，再点高版本的分享链接，**不要在 Play 页面里点安装**，回到应用里检查更新；内部应用分享不支持优先级 [官方](https://developer.android.com/guide/playcore/in-app-updates/test)。

## 4. Play 结算（Unity IAP）

- 用 Unity 的 In App Purchasing（`com.unity.purchasing`）。**版本至少 4.14.0 或 5.0.0**：Play 结算库 7 的截止日（延期到 2026-11-01）已过，必须用 PBL 8 以上 [官方](https://developer.android.com/google/play/billing/deprecation-faq)。5.4.0 起用 PBL 9；最新 5.4.4 [Unity 官方](https://docs.unity3d.com/Packages/com.unity.purchasing@5.4/changelog/CHANGELOG.html)。
- 接入写法按 Unity 手册（v5 用 `StoreController`）：https://docs.unity3d.com/Packages/com.unity.purchasing@5.4/manual/index.html
- 后台：
  - 先上传一个带 `com.android.vending.BILLING` 权限的包（Unity IAP 会自动加），并由账号所有者设好付款资料；
  - 再在「借助 Play 变现 → 商品 → 应用内商品」建商品 [官方](https://support.google.com/googleplay/android-developer/answer/1153481?hl=zh-Hans)；
  - 测试：「设置 → 许可测试」里加测试账号，应用要已发布到某个测试轨道，商品要已发布 [官方](https://support.google.com/googleplay/android-developer/answer/6062777?hl=zh-Hans)。
- 个人账号开始在 Google Play 上创收后，会公开完整地址，见 [onboarding.md](onboarding.md) 第 1 节。本仓库的实测游戏没有内购。

## 5. 云存档怎么选

- PGS 游戏存档（第 1 节）或任何第三方云存储（Firebase、Unity Cloud Save 等）都可以。没有自己的服务器时，PGS 游戏存档最省事。
- 只在关键节点存；处理多设备冲突；玩家没登录时先存本地，登录后再同步（Google 官方 cloud-save skill 的要点）。
- 本仓库的实测游戏只存在本地。
