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

**实测情况** `[实测]`（2026-11-11，个人主体的单机游戏，上线第 4 天）
- 走过的：开通后进流量主后台 → 建 3 个广告位 → 接激励视频、插屏、原生模板横幅 → 在开发者工具里验证 → 上传、提审。
- 没走过的：广告组件审核、线上的实际填充和收入。这几项按官方写。

### 开通

来源：微信广告帮助中心的[小游戏流量主开通流程](https://ad.weixin.qq.com/docs/237)。满足任一条件即可开通 `[官方]`：

1. 小程序累计独立访客（UV）超过 100，没有刷粉行为，也没有严重违规记录。
2. 开通内置游戏圈，并产生有效活跃（用户从游戏进入游戏圈后有消费或转评赞），还要发布 5 篇官方帖。
3. 同公司主体名下已有开通流量主、连续一个季度有变现的小程序。个人主体用不上这一条。

注意：
- 满足条件后资格要**次日**才刷新 `[官方]`。
- 开通时要同意《微信公众平台广告展示服务协议》等协议，由用户本人操作 `[实测]`。
- 前两个条件都需要上线后有真实用户。所以顺序只能是：上线 → 用户达标 → 开通流量主 → 建广告位 → 把 ID 填进游戏 → 再发一版。**首版审核期间接不了真实广告。**
- 门槛数字各处不一样：后台「流量主」页和帮助中心写「> 100」`[实测]`；开放文档的[广告](https://developers.weixin.qq.com/minigame/dev/guide/open-ability/ad/ad.html)页还写「> 500」。以后台页面为准。
- 开通后要马上补财务资料，否则结算会卡住，见 [payouts.md](payouts.md)。

**开通后的后台** `[实测]`
- 入口是左侧菜单「功能 → 流量主」，路径见 [console.md](console.md#读后台的技巧)。页签：概览、广告管理、数据统计、合作计划、财务管理、权限管理、公告消息。
- 第一次进会连着弹两样东西：
  - 两页欢迎弹窗，第二页是规则提示（禁止诱导点击、反对滥用组件），按钮「知道了」。
  - 「服务协议更新，请确认同意」，按钮「我已知晓并同意」。这是同意协议，留给用户点。弹窗不关，页面上别的都点不了。
- 概览的「待办」里有一条「财务信息」，见 [payouts.md](payouts.md)。

### 建广告位 `[实测]`

位置：「广告管理 → 广告位管理」。第一次进是「创建你的第一个广告位 → 自主开发接入」，以后是右上角的「新建广告位」。

| 广告位类型 | 表单 | 实测怎么选 |
|---|---|---|
| 激励广告 | 名称；最大激励时长 15 秒 / 30 秒 | 默认选中的是 15 秒，但页面提示「限制 15 秒的激励时长会降低eCPM与广告收入，非必要不建议勾选」，实测选 30 秒 |
| 插屏广告 | 名称 | — |
| 原生模板广告 | 名称；模板托管「优选模式 / 自定义模式」；模板选择 | 优选模式 → 卡片广告 → 横版卡片 → 20:7 横幅卡片。它就是以前的 Banner |

- 名称上限 30 字，只用来自己区分。一个位置建一个，例如「激励视频-道具」「插屏-过关」「横幅-底部」。
- **新建不了 Banner 广告位**：
  - 帮助中心原文：「原生模板广告（横幅模板）注：原Banner广告位」（[小游戏广告组件](https://ad.weixin.qq.com/docs/236)）`[官方]`。
  - 开放文档原文：Banner 接口「从基础库 3.5.5 开始，本接口停止维护，请使用原生模板广告 wx.createCustomAd 代替」`[官方]`。
- 自定义模式可以选横幅（左图右文、单图）和格子，「进入编辑」会在新标签页打开模板编辑器。优选模式不支持格子。实测没用自定义模式。
- 点「确认」后弹「创建成功」和 `adunit-` 开头的广告位 ID。回到列表，状态是「已开启」，「获取代码」给示例代码；横幅的示例是 `wx.createCustomAd({ adUnitId, style: { left: 0, top: 0, width: 350 } })`。
- 列表页原文：「组件审核: 首次提交小程序广告组件，需经过合规性审核才能在线上展示；审核时间为1个工作日，审核期间暂时关闭线上广告展示。」
- 建广告位是在改后台配置：用户说要接广告之后再建，建完把类型、名称和选项报给用户。
- 广告位 ID 写进游戏工程和进度档案，不要写进本仓库。

### 先读规范再接

来源 `[官方]`：[小游戏流量主运营规范](https://ad.weixin.qq.com/docs/239)、[平台运营规范](https://developers.weixin.qq.com/minigame/product/)。违规的处罚是驳回广告组件、扣掉全部未结算收入，重的冻结或终止账号。

| 条款 | 原文要点 | 接的时候怎么做 |
|---|---|---|
| 平台运营规范 | 广告不可遮挡「开始/继续游戏」等功能按钮；用户点这类按钮时不能打开广告；按钮或广告位置不能漂移 | 插屏不挂在按钮点击上；横幅位置固定，它出现、消失时按钮都不挪位置 |
| 2.3 强制点击 | 不能让所有内容都只能看激励视频才解锁 | 激励视频只给「提前拿到」或额外奖励，正常玩也拿得到 |
| 2.4 诱骗点击 | 移动 banner、高频点击后突然出现广告、用户没有广告预期 | 广告不出现在手指马上要点的地方 |
| 2.6 | 激励视频必须由用户点击触发 | — |
| 3.1.2 | 激励视频入口的描述要写明会播广告 | 入口文字写「看视频……」，只放一个视频图标不够 |
| 3.1.3、3.1.4 | 没看完不能发奖励；看完必须发 | 只认关闭回调里的 `isEnded` |
| 3.1.6 | 不能自己给广告做关闭按钮 | — |
| 3.1.7 | 一屏里不能有两个以上 banner、原生广告位 | 一个界面只放一条横幅 |
| 3.2.1、3.2.2 | 不能用内容遮挡广告；广告和可点击区域之间要有间距 | 横幅和按钮之间留出空隙 |
| 2.1 | 不能自己反复制造曝光和点击 | 测试时每种广告只看一两次，**不点广告内容** |

### 代码 `[实测]`

下面的类照实测游戏的代码整理，接口调用和实测一致，单独编译通过（SDK v0.1.34）。ID 留空的那一种广告不创建。

```csharp
using System;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR && !SHIP_NO_WECHAT
using WeChatWASM;
#endif

/// <summary>微信小游戏广告：激励视频、插屏、底部横幅（原生模板广告，20:7 横幅卡片）。ID 留空的那一种不创建。</summary>
public class WxAds : MonoBehaviour
{
    public static string RewardedId = "";       // 流量主开通后填 adunit-…
    public static string InterstitialId = "";
    public static string BannerId = "";

    public static WxAds I { get; private set; }
    /// <summary>底部给横幅让出的高度：从屏幕下沿算起，单位是屏幕像素；没有横幅时是 0。</summary>
    public float BannerHeightScreenPx { get; private set; }
    public static event Action BannerChanged;

    public static void Ensure()                 // 在 WX.InitSDK 的回调里、游戏界面建好之后调用
    {
        if (I != null) return;
        var go = new GameObject("WxAds");
        DontDestroyOnLoad(go);
        I = go.AddComponent<WxAds>();
        I.Init();
    }

#if UNITY_WEBGL && !UNITY_EDITOR && !SHIP_NO_WECHAT
    const float FirstInterstitialAfter = 180f, InterstitialGap = 150f;   // 游戏自己定的节奏，不是平台规定
    const int BreaksBetweenInterstitials = 2;
    const float BannerAspect = 7f / 20f;        // 20:7 横幅卡片的高宽比
    const float BannerMaxWidthCss = 350f;       // 后台「获取代码」的示例宽度
    const float BannerGapCss = 14f;             // 横幅和上面按钮之间多留的空隙
    const float BannerShowDelay = 0.6f;         // 能显示了也再等一下：手指可能还停在上一次点击的地方

    WXRewardedVideoAd _rewarded;
    bool _rewardedLoaded;
    Action<bool> _rewardedDone;
    WXInterstitialAd _interstitial;
    bool _interstitialLoaded;
    Action _interstitialDone;
    float _start, _lastFullScreenAd = -9999f;
    int _breaks;
    WXCustomAd _banner;
    bool _bannerLoaded, _bannerReserved, _bannerSuppressed, _bannerShowing;
    float _bannerShowAt = -1f;

    void Init()
    {
        _start = Time.realtimeSinceStartup;
        if (!string.IsNullOrEmpty(RewardedId)) CreateRewarded();
        if (!string.IsNullOrEmpty(InterstitialId)) CreateInterstitial();
        CreateBanner();                         // 先藏着加载，进到有横幅的界面时才可能显示
    }

    // ---------- 激励视频：只在玩家点了写明「看视频」的按钮之后播 ----------

    void CreateRewarded()
    {
        _rewarded = WX.CreateRewardedVideoAd(new WXCreateRewardedVideoAdParam { adUnitId = RewardedId });
        _rewarded.OnLoad(_ => _rewardedLoaded = true);
        _rewarded.OnError(e =>
        {
            Debug.LogWarning("[WxAds] rewarded " + e?.errCode + " " + e?.errMsg);
            _rewardedLoaded = false;
            Invoke(nameof(LoadRewarded), 30f);
        });
        _rewarded.OnClose(r => FinishRewarded(r != null && r.isEnded));   // 只注册一次；看完（isEnded）才发奖励
    }

    void LoadRewarded() => _rewarded?.Load(_ => { }, _ => { });

    public bool RewardedReady => _rewarded != null && _rewardedLoaded;

    /// <summary>onDone(true) 才发奖励。按钮先看 RewardedReady，没准备好就显示「暂时没有可播放的视频」。</summary>
    public void ShowRewarded(Action<bool> onDone)
    {
        if (!RewardedReady) { onDone?.Invoke(false); LoadRewarded(); return; }
        _rewardedDone = onDone;
        _rewardedLoaded = false;
        AudioListener.pause = true;
        _rewarded.Show(_ => { }, _ => FinishRewarded(false));
        _lastFullScreenAd = Time.realtimeSinceStartup;   // 刚看完激励视频，插屏往后推
    }

    void FinishRewarded(bool earned)
    {
        AudioListener.pause = false;
        var done = _rewardedDone;
        _rewardedDone = null;
        done?.Invoke(earned);
        LoadRewarded();
    }

    // ---------- 插屏：一局结束时调用，不要挂在任何按钮的点击上 ----------

    void CreateInterstitial()
    {
        _interstitial = WX.CreateInterstitialAd(new WXCreateInterstitialAdParam { adUnitId = InterstitialId });
        _interstitial.OnLoad(_ => _interstitialLoaded = true);
        _interstitial.OnError(e =>
        {
            Debug.LogWarning("[WxAds] interstitial " + e?.errCode + " " + e?.errMsg);
            _interstitialLoaded = false;
        });
        _interstitial.OnClose(FinishInterstitial);
    }

    /// <summary>显示了返回 true，广告关掉后调 onClosed（结算弹窗等它）；没显示返回 false，调用方照常继续。</summary>
    public bool TryShowInterstitial(Action onClosed)
    {
        _breaks++;
        float now = Time.realtimeSinceStartup;
        if (now - _start < FirstInterstitialAfter || now - _lastFullScreenAd < InterstitialGap) return false;
        if (_breaks < BreaksBetweenInterstitials || _interstitial == null || !_interstitialLoaded) return false;
        _lastFullScreenAd = now;
        _breaks = 0;
        _interstitialLoaded = false;
        _interstitialDone = onClosed;
        AudioListener.pause = true;
        _interstitial.Show(_ => { }, _ => FinishInterstitial());
        Invoke(nameof(FinishInterstitial), 60f);         // 保险：关闭事件万一没来，游戏不能一直等下去
        return true;
    }

    void FinishInterstitial()
    {
        CancelInvoke(nameof(FinishInterstitial));
        AudioListener.pause = false;
        var done = _interstitialDone;
        _interstitialDone = null;
        done?.Invoke();
        _interstitial?.Load(_ => { }, _ => { });
    }

    // ---------- 横幅：只在留了位置的界面上显示；它出现、消失，按钮都不挪位置 ----------

    /// <summary>进入、离开「有横幅的界面」时调用。进入那一刻横幅已经加载好才显示，位置一直留到离开。</summary>
    public void SetBannerScreen(bool on)
    {
        _bannerReserved = on && _banner != null && _bannerLoaded;
        float h = 0;
        if (_bannerReserved)
        {
            var win = WX.GetWindowInfo();
            float fromBottomCss = BottomInsetCss(win) + BannerWidthCss(win) * BannerAspect + BannerGapCss;
            h = fromBottomCss * Screen.height / (float)win.windowHeight;
        }
        if (!Mathf.Approximately(h, BannerHeightScreenPx))
        {
            BannerHeightScreenPx = h;
            BannerChanged?.Invoke();
        }
        RefreshBanner();
    }

    /// <summary>有弹窗、或这一局已经结束时传 true：藏起横幅，留的位置不动。可以每帧调用。</summary>
    public void SetBannerSuppressed(bool suppressed)
    {
        if (_bannerSuppressed == suppressed) return;
        _bannerSuppressed = suppressed;
        RefreshBanner();
    }

    bool BannerShouldShow => _bannerReserved && !_bannerSuppressed && _banner != null && _bannerLoaded;

    void RefreshBanner()                        // 藏是立刻藏；显示要晚 BannerShowDelay
    {
        if (BannerShouldShow)
        {
            if (!_bannerShowing) _bannerShowAt = Time.realtimeSinceStartup + BannerShowDelay;
            return;
        }
        _bannerShowAt = -1f;
        if (!_bannerShowing) return;
        _bannerShowing = false;
        _banner?.Hide(_ => { }, _ => { });
    }

    void Update()
    {
        if (_bannerShowAt < 0 || Time.realtimeSinceStartup < _bannerShowAt) return;
        _bannerShowAt = -1f;
        if (!BannerShouldShow || _bannerShowing) return;
        _bannerShowing = true;
        _banner.Show(_ => { }, _ => _bannerShowing = false);
    }

    static float BannerWidthCss(WindowInfo win) => Mathf.Min((float)win.windowWidth, BannerMaxWidthCss);

    static float BottomInsetCss(WindowInfo win) // 底部安全区（手势条）的高度
    {
        float safeBottom = (float)win.safeArea.bottom;
        return safeBottom > 0 ? Mathf.Max(0f, (float)win.windowHeight - safeBottom) : 0f;
    }

    void CreateBanner()
    {
        if (_banner != null || string.IsNullOrEmpty(BannerId)) return;
        var win = WX.GetWindowInfo();
        if (win.windowHeight / win.windowWidth < 1.9) return;   // 16:9 的屏和平板：这一条的高度要从游戏画面里挤，不放
        float width = BannerWidthCss(win), height = width * BannerAspect;
        _bannerLoaded = false;
        _banner = WX.CreateCustomAd(new WXCreateCustomAdParam
        {
            adUnitId = BannerId,
            adIntervals = 30,                   // 自动换素材的间隔（秒）
            style = new CustomStyle             // 逻辑像素。底部居中，贴着安全区上沿；建好以后位置不再变
            {
                left = Mathf.RoundToInt(((float)win.windowWidth - width) / 2f),
                top = Mathf.RoundToInt((float)win.windowHeight - BottomInsetCss(win) - height),
                width = Mathf.RoundToInt(width),
            },
        });
        _banner.OnLoad(_ => _bannerLoaded = true);   // 这里不显示：等下一次 SetBannerScreen(true)
        _banner.OnError(e =>
        {
            Debug.LogWarning("[WxAds] banner " + e?.errCode + " " + e?.errMsg);
            if (_bannerLoaded) return;          // 换素材失败：上一条还在
            DropBanner();
            Invoke(nameof(CreateBanner), 60f);
        });
        _banner.OnClose(() =>                   // 玩家自己关掉的：这个界面里空位留着，过一阵再建
        {
            DropBanner();
            Invoke(nameof(CreateBanner), 90f);
        });
    }

    void DropBanner()
    {
        _banner?.Destroy();
        _banner = null;
        _bannerLoaded = _bannerShowing = false;
        _bannerShowAt = -1f;
    }
#else
    void Init() { }
    public bool RewardedReady => false;
    public void ShowRewarded(Action<bool> onDone) => onDone?.Invoke(false);
    public bool TryShowInterstitial(Action onClosed) => false;
    public void SetBannerScreen(bool on) { }
    public void SetBannerSuppressed(bool suppressed) { }
#endif
}
```

**游戏这边怎么接** `[实测]`
- 激励视频：
  - 入口文字写明要看视频，例如「看视频：+3次提示」「观看视频立即解锁」。
  - 打开入口时看 `RewardedReady`，没准备好就把按钮换成「暂时没有可播放的视频」。
  - `onDone(true)` 才发奖励。
- 插屏：
  - 在一局结束的结算动画播完时调用，例如 `if (!WxAds.I.TryShowInterstitial(ShowResult)) ShowResult();`。显示了插屏，结算弹窗就等它关掉再出。
  - 不要在「下一关」「返回」这类按钮的点击里调用。安卓上常见的这种写法，在微信里正好是规范禁止的。
  - 玩家在结算动画期间已经离开这个界面时，不要再调。
- 横幅：
  - 只放在给它留了位置的界面上。实测只放在游戏进行的界面，列表和首页不放：横幅盖在一列可点的卡片上，容易误触。
  - 进这个界面时调 `SetBannerScreen(true)`，离开时调 `SetBannerScreen(false)`。界面订阅 `BannerChanged`，把贴底的按钮栏整体上移 `BannerHeightScreenPx`（先减掉底部安全区的高度，再换算成画布单位）。
  - 每帧调 `SetBannerSuppressed(有弹窗 || 这一局已经结束)`。弹窗的按钮可能离横幅很近，结算时又要出插屏。
- 播放激励视频和插屏期间暂停声音，回调里恢复，示例里用的是 `AudioListener.pause`。

**横幅的几个事实** `[实测]`
- 20:7 横幅卡片会按 `style.width` 缩放：宽 350 时实测高约 121（逻辑像素）。`adIntervals` 是自动换素材的间隔，实测设 30 秒。
- SDK 的 `WXCustomAd` 读不到广告的实际尺寸，高度要自己按 20:7 算。
- 这么高的一条放在 16:9 的屏上会挤掉游戏画面，所以示例在高宽比小于 1.9 的屏幕上不建横幅。

**激励视频的几个事实** `[官方]`（[激励视频广告](https://developers.weixin.qq.com/minigame/dev/guide/open-ability/ad/rewarded-video-ad.html)）
- 激励视频是全局单例，只创建一次，`OnClose` 也只注册一次。重复注册会导致一次关闭发好几次奖励。
- C# 收到的回调对象由 SDK 拼出来，不会是 null。所以不要照搬 JS 示例里的 `res === undefined` 分支：在 2.1.0 以下的基础库，`isEnded` 会是 false。
- 自动拉取失败后，`Show` 会走失败回调，要先 `Load` 再 `Show`。
- 每个用户每天能看的激励视频次数有限，所以按钮要能接受「暂时没有可播放的视频」。

**1004 兜底分享页** `[官方]`
- 基础库 3.7.7 起，遇到错误码 1004 时，平台会弹一个分享页，分享后也能拿奖励。这时触发的是 `onLoad`，带 `useFallbackSharePage`，`onError` 不触发。
- SDK v0.1.34 的 C# 参数里没有 `disableFallbackSharePage`，`WXADLoadResponse` 里也没有 `useFallbackSharePage`。
- 要关掉或识别兜底分享页，只能改 JS 或用 `CallJSFunction`，**未实测**。

**服务端校验**：可选。在流量主后台开启「服务端奖励回调」，需要服务器，单机游戏一般不用 `[官方]`（[激励广告反作弊](https://developers.weixin.qq.com/minigame/dev/guide/security/antiadcheat.html)）。

**插屏的几个事实**
- 组件创建后会自己拉一次广告，用户关掉后会拉下一条 `[官方]`（[插屏广告](https://developers.weixin.qq.com/minigame/dev/guide/open-ability/ad/interstitialAd-ad.html)）。
- 示例里的节奏（开局 180 秒内不出，两次全屏广告之间至少 150 秒、至少隔两局）是游戏自己定的，不是平台规定。

### 怎么测

**开通之前**：确认 ID 为空时按钮提示没有视频，游戏不卡、不报错。

**开通之后，先在开发者工具里测** `[实测]`
- 模拟器里三种广告都会出，而且是**真实的广告素材**。每种只看一两次，广告内容一下都不要点。
- 实测走过的检查：
  1. 横幅：只在该出现的界面出现；量一下它和按钮之间的空隙；打开弹窗时消失，关掉后回来，按钮位置始终不动。
  2. 激励视频：左上角从「N 秒后可获得奖励」变成「已获得奖励」后点「关闭」，奖励到账；计时类的数值没有在播放期间多走。
  3. 插屏：按游戏的节奏等够时间，过一局，先出插屏；点它右上角的 × 之后才出结算弹窗。
- 控制台里的 `SystemError (appServiceSDKScriptError) {"err_code":-1,"errMsg":"operateWXDataForAd:fail system apperror"}` 是模拟器自己的，栈全在 `WAGame.js` 里，可以忽略。
- 点模拟器画布要用全屏控制，见 [troubleshooting.md](troubleshooting.md#开发者工具和模拟器)。

**再请用户用体验版在真机上看一遍**
- 中途关闭激励视频不发奖励，看完才发。
- 没有广告可播时，入口提示没有视频，游戏照常。
- 真机上暂时没有广告不一定是代码的问题：广告组件第一次上线要过组件审核，见上面「建广告位」。

### 收入 `[官方]`

来源：[商业化](https://developers.weixin.qq.com/minigame/introduction/commercialization/)、[广告变现激励政策](https://developers.weixin.qq.com/minigame/introduction/commercialization/guide/ad-monetization.html)、[小游戏广告组件](https://ad.weixin.qq.com/docs/236)。
- 普通小游戏按单日广告收入流水的 50% 分成，不设上限；实际分成以后台「数据统计」页为准。
- 半月结算，在「流量主 → 财务管理」查看。
- 激励政策会调整，以页面为准。
- 个人不用开发票，腾讯代扣代缴个税。财务资料、结算时间和到账见 [payouts.md](payouts.md)。

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
- 用审核过的图片时，要在「游戏能力地图 → 运营素材管理 → 分享图片」先提交，审核通过后拿到 `imageUrlId`，和图片地址一起传。运营素材管理页的结构和其他素材见 [listing.md](listing.md#运营素材)。
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
