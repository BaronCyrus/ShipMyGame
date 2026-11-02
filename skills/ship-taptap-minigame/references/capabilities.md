# 平台能力接入（Unity C#，SDK 2.0.21）

本文对应 SKILL.md 的 P4。

**通用约定**：
- 命名空间是 `TapTapMiniGame`。主要的类：
  - `Tap`：登录、排行榜、分享、存储、系统信息等；
  - `TapAchievement`：成就；
  - `TapCloudSave`：云存档；
  - `TapBattleClient`：多人联机。

  C# 的类名和字段名以 SDK 源码（`Runtime/Scripts/TapExt.cs`、`Runtime/Scripts/Wrapper/**`）为准 [官方](https://github.com/taptap/minigame-sdk-unity)。官方能力文档大多只给 JS 示例（`tap.xxx`）。
- **SDK 的 README 里分享接口的示例已经过时**，照抄会编译失败，以源码为准。
- SDK 里的 `Tap` 类是照着微信适配层整体生成的，里面有很多 TapTap 文档没有的接口，例如 `RequestMidasPayment`、`JoinVoIPChat`。只用小游戏 API 文档里写了的能力。
- 调用 TapTap 接口的代码，用 TapTap 分支专用的宏包起来，例如 `#if TAPTAP && UNITY_WEBGL && !UNITY_EDITOR`。这样其他渠道的分支、编辑器里都能正常编译 `[实测]`。
- 测试：
  - 成就和排行榜只记录测试名单里的账号。请用户提供自己的 TapTap 用户 ID，加进两个测试名单 `[实测]`。
  - 然后扫开发版本的自测二维码，在真机上验证。
  - 没有 PC 模拟器。SDK 菜单「TapTap小游戏/Tools/调试工具」可以在 Editor 里联手机调用接口 [官方](https://developer.taptap.cn/minigameapidoc/dev/engine/unity-adaptation/unity-debug/)，本仓库未实测。

| 能力 | 后台要配置 | 代码状态 |
|---|---|---|
| TapTap 登录 | 不用 | `[实测：编译通过，未真机验证]` |
| 成就 | 应用配置、创建成就、测试名单、发布 | `[实测：后台创建；代码编译通过]` |
| 排行榜 | 创建榜单、测试白名单 | `[实测：后台创建；代码编译通过]` |
| 云存档 | 未确认 | [官方] |
| 广告 | Dirichlet 平台建媒体和广告位 | [官方] |
| 分享 | 分享模板（入口未确认） | [官方] |
| 本地存储、系统信息、安全区 | 不用 | [官方] |
| 隐私授权接口 | 隐私保护指引 | [官方] |
| 添加到桌面 | 不用 | [官方] |
| 内购 | 要版号，个人做不了 | [官方] |

## 1. TapTap 登录（必接）

**为什么必须接**：
- 审核规范 5.2.4 要求包体「正确接入 TapTap 登录」[官方](https://developer.taptap.cn/minigameapidoc/tap-operation/operation-standards/review-standards/)。
- 包体管理页也写着同样的要求 `[实测]`。

**流程** [官方](https://developer.taptap.cn/minigameapidoc/dev/tutorial/open-capabilities/login-management/)：
1. 客户端调用 `tap.login` 拿到 `code`（5 分钟有效，只能用一次）。
2. 发给自己的服务器。
3. 服务器调用 `GET https://cloud-miniapp.tapapis.cn/auth/v1/jscode2session?appid=<MiniApp ID>&secret=<Secret>&js_code=<code>&grant_type=authorization_code`，换到 openid、session_key、unionid。
4. session_key 只能留在服务器上。

没有服务器的单机游戏：不保存 code，登录只是为了把会话绑定到玩家的 TapTap 账号，成就和排行榜靠这个识别玩家 `[实测：设计]`。

```csharp
// 用户同意隐私政策之后才调用（见 build.md 第 4 节）
Tap.InitSDK(_ =>
{
    Tap.Login(new LoginOption
    {
        success = res => Debug.Log("[Tap] login ok"),          // res.code：有服务器就发过去换 openid
        fail    = err => Debug.LogWarning("[Tap] login failed: " + err.errMsg)
    });
});
// 之后检查登录态是否还有效：
Tap.CheckSession(new CheckSessionOption { success = _ => { }, fail = _ => { /* 重新 Login */ } });
```

**坑**：
- 未确认：用户不同意隐私政策就不登录，是否满足 5.2.4。目前还没有过审结果 `[实测：待确认]`。
- 获取昵称和头像（`GetUserInfo`）要先通过 `CreateUserInfoButton` 让用户授权；用 `Authorize` 申请 `scope.userInfo` 不会弹窗 [官方](https://developer.taptap.cn/minigameapidoc/dev/api/open-api/authorize/tap.authorize/)。用了这个接口，就要在隐私保护指引里勾「昵称、头像」。

## 2. 成就

### 后台配置

1. 先开启「游戏服务 → 应用配置」[官方](https://developer.taptap.cn/docs/sdk/access/get-ready/)。然后在「游戏服务 → 运营工具 → 成就」开通服务。实测这一步是用户自己点的，具体按钮没有记录；页面要求同意服务协议时，由用户勾选。
2. 「配置」页签 →「创建普通成就」，表单字段 `[实测]`：
   - 图标：JPG/PNG，512×512，不超过 1 MB，不要圆角。未解锁的灰色版本由平台自动生成。
   - 名称：不超过 40 字符。
   - 简介：不超过 200 字符，写清怎么获得。
   - 成就 ID：英文和数字，不超过 40 字符。官方功能说明写「只允许英文+数字」，但页面示例 `NEW_ACHIEVEMENT_1_1` 里带下划线。**稳妥做法是只用小写字母加数字**，实测这样能用。
   - 「将此成就设置成为白金成就解锁条件范围内」：默认勾选。
   - 「将此成就设置成为分步成就」：分步成就只能累加步数，总步数 2～100,000,000。进度会倒退、或者游戏只上报总数的成就，不要设成分步，改成普通成就，在游戏里达到阈值时再解锁。
   - 展示状态：显示 / 隐藏。设好后不能再改。
3. 点「提交」后进入「审核中」，实测 1～2 分钟就变成「已审核（待发布）」。10 个成就约 8 分钟建完 `[实测]`。
4. 「测试」页签：添加测试用户（最多 100 个），只有名单里的账号能上报测试数据。发布前，只有管理员和测试人员能看到成就 `[实测]`。
5. 上线前点「发布普通成就」：**至少 5 个**才能发布。发布后成就 ID、分步设置、分组、初始状态都不能改，也不能删除或重置。测试数据没重置的话会带进正式环境 [官方](https://developer.taptap.cn/docs/sdk/achievement/features/)。
6. 白金成就：需要 10 个以上纳入白金范围的普通成就，并且有质量门槛。官方写明不接入休闲小游戏和玩法单一的益智游戏，所以这类游戏不用申请 [官方](https://developer.taptap.cn/docs/sdk/achievement/features/)。

### 代码

```csharp
TapAchievement.Setup(new AchievementManagerOption { enableToast = true });   // 解锁时显示平台冒泡
TapAchievement.RegisterCallBack(new AchievementListener
{
    success = s => Debug.Log($"[Tap] achievement {s.code} {s.result?.achievementId}"),     // 70001 解锁成功；70002 步数增加成功
    fail    = f => Debug.LogWarning($"[Tap] achievement {f.achievementId} {f.errorCode} {f.errorMsg}")
});
TapAchievement.Unlock("<成就ID>");             // 普通成就
TapAchievement.Increment("<成就ID>", 1);       // 分步成就：传增量
TapAchievement.ShowAchievements();             // 打开平台成就页
```

错误码 [官方](https://github.com/taptap/minigame-sdk-unity)：
- 80000：未初始化；
- 80001：区域不支持；
- 80002：未登录；
- 80010：登录失效；
- 80020：参数无效；
- 80030：网络异常；
- 80100：未知错误。

要先注册回调再解锁，否则收不到结果 [官方](https://developer.taptap.cn/minigameapidoc/dev/api/open-api/achievements/AchievementManager.unlockAchievement/)。

**自动化填表**：
- 按元素引用点「提交」时实测点到了「取消」，改用 JS 读坐标后按坐标点。
- 上传图标后等 2 秒再填文字。
- 每建一个成就，在页面里用同源请求 `fetch('/api/achievement/v2/list?client_id=<Client ID>&page_size=20&page_num=1')` 读回列表核对。这是非公开接口，可能会变 `[实测]`。

## 3. 排行榜

### 后台配置

1. 在「游戏服务 → 运营工具 → 排行榜」点「创建排行榜」。数量显示「可配置排行榜数量：N /100」`[实测]`。
2. 「排行榜设置」弹窗里的字段 `[实测]`：
   - 排行榜名称：不超过 10 字。
   - 所属分组：不选就归在全部类别下。
   - 额外规则说明：选填，不超过 500 字。
   - 重置周期：不重置、日、周、月等。
   - 重置时间。
   - 最大展示排名数：例如 100。
   - 数据类型：例如「数值型」，要填单位。
   - 数据排序方式：升序或降序。
   - 数据更新策略：官方推荐「保留历史最高分」，实测选的是「周期内最佳」。
   - 最低值、最高值：选填，超出范围的成绩会被屏蔽。
   - 界面底图：JPG/PNG，不超过 2 MB，1920×1080；不传就用游戏宣传图。
3. **重置周期、重置时间、数据类型、排序方式、数据更新策略、枚举值，创建后都不能改** [官方](https://developer.taptap.cn/docs/sdk/leaderboard/features/)。
4. 新建的榜默认「仅白名单可见」。在「测试白名单」页签里加测试用户的 TapTap ID `[实测]`。
5. 排行榜 ID 由平台生成，在列表的「排行榜 ID」列里复制 `[实测]`。
6. 上线前：关掉「仅白名单可见」，在数据管理里清空测试数据（所有周期都要清），核对客户端里的排行榜 ID [官方](https://developer.taptap.cn/docs/sdk/leaderboard/features/)。
7. 自动化：重置周期、数据类型、更新策略这几个下拉框，实测都要截图找坐标点选。2 个榜约 3 分钟建完 `[实测]`。

### 代码

```csharp
using System.Collections.Generic;

Tap.SubmitScore(new SubmitScoreOption
{
    scores = new List<ScoreItem> { new ScoreItem { leaderboardId = "<排行榜ID>", score = 1234 } },   // 一次 1～5 条，只能是整数
    callback = new LeaderboardCallback<SubmitScoresResponse>
    {
        onSuccess = r => Debug.Log("[Tap] submitted"),
        onFailure = (code, msg) => Debug.LogWarning($"[Tap] submit {code} {msg}")
    }
});
Tap.OpenLeaderboard(new OpenLeaderboardOption
{
    leaderboardId = "<排行榜ID>",
    collection = LeaderboardCollection.PUBLIC,   // 好友榜用 FRIENDS，见下
    callback = new OpenLeaderboardCallback { onSuccess = _ => { }, onFailure = (c, m) => Debug.LogWarning($"{c} {m}") }
});
// 自己画榜单时：Tap.LoadLeaderboardScores / LoadCurrentPlayerLeaderboardScore / LoadPlayerCenteredScores
```

**错误码** [官方](https://developer.taptap.cn/minigameapidoc/dev/api/open-api/leaderboard/LeaderboardManager.openLeaderboard/)：
- 500001：排行榜 ID 不存在；
- 500002：ID 和游戏不匹配；
- 500199：一次提交的条数不在 1～5 之间；
- 500201：leaderboardId 为空。

**好友榜**：要先在隐私保护指引里勾「好友关系」，否则会报以下错误 [官方](https://developer.taptap.cn/minigameapidoc/dev/tutorial/open-capabilities/leaderboard-tutorial/)：
- 1025：没有声明使用好友关系；
- 104：用户没同意隐私协议；
- 103：用户没授权好友关系。

只用公开榜就不用声明。另外，「保留历史最高分」的榜，提交更低的分数不会更新成绩。

## 4. 云存档（官方，未实测）

依据 [官方](https://developer.taptap.cn/minigameapidoc/dev/tutorial/open-capabilities/cloud-save-tutorial/)：
- **限制**：
  - 单个存档文件不超过 10 MB，封面不超过 512 KB。
  - 存档名不超过 60 字节，不能有空格和中文。
  - 每个玩家每个游戏最多 100 个存档、总共 100 MB。
  - 上传频率有两种说法：小游戏教程写每分钟 1 次（超了报 400001），SDK 功能页写每分钟 60 次、最多 10 个并发 [官方](https://developer.taptap.cn/docs/sdk/tap-cloudsave/features/)。按严的来，只在通关、手动保存这类关键时刻同步。
- **流程**：先把数据写进小游戏本地文件（`Tap.env.USER_DATA_PATH` 下），再创建或更新云存档。
- **后台**：要不要先开通服务，文档没写，未确认。

```csharp
TapCloudSave.Setup();
// 先把存档写到本地文件 path（推断：Tap.GetFileSystemManager() 的写文件接口；签名以 SDK 为准）
TapCloudSave.CreateArchive(new CreateArchiveOption
{
    name = "slot1", description = "Lv5", archiveFilePath = path, playtime = 120,
    success = r => Debug.Log($"[Tap] archive {r.archiveId}"),
    fail = (code, msg) => Debug.LogWarning($"[Tap] archive {code} {msg}")
});
// 列表：TapCloudSave.GetArchiveList；下载：GetArchiveData；更新：UpdateArchive（要传 archiveId）
```

## 5. 广告

**能不能接**：Unity 导出的包属于「Tap 小游戏」，广告只能接 Dirichlet（又名 TapADN）聚合。
- 后台的「广告变现」页（以及官方 MCP 的广告指南）只适用于星火、H5 小游戏和 TapTap 制造 `[实测]` [官方](https://developer.taptap.cn/minigameapidoc/quick-start/mcp-guide/ad-integration-guide/)。
- Dirichlet 零分成，个人和企业都能接入 [官方](https://developer.taptap.cn/minigameapidoc/quick-start/monetization/IAA-game/)。

**个人能不能拿到钱**：难。以下是 2026-10 调研的结论，来源是 Dirichlet 协议，不在 TapTap 文档里，只当线索：
- 每次结算要一张等额的增值税专用发票，个人开不出来；
- 累计满 1000 元才结算；
- Dirichlet 账号主体要和 TapTap 开发者主体一致。

要靠广告赚钱，得用个体户或公司主体。把这些先讲给用户听，由他决定接不接。

**步骤**：
1. 在 https://ssp.dirichlet.cn/ 用 TapTap 开发者账号登录，新建媒体时「应用类型」选「小游戏」，再建广告位，拿到 adUnitId。TapTap 小游戏支持激励视频、插屏、横幅三种。这一条来自 Dirichlet 帮助页，只当线索。
2. 测试阶段配置测试工具，保证测试广告位能正常展示；正式广告位要在 Dirichlet 提交审核，通过后才会展示 [官方](https://developer.taptap.cn/minigameapidoc/quick-start/monetization/IAA-game/)。Dirichlet 的常见问题提到，游戏正式开放前可以先建测试媒体和测试推广位，这一条只当线索。

```csharp
// 激励视频：必须先 Load 成功才能 Show；看完才给奖励
var rv = Tap.CreateRewardedVideoAd(new TapCreateRewardedVideoAdParam { adUnitId = "<广告位ID>" });
rv.OnLoad(_ => { /* 可以显示「看视频」按钮 */ });
rv.OnError(e => Debug.LogWarning("[Tap] ad error: " + e.errMsg));
rv.OnClose(r => { if (r.isEnded) GiveReward(); });
rv.Load();
// 玩家主动点按钮后：rv.Show();

// 横幅：固定在屏幕底部中间，每 30 秒自动刷新，高 50
var banner = Tap.CreateFixedBottomMiddleBannerAd("<广告位ID>", 30, 50);
banner.OnError(e => Debug.LogWarning(e.errMsg));
// banner.Hide(); banner.Show(); banner.Destroy();
```

C# 示例来自官方 Unity 广告页 [官方](https://developer.taptap.cn/minigameapidoc/dev/engine/unity-adaptation/unity-ad/)。插屏广告：SDK 里有 `TapCreateInterstitialAdParam` 和 `TapInterstitialAd`，C# 用法应该和激励视频类似，属于推断，Unity 文档没写。

**审核红线** [官方](https://developer.taptap.cn/minigameapidoc/tap-operation/operation-standards/minigame-ad-policy/) [官方](https://developer.taptap.cn/minigameapidoc/tap-operation/operation-standards/review-standards/)：
- 首次启动后 60 秒内或第 1 关内，不展示任何广告；120 秒内或前 3 关内，最多 1 条会强制打断游戏的全屏广告。
- 激励视频：展示前说明规则和奖励，由用户主动选择看，播放中可以随时关闭，看完才给奖励，不能虚假宣传奖励。
- 横幅：高度不超过屏幕的 10%，固定在顶部或底部的非操作区，有明显的关闭按钮，不紧贴可点击元素。
- 插屏：只在关卡结束、死亡复活前这类自然断点出现。
- 广告要明显标注「广告」；非激励广告的展示总时长不超过游戏时长的 20%（行为规范第五章）。

**坑**：
- 广告位 ID 为空时，把所有广告入口隐藏，原本靠看广告解锁的内容改成直接可用，避免审核时出现点了没反应的按钮 `[实测]`。
- 官方 Unity 广告页里的「变现能力指引」链接已经失效（404）。
- 接了广告，要在隐私保护指引里加第三方 SDK 信息，商店资料也要如实反映。

## 6. 分享与好友（官方，未实测）

依据 [官方](https://developer.taptap.cn/minigameapidoc/dev/api/open-api/share/tap.showShareboard/)：
- 分享面板必须传分享模板 ID（`templateId`）。文档只写了「在开发者后台创建自定义分享模版」，**入口在哪里没写**，未确认。
- 需要基础库 1.7.0 及以上。

```csharp
Tap.ShowShareboard(new ShowShareboardOption
{
    templateId = "<分享模板ID>", sceneParam = "room=1",   // sceneParam 会透传给打开分享卡片的玩家
    success = r => { }, fail = r => Debug.LogWarning($"{r.errNo} {r.errMsg}")
});
Tap.OnShareMessage(new OnShareOption { success = r => Debug.Log(r.channel), fail = r => { } });   // 分享结果
Tap.SetShareboardHidden(new SetShareboardHiddenOption { hidden = false });   // 只影响右上角菜单里的分享入口
Tap.OpenFriendList();
```

`ShowShareboard` 的回调只表示面板有没有拉起来，真正的分享结果看 `OnShareMessage`。

## 7. 本地存储

依据 [官方](https://developer.taptap.cn/minigameapidoc/dev/api/storage/tap.setStorageSync/)：
- 限制：每个 key 不超过 1 MB，总共不超过 10 MB。
- 启动阶段不要频繁同步读写。
- 官方建议小游戏里不用 `System.IO.File` 和 PlayerPrefs，改用平台存储。
- C# 写法：
  - `Tap.SetStorageSync<T>(key, data)`、`Tap.RemoveStorageSync(key)`、`Tap.GetStorageInfoSync()` 在 SDK 的 `Tap.cs` 里有。
  - 替代 PlayerPrefs 的字符串和整数读写：SDK 底层有 `StorageSetStringSync`、`StorageGetStringSync`、`StorageSetIntSync`、`StorageHasKeySync`、`StorageDeleteKeySync` 等对应函数，C# 应该是 `Tap.StorageSetStringSync(key, value)` 这样的写法（推断，和微信 SDK 一样）。

关键操作后立即保存，不要依赖退出时的回调（见 [project-check.md](../../ship-my-game/references/project-check.md)）。

## 8. 系统信息、安全区、右上角胶囊

依据 [官方](https://developer.taptap.cn/minigameapidoc/dev/api/base/system/tap.getWindowInfo/)：
- `Tap.GetWindowInfo()` 返回 `WindowInfo`。字段有 `pixelRatio`、`screenWidth/Height`、`windowWidth/Height`、`statusBarHeight`，以及 `safeArea{left,right,top,bottom,width,height}`。
- `safeArea` 是**竖屏正方向下**的逻辑像素：要乘以 `pixelRatio` 才能换成 Unity 的屏幕像素；横屏游戏还要自己转换坐标。有的机型不返回 `safeArea`，代码要能兼容。
- 右上角胶囊按钮的位置用 `Tap.GetMenuButtonBoundingClientRect()` 取。重要按钮不要放在它下面。

## 9. 隐私授权接口

依据 [官方](https://developer.taptap.cn/minigameapidoc/dev/api/open-api/privacy/tap.requirePrivacyAuthorize/)：
- 调用隐私相关接口（例如 `getUserInfo`、剪切板）时，用户还没同意过隐私协议的话，平台会弹统一的隐私弹窗。
- 想在一开始就主动弹：调用 `Tap.RequirePrivacyAuthorize(new RequirePrivacyAuthorizeOption { success = …, fail = … })`。
- 用自定义弹窗：注册 `onNeedPrivacyAuthorization`，然后必须在用户点击时回传 agree 或 disagree。这个接口在 C# 里的签名未确认。
- 只用 TapTap 登录、成就、公开排行榜，而且隐私保护指引一项都没勾时：`ShipCompliance` 的同意弹窗就够了，实测没有用到这些接口 `[实测]`。

## 10. 添加到桌面（建议接）

依据 [官方](https://developer.taptap.cn/minigameapidoc/dev/tutorial/open-capabilities/desktop-folder/)：
- 《小游戏文档指引》把它列为「必接能力」[官方](https://developer.taptap.cn/minigameapidoc/quick-start/document-guide/)。审核规范没写，是否影响过审未确认，实测那次没有接。

```csharp
Tap.HasHomeScreenWidgetAndPinned(new HasHomeScreenWidgetOption
{
    success = r => { if (!r.hasWidget || !r.isPinned) { /* 在合适的时机询问玩家要不要添加 */ } }
});
Tap.CreateHomeScreenWidget(new CreateHomeScreenWidgetOption { success = _ => { /* 可以给一点奖励 */ }, fail = r => { } });
```

**坑**：
- 扫码自测时测不了，只能在线上版本里体验。
- iOS 上会跳转到 Safari，由用户手动「添加到主屏幕」，`isPinned` 不可靠。
- 错误码 1518002 表示已经添加过。

## 11. 内购（个人做不了）

依据 [官方](https://developer.taptap.cn/minigameapidoc/quick-start/monetization/IAP-game/)：
- 要有《网络游戏出版物号核发单》，用工单申请白名单，并完成企业财务认证。
- 没有版号的游戏不得有任何内购计费（审核规范 1.1.1）。

个人开发者不要接内购，也不要在游戏里放「购买」入口。
