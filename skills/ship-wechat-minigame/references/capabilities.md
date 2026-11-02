# 平台能力接入（Unity C#，命名空间 `WeChatWASM`）

**本页示例怎么来的**
- 类型名和字段名都对照 SDK v0.1.34 的 `Runtime/WX.cs`、`Runtime/WXBase.cs` 和 `wx-runtime.dll` 元数据核对过。
- 标 `[实测]` 的，是实测游戏里实际用过的写法：存档、安全区、广告代码在 Unity 6000.6 上编译通过、模拟器能启动。真实广告的加载和播放还没测，因为流量主还没开通。
- 其余是 `[官方]`。

**通用约定**
- 所有微信调用都包在 `#if UNITY_WEBGL && !UNITY_EDITOR && !SHIP_NO_WECHAT` 里，`using WeChatWASM;` 也放在里面。这样其他平台和没装 SDK 的分支都能编译。`SHIP_NO_WECHAT` 和 `ShipWeChatBuild.cs` 用的是同一个开关。
- 先初始化：在 `WX.InitSDK(code => { ... })` 的回调里创建游戏，再初始化广告。主页出现后调用一次 `WX.ReportGameStart()` `[实测]`。
- C# 的写法基本就是把 JS 的 `wx.xxx` 改成 `WX.Xxx`。没封装的接口用 `WX.CallJSFunction("wx", "<函数名>", <参数对象>)` 调用；判断能否调用用 `WX.CanIUse("<接口名>")` `[官方]`（[WX SDK 平台能力适配](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/WX_SDK.html)）。
- **坐标单位不统一**（依据：SDK 的 `unity-sdk/*.js` 源码）：
  - `CreateUserInfoButton`、`ShowOpenData` 传 Unity 屏幕像素，原点在左上角；
  - 广告的 `style`、`CreateGameClubButton` 的 `style` 传逻辑像素（CSS px），和 `WX.GetWindowInfo().windowWidth` 同一单位；
  - `GetWindowInfo().safeArea` 和 `GetMenuButtonBoundingClientRect()` 返回逻辑像素。
- **单机游戏用不了隐私接口**：好友排行榜、剪贴板、昵称头像、游戏圈数据都属于隐私接口。原因见 [console.md](console.md#用户隐私保护指引)。下面每个能力都注明要不要联网、要不要声明隐私。

## 存档 `[实测]`

- 小游戏里 `persistentDataPath` 上的文件不可靠，所以 WebGL 版改用微信本地存储。
- 关闭小游戏时不一定会走到 `OnApplicationPause`，所以每一步关键操作后立即存。

```csharp
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR && !SHIP_NO_WECHAT
using WeChatWASM;
#endif

public static class SaveStore
{
    public static void SetString(string key, string value)
    {
#if UNITY_WEBGL && !UNITY_EDITOR && !SHIP_NO_WECHAT
        WX.StorageSetStringSync(key, value);     // 等同于 wx.setStorageSync
#else
        PlayerPrefs.SetString(key, value);
        PlayerPrefs.Save();
#endif
    }

    public static string GetString(string key, string fallback = "")
    {
#if UNITY_WEBGL && !UNITY_EDITOR && !SHIP_NO_WECHAT
        return WX.StorageGetStringSync(key, fallback);
#else
        return PlayerPrefs.GetString(key, fallback);
#endif
    }
}
```

限制 `[官方]`（[本地缓存](https://developers.weixin.qq.com/minigame/dev/guide/base-ability/storage.html)、[Unity QA](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/DevelopmentQAList.html)）：
- 每个用户在每个小游戏里最多存 10MB。
- 同步接口会阻塞主线程，不要每帧调用。
- 用户删除小游戏，数据也会一起删掉。
- 需要跨设备存档要做云端存储，那属于联网游戏。

怎么测：在模拟器和真机上各试一次，存完后划掉微信再进，确认数据还在。

## 安全区和胶囊按钮

`[实测]`

- 小游戏里 `Screen.safeArea` 等于整个屏幕，既不避让刘海，也不避让右上角的胶囊按钮。
- 实测修了 3 轮才在真机上不重叠：
  1. 右侧按钮挪过头，压到了左侧按钮；
  2. 没算胶囊左边那个圆形「游戏圈」按钮，它只在真机上出现，开发者工具里看不到；
  3. 第一帧缩放值还没算好。

```csharp
using UnityEngine;
using UnityEngine.UI;
#if UNITY_WEBGL && !UNITY_EDITOR && !SHIP_NO_WECHAT
using WeChatWASM;
#endif

public static class WxInsets
{
    // 返回值都是 CSS px（逻辑像素）
    public static void Get(out float top, out float bottom, out float capsuleRight, out float windowWidth)
    {
        top = bottom = capsuleRight = 0; windowWidth = 0;
#if UNITY_WEBGL && !UNITY_EDITOR && !SHIP_NO_WECHAT
        WindowInfo win = WX.GetWindowInfo();
        ClientRect menu = WX.GetMenuButtonBoundingClientRect();
        windowWidth = (float)win.windowWidth;
        double safeTop = win.safeArea != null ? win.safeArea.top : 0;           // 有的机型不返回 safeArea
        double safeBottom = win.safeArea != null ? win.safeArea.bottom : win.windowHeight;
        top = (float)System.Math.Max(safeTop, win.statusBarHeight);
        bottom = (float)(win.windowHeight - safeBottom);
        // 顶栏右侧要让出的宽度：胶囊本身，加一个胶囊高度（给真机上的游戏圈按钮），再加 12px 间距
        capsuleRight = (float)((win.windowWidth - menu.left) + menu.height + 12);
#endif
    }

    // CSS px 换成画布单位。用于 Scale With Screen Size、按宽度匹配的 Canvas。
    // 不要用 canvas.scaleFactor：第一帧它还没算好，会导致第一次进入时顶部重叠。
    public static float CssToCanvas(float css, CanvasScaler scaler, float windowWidth)
        => windowWidth > 0 ? css * scaler.referenceResolution.x / windowWidth : 0;
}
```

- 按高度匹配的 Canvas，改用 `referenceResolution.y / windowHeight` 换算。
- 实测采用的布局：内容区顶部只让出状态栏；游戏顶栏和胶囊在同一行；顶栏右侧的按钮和标题往左让出 `capsuleRight`。
- 怎么测：只能看真机。请用户截图核对刘海屏机型，胶囊和游戏圈按钮都不能挡住游戏里的按钮。

## 广告（流量主）

### 开通条件 `[官方]`

来源：[广告](https://developers.weixin.qq.com/minigame/dev/guide/open-ability/ad/ad.html)。实测时在「功能 → 流量主」页面读到的文字和官方一致。满足任一条件即可开通：

1. 小程序累计独立访客（UV）超过 500，没有刷粉行为，也没有严重违规记录。
2. 开通内置游戏圈，并产生有效活跃（用户从游戏进入游戏圈后有消费或转评赞），还要发布 5 篇官方帖。
3. 同公司主体名下已有开通流量主、连续一个季度有变现的小程序。个人主体用不上这一条。

注意：
- 满足条件后资格要**次日**才刷新。
- 开通时要同意《微信公众平台广告展示服务协议》等协议，由用户本人确认。
- 前两个条件都需要上线后有真实用户。所以顺序只能是：上线 → 用户达标 → 开通流量主 → 建广告位 → 把 ID 填进游戏 → 再发一版。**审核期间接不了真实广告。**
- Unity 的 WX_SDK 页写的门槛是「UV>1000」，和广告专页不一致，以广告专页的「> 500」为准。

### 广告位 `[官方]`

- 开通后在流量主的「广告管理」里新建广告位，每个位置单独建一个，建好得到 `adunit-` 开头的 ID。
- 具体按钮名本仓库**未核对**。
- 有一条只在搜索摘要里见过、**未核实**的说法：广告位关闭后 24 小时内不能重新开启。

### 代码 `[实测]`

实测游戏的做法：ID 为空时什么都不创建；激励视频只在用户主动点「观看视频」时才播。

```csharp
using System;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR && !SHIP_NO_WECHAT
using WeChatWASM;
#endif

public static class WxAds
{
    public static string RewardedId = "";      // 流量主开通后填 adunit-…；空着就不创建
    public static string InterstitialId = "";
    public static string CustomId = "";

#if UNITY_WEBGL && !UNITY_EDITOR && !SHIP_NO_WECHAT
    static WXRewardedVideoAd rewarded;
    static WXInterstitialAd interstitial;
    static WXCustomAd custom;
    static Action<bool> pending;

    public static void Init()                  // 在 WX.InitSDK 回调里、游戏创建之后调用
    {
        if (rewarded != null || string.IsNullOrEmpty(RewardedId)) return;
        rewarded = WX.CreateRewardedVideoAd(new WXCreateRewardedVideoAdParam { adUnitId = RewardedId });
        rewarded.OnLoad((WXADLoadResponse r) => { });
        rewarded.OnError((WXADErrorResponse e) => Debug.LogWarning("rewarded error " + e.errCode + " " + e.errMsg));
        rewarded.OnClose((WXRewardedVideoAdOnCloseResponse r) => Finish(r != null && r.isEnded));   // 只注册一次
    }

    public static bool CanShowRewarded => rewarded != null;

    // onDone(true) 才发奖励。播放期间暂停游戏和声音，回调里再恢复。
    public static void ShowRewarded(Action<bool> onDone)
    {
        if (rewarded == null || pending != null) { onDone(false); return; }
        pending = onDone;
        rewarded.Show((WXTextResponse ok) => { },
            (WXTextResponse fail) => rewarded.Load(
                (WXTextResponse loaded) => rewarded.Show((WXTextResponse ok2) => { }, (WXTextResponse f2) => Finish(false)),
                (WXADErrorResponse le) => Finish(false)));
    }

    static void Finish(bool ended) { var cb = pending; pending = null; cb?.Invoke(ended); }

    public static void ShowInterstitial()      // 只在通关这类自然断点调用
    {
        if (string.IsNullOrEmpty(InterstitialId)) return;
        if (interstitial == null)
        {
            interstitial = WX.CreateInterstitialAd(new WXCreateInterstitialAdParam { adUnitId = InterstitialId });
            interstitial.OnError((WXADErrorResponse e) => Debug.LogWarning("interstitial error " + e.errCode));
            interstitial.OnClose(() => { });
        }
        interstitial.Show((WXTextResponse ok) => { }, (WXTextResponse fail) => { });   // 没拉到或被限频：直接继续游戏
    }

    public static void ShowBottomCustomAd()    // 原生模板广告（代替 Banner）
    {
        if (string.IsNullOrEmpty(CustomId) || custom != null) return;
        WindowInfo win = WX.GetWindowInfo();   // 逻辑像素
        custom = WX.CreateCustomAd(new WXCreateCustomAdParam
        {
            adUnitId = CustomId,
            adIntervals = 30,
            style = new CustomStyle { left = 0, top = (int)(win.windowHeight - 120), width = (int)win.windowWidth }
        });
        custom.OnError((WXADErrorResponse e) => Debug.LogWarning("custom ad error " + e.errCode));
        custom.OnLoad((WXADLoadResponse r) => custom.Show((WXTextResponse ok) => { }, (WXTextResponse f) => { }));
    }

    public static void HideBottomCustomAd() { if (custom != null) { custom.Destroy(); custom = null; } }
#else
    public static void Init() { }
    public static bool CanShowRewarded => false;
    public static void ShowRewarded(Action<bool> onDone) => onDone(false);
    public static void ShowInterstitial() { }
    public static void ShowBottomCustomAd() { }
    public static void HideBottomCustomAd() { }
#endif
}
```

**激励视频** `[官方]`（[激励视频广告](https://developers.weixin.qq.com/minigame/dev/guide/open-ability/ad/rewarded-video-ad.html)）
- 激励视频是全局单例，只创建一次，`OnClose` 也只注册一次。重复注册会导致一次关闭发好几次奖励。
- 只有 `isEnded == true` 才发奖励，中途关闭不给。
- C# 收到的回调对象由 SDK 拼出来，不会是 null。所以不要照搬 JS 示例里的 `res === undefined` 分支：在 2.1.0 以下的基础库，`isEnded` 会是 false。
- 自动拉取失败后，`Show` 会走失败回调，要先 `Load` 再 `Show`。
- 每个用户每天能看的激励视频次数有限，所以按钮要能接受「暂时没有可播放的视频」。

**1004 兜底分享页** `[官方]`
- 基础库 3.7.7 起，遇到错误码 1004 时，平台会弹一个分享页，分享后也能拿奖励。这时触发的是 `onLoad`，带 `useFallbackSharePage`，`onError` 不触发。
- SDK v0.1.34 的 C# 参数里没有 `disableFallbackSharePage`，`WXADLoadResponse` 里也没有 `useFallbackSharePage`。
- 要关掉或识别兜底分享页，只能改 JS 或用 `CallJSFunction`，**未实测**。

**服务端校验**：可选。在流量主后台开启「服务端奖励回调」，需要服务器，单机游戏一般不用 `[官方]`（[激励广告反作弊](https://developers.weixin.qq.com/minigame/dev/guide/security/antiadcheat.html)）。

**插屏**
- 实测的节奏：开局 180 秒内不出；两次之间至少 150 秒、至少隔两次断点；看完激励视频后，插屏重新计时。这个节奏是游戏自己定的，不是平台规定。
- 平台的规定是：不能在点「开始/继续游戏」时弹，不能挡住功能按钮（[运营规范](https://developers.weixin.qq.com/minigame/product/) 3.2.10）`[官方]`。

**Banner**
- 实测游戏用的是 `WX.CreateFixedBottomMiddleBannerAd(adUnitId, 30, height)`，编译通过。
- 但官方原文：「从基础库 3.5.5 开始，本接口停止维护，请使用原生模板广告 wx.createCustomAd 代替」。
- 新代码用上面的原生模板广告。模板实际高度随模板变化，示例里的 120 只是示意，要按真机效果调整。

**怎么测**
- 开通流量主之前：确认 ID 为空时按钮提示没有视频，游戏不卡、不报错。
- 开通之后：用体验版或线上版在真机上测三件事：
  1. 中途关闭不发奖励；
  2. 看完才发奖励；
  3. 没广告时 `Show` 失败，`Load` 之后再 `Show` 的流程正常。
- 开发者工具能不能出测试广告，**未确认**。

**收入** `[官方]`（[商业化](https://developers.weixin.qq.com/minigame/introduction/commercialization/)、[广告变现激励政策](https://developers.weixin.qq.com/minigame/introduction/commercialization/guide/ad-monetization.html)）
- IAA 游戏现金分成 50%。
- 半月结算，在「流量主 → 财务管理」查看。
- 激励政策会调整，以页面为准。
- 社区说法：个人不用开发票，由平台代扣税费。只能当线索。

## 分享

不需要后台配置，不属于隐私接口，单机游戏也能用 `[官方]`。来源：[转发](https://developers.weixin.qq.com/minigame/dev/guide/open-ability/share/share.html)、[分享到朋友圈](https://developers.weixin.qq.com/minigame/dev/guide/open-ability/share-timeline_game.html)。

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR && !SHIP_NO_WECHAT
using WeChatWASM;

public static class WxShare
{
    public static void Setup()                 // InitSDK 回调里调用一次
    {
        // 右上角菜单里的「转发」默认是灰的，要先打开
        WX.ShowShareMenu(new ShowShareMenuOption { menus = new[] { "shareAppMessage", "shareTimeline" } });
        WX.OnShareAppMessage(new WXShareAppMessageParam { title = "<分享标题>", imageUrl = "<5:4 图片地址或包内路径>", query = "from=menu" });
        WX.OnShareTimeline((setter) => setter(new OnShareTimelineListenerResult { title = "<分享标题>", query = "from=timeline" }));
    }

    public static void ShareNow(string query)  // 游戏里按钮触发的主动分享
        => WX.ShareAppMessage(new ShareAppMessageOption { title = "<分享标题>", imageUrl = "<图片>", query = query });
}
#endif
```

要点：
- 图片最佳比例 5:4。不传图片时用游戏画面截图。
- 用审核过的图片时，要在「游戏能力地图 → 运营素材管理 → 分享图片」先提交，审核通过后拿到 `imageUrlId`，和图片地址一起传。
- 进入游戏的 query 从 `WX.GetLaunchOptionsSync().query` 读，切回前台时从 `WX.OnShow` 读。
- 分享到朋友圈是 Beta 功能，**只有 Android** 支持；从朋友圈打开时是预览模式，场景值 1155。
- 运营规范：不得强制分享、暗示分享，不得用利益诱导分享到朋友圈，不得设置「分享后无需互动即可获得利益」（2.5.1）。所以「分享得奖励」这类设计不要做。

怎么测：用真机，开发者工具里只是模拟面板。从分享卡片进入时，检查 query 有没有带上。

## 游戏圈按钮 `[官方]`

来源：[游戏圈](https://developers.weixin.qq.com/minigame/dev/guide/open-ability/game-club.html)、[wx.createGameClubButton](https://developers.weixin.qq.com/minigame/dev/api/open-api/game-club/wx.createGameClubButton.html)。

- 按钮本身不是隐私接口，单机游戏也能用。
- 读取用户的游戏圈数据（`getGameClubData`）要用户授权，并且要在隐私指引里声明。
- 后台管理在「游戏能力地图 → 游戏圈」。是否必须先开通才能显示按钮，**未确认**。
- 要跳到某个帖子时，传 `openlink`，它是帖子的「游戏内跳转ID」。

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR && !SHIP_NO_WECHAT
using WeChatWASM;

public static class WxClub
{
    public static WXGameClubButton Create()    // style 用逻辑像素
    {
        var btn = WX.CreateGameClubButton(new WXCreateGameClubButtonParam
        {
            type = GameClubButtonType.image,
            icon = GameClubButtonIcon.green,
            style = new GameClubButtonStyle { left = 10, top = 76, width = 40, height = 40 }
        });
        btn.OnTap(() => { });
        btn.Show();
        return btn;                            // 离开页面时调用 Hide() 或 Destroy()
    }
}
#endif
```

注意：按钮是原生控件，叠在游戏画面之上；真机上胶囊左边已经有一个系统的游戏圈按钮（见「安全区和胶囊按钮」）。

## 排行榜（好友关系）`[官方]`

**前提：联网，并且隐私指引里声明了「微信朋友关系」**（见 [console.md](console.md#用户隐私保护指引)）。单机游戏走不通。来源：[关系链](https://developers.weixin.qq.com/minigame/dev/guide/open-ability/open-data.html)、[Unity 排行榜](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/OpenData.html)。

1. 导出面板勾选「使用好友关系链」，对应 `MiniGameConfig` 的 `UseFriendRelation`。导出时会把示例 `open-data/` 拷进 `minigame/`，注意先备份自己改过的文件。
2. 主域上报分数用 `WX.SetUserCloudStorage`。要进微信自带的排行榜（搜索、社交组件），value 必须是 `{"wxgame":{"score":<整数>,"update_time":<秒级时间戳>}}`，并在「游戏能力地图 → 社交能力 → 微信排行榜配置」配置，审核通过后生效。
3. 好友数据只能在开放数据域（JS）里取，用 `wx.getFriendCloudStorage`。Unity 里放一个 RawImage 占位，`rotation.x` 设为 180，用 `WX.GetOpenDataContext(new OpenDataContextOption { sharedCanvasMode = CanvasType.ScreenCanvas })` 获取上下文（只获取一次），用 `PostMessage` 发消息，用 `WX.ShowOpenData(texture, x, y, w, h)` 显示（Unity 像素、左上角原点），关闭时**必须**调 `WX.HideOpenData()`。

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR && !SHIP_NO_WECHAT
using WeChatWASM;

public static class WxRank
{
    public static void Report(int score)
    {
        long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        WX.SetUserCloudStorage(new SetUserCloudStorageOption
        {
            KVDataList = new KVData[] { new KVData { key = "score", value = "{\"wxgame\":{\"score\":" + score + ",\"update_time\":" + now + "}}" } },
            success = (res) => { },
            fail = (err) => UnityEngine.Debug.LogWarning("rank upload fail: " + err.errMsg)
        });
    }
}
#endif
```

坑：
- 漏调 `HideOpenData` 会导致纹理错乱。
- 第一次显示时黑一下：开放数据域收到 `WXRender` 事件后，要同步画点东西。
- 工具报 Layout 插件 `permission deny`：要在开发者工具里添加插件。不用关系链时，删掉 `game.json` 里的 `plugins.Layout`。
- 怎么测：只能用真机，而且要有托管过数据的好友。

## 登录 `[官方]`

**前提：要有自己的服务器（联网）。** 来源：[登录](https://developers.weixin.qq.com/minigame/dev/guide/open-ability/login.html)、[code2Session](https://developers.weixin.qq.com/minigame/dev/api-backend/login/api_code2session.html)。

- `WX.Login` 拿到 code，code 有效期 5 分钟、只能用一次。
- 服务器拿 code、AppID 和 AppSecret 调 `GET https://api.weixin.qq.com/sns/jscode2session` 换 openid。AppSecret 只放在服务器上。
- 服务器的域名要配进「开发管理 → 开发设置 → 服务器域名」，必须 HTTPS，并且做过 ICP 备案。
- 纯单机的广告游戏不需要登录。排行榜也不依赖 openid。

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR && !SHIP_NO_WECHAT
using WeChatWASM;

public static class WxLogin
{
    public static void Login(System.Action<string> onCode) => WX.Login(new LoginOption
    {
        success = (res) => onCode(res.code),
        fail = (err) => UnityEngine.Debug.LogWarning("WX.Login fail: " + err.errMsg)
    });
}
#endif
```

要昵称和头像时：
- 用 `WX.CreateUserInfoButton(x, y, w, h, "zh_CN", false)`，坐标是 Unity 像素、左上角原点。它会生成一个透明按钮盖在游戏按钮上。
- `OnTap` 回调里 `errCode == 0` 表示用户授权了。
- 前提是隐私指引里声明了昵称头像，并且用户已同意隐私协议。

## 震动、剪贴板、系统信息 `[官方]`

来源：[wx.vibrateShort](https://developers.weixin.qq.com/minigame/dev/api/device/vibrate/wx.vibrateShort.html)、[wx.setClipboardData](https://developers.weixin.qq.com/minigame/dev/api/device/clipboard/wx.setClipboardData.html)、[wx.getSystemInfoSync](https://developers.weixin.qq.com/minigame/dev/api/base/system/wx.getSystemInfoSync.html)。

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR && !SHIP_NO_WECHAT
using WeChatWASM;

public static class WxDevice
{
    public static void Buzz() => WX.VibrateShort(new VibrateShortOption { type = "light" });   // type 必填：heavy / medium / light
    public static string BaseLib() => WX.GetAppBaseInfo().SDKVersion;                        // 基础库版本
    public static void Copy(string text) => WX.SetClipboardData(new SetClipboardDataOption { data = text });  // 隐私接口
}
#endif
```

- `wx.getSystemInfoSync` 从基础库 2.20.1 起停止维护。改用 `WX.GetWindowInfo()`、`WX.GetDeviceInfo()`、`WX.GetAppBaseInfo()`。
- 震动只在 iPhone 7 以上和 Android 机型上有效。
- 剪贴板是隐私接口：单机游戏不要用；联网游戏要先声明。调用成功后系统会弹「内容已复制」提示。

## 订阅消息 `[官方]`

来源：[订阅消息](https://developers.weixin.qq.com/minigame/dev/guide/open-ability/subscribe-message.html)、[wx.requestSubscribeMessage](https://developers.weixin.qq.com/minigame/dev/api/open-api/subscribe-message/wx.requestSubscribeMessage.html)。

**前提**
- 要有服务器下发消息。
- 要在 MP「功能 → 订阅消息」配好模板。
- 个人主体能不能用订阅消息，**未确认**。

**限制**
- 原文：「仅在点击行为时调用」。
- Unity 的 UI 回调会晚一帧执行，所以要在 `WX.OnTouchEnd` 回调里调用 `WX.RequestSubscribeMessage`（[Unity QA](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/DevelopmentQAList.html)）。

## 隐私授权弹窗（联网游戏才需要）

`[官方]`

声明了「处理了用户信息」的游戏，要在调用隐私接口前取得同意。两种做法：
- 在后台开官方的「隐私授权弹窗」组件。一旦开了就关不掉。
- 自己做弹窗：
  - 用 `WX.OnNeedPrivacyAuthorization` 监听，弹出自己的界面；
  - 弹出后调用 `WX.PrivacyAuthorizeResolve(new PrivacyAuthorizeResolveOption { eventString = "exposureAuthorization" })`；
  - 用户点同意或拒绝时，再分别传 `"agree"` 或 `"disagree"`；
  - 界面里《隐私保护指引》的链接要用 `WX.OpenPrivacyContract(new OpenPrivacyContractOption())` 打开。

相关错误码：
- 104：用户没同意。
- 112：指引里没声明这个接口。
- 1025、1026：一项都没声明，或者声明了未处理任何信息。

来源：[隐私合规开发指南](https://developers.weixin.qq.com/minigame/dev/guide/open-ability/privacy.html)。本仓库实测的游戏是单机，没有走过这条路。
