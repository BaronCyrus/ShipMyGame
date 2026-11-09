# 后台（mp.weixin.qq.com）逐页怎么填

本页写的是后台的页面、字段原文和推荐取值。标「已在后台核对」的，是 2026-11-02 在真实个人主体小游戏账号上只读核对过的，当时没有提交。

## 读后台的技巧

`[实测]`

**新版页面的正文读不到**
- 正文在微前端或 shadow DOM 里，`get_page_text`、`find`、`read_page` 往往只能读到左侧菜单。
- 读正文：用 JS 遍历 `el.shadowRoot` 取 `innerText`。
- 找按钮：同样遍历 shadow root，找 `innerText` 等于按钮文字的元素并 `click()`；或者截图后按坐标点击。
- 用 wujie 微前端的页面（例如「运营素材管理」），正文在 `document.querySelectorAll('wujie-app')[i].shadowRoot` 里。实测在 shadow root 里对按钮文字所在的 `span` 调 `click()` 没有打开编辑框；先 `scrollIntoView`，再用 `getBoundingClientRect()` 的中心乘以「截图宽度 ÷ `window.innerWidth`」算出坐标，真点一下就行 `[实测]`。
- 地址是 `/wxamp/frame/pluginRedirect/...` 的页面（例如推广视频列表里「上传素材」打开的页），正文在跨域 iframe（gamemp.weixin.qq.com）里，页面脚本读不到也改不了。同一个页面一般还有 `/wxamp/subApp/game/<路径>` 的地址，换成它，正文就在 wujie 的 shadow root 里了 `[实测]`。

**读通知中心**
- 审核结果、备案进展都发站内信，在「通知中心」（路径见下表）。
- 列表只显示日期。精确到秒的时间和正文在页面 Vue 数据的 `listInfo` 里：`title`、`create_time`（秒级时间戳）、`content`（HTML）`[实测]`。

**跳转页面要带 token**
- 后台网址都要带 `token=<MP会话token>&lang=zh_CN`，token 每次会话都不一样。
- 不要记 token。在已登录的标签页里读当前 URL 的 token，再拼出目标地址：

  ```js
  const t = new URL(location.href).searchParams.get('token');
  location.href = `https://mp.weixin.qq.com/wxamp/basicprofile/index?token=${t}&lang=zh_CN`;
  ```

**常用页面路径**（2026-10～11 实测，后台改版时以页面为准）

| 页面 | 路径 |
|---|---|
| 首页 | `/wxamp/home/guide` |
| 账号设置 | `/wxamp/basicprofile/index` |
| 版本管理 | `/wxamp/wacodepage/getcodepage` |
| 成员管理 | `/wxamp/user/managev2?action=index&use_role=1` |
| 开发管理 → 开发设置 | `/wxamp/devprofile/get_profile` |
| 游戏能力地图 | `/wxamp/subApp/game/minigame_abilitymap/main`（「研发能力」页签是 `/research`） |
| 生产提效包（快适配所在页） | `/wxamp/subApp/game/minigame_setting/performance` |
| 小游戏备案&发布流程指引 | `/wxamp/subApp/game/minigame_new_setting/record/step?type=2` |
| 小游戏备案表单 | `/wxamp/subApp/game/minigame/new-pre-approval-file?status=1` |
| 小程序备案表单（「验证备案类型」，提交后显示审核状态和「撤回备案」） | `/wxamp/webpocnew/verifyRecord` |
| 流量主 | `/wxopen/frame?t=promotion/promotion_frame&page=applet/open` |
| 运营素材管理（游戏简介、封面、推广图等，见 [listing.md](listing.md#运营素材)） | `/wxamp/subApp/game/minigame/material_resource_management` |
| 详细推广图上传页（从运营素材管理的「前往修改」进入） | `/wxamp/subApp/game/old_minigame_setting/gameset?pageR=gameset&mod=promot&type=vertical`（横版是 `type=horizontal`） |
| 推广视频列表（运营素材管理 → 推广视频） | `/wxamp/subApp/game/minigame/material-resource-management-material-video-list` |
| 推广视频上传页（列表页「上传素材」打开的是 `pluginRedirect` 版，脚本碰不到；用这个） | `/wxamp/subApp/game/old_minigame_setting/gameset?pageR=gameset&mod=video` |
| 通知中心 | `/wxamp/tools/wasysnotify` |

**登录会过期**
- 网页登录不到一天就会过期。过期后点「保存」会提示「保存失败」，要请用户重新扫码。
- 长流程要趁用户在场时一口气做完。
- 只关你自己新开的标签页。实测关一个过期标签页时，整个标签组都被关掉了。

## 左侧导航（已在后台核对）

- 「管理」：账号设置、版本管理、成员管理、开发管理、付费管理
- 「统计」：基础数据、商业化工具箱、收入诊断调优、实验工具
- 「功能」：游戏能力地图、运营功能管理、PC小游戏、流量主、广告主、虚拟支付、订阅消息、用户反馈、体验评价、客服、贴图话题
- 「成长」：成长计划、IAA新游专区、创意游戏、异业合作、RTA投放、CPS合作、一包多发、微信搜一搜
- 其他：违规记录、主体信用分、微信认证
- 「开发支持」：开发工具、研发工具箱、云服务、小程序码
- 通知中心

## 首页「小程序开发与发布流程」（已在后台核对）

**第一步 发布前准备**
- 完善基础信息：小程序基础信息、小程序类目、添加开发者、运营地区。运营地区的说明原文：「选择小游戏的上架地区，进入对应的运营及开发环境」。
- 代码开发与调试：方式一是自行开发，用开发者工具；方式二是找服务商。
- 备案与认证：
  - 小程序备案原文：「备案可与小程序代码开发并行，在小程序上架发布前完成即可」。提交以后，这里的标签显示审核状态：实测先是「平台审核中」，负责人做完工信部短信核验后是「管局审核中」`[实测]`。单机游戏实测没等它完成就发布了，见 [review.md](review.md#发布)。
  - 微信认证原文：「完成微信认证后，账号可获得"被搜索"和"被分享"能力。未完成微信认证不影响后续版本发布」。

**第二步 版本管理**：在微信开发者工具上传代码，然后选择版本提交审核，审核通过后的版本可以发布。

## 账号设置 → 基本设置（已在后台核对）

| 字段 | 页面显示 / 限制 | 怎么填 |
|---|---|---|
| 名称 | 每年可改 2 次（页面写认证后重置） | 纯中文游戏名，见 [listing.md](listing.md#名称简称头像介绍) |
| 简称 | 选填 | 名称较长时再填 |
| 头像 | 每年可改 5 次 | 游戏图标 |
| 介绍 | 每月可改 5 次 | 一两句话说清玩法 |
| 服务类目 | 例如「游戏 > 休闲游戏」 | 和游戏内容一致；个人主体不能选文化互动、角色、牌类 |
| 运营地区 | 「中国内地（大陆）地区」 | 保持不变 |
| 主体信息 | 个人 | 只读 |
| 微信认证 | 个人 | 见 [onboarding.md](onboarding.md#微信认证) |
| 适龄提示 | 「未设置」，提示「将在游戏启动进行展示」，按钮「去设置」 | 见下方「适龄提示」一节 |
| 小程序备案 | 「去完善」。实测提交以后这里仍显示「去完善」，审核状态要看首页「备案与认证」 | 见 [filing.md](filing.md#小程序备案icp) |

**「服务内容声明」区**
- 用户隐私保护指引：显示「未更新」，说明「基于微信提供的标准化用户隐私保护指引」，按钮「去完善」。见下方「用户隐私保护指引」一节。
- 隐私授权弹窗：可以设置。单机游戏不要开。原因：官方原文「该组件一经启用无法关闭」，见 [隐私合规开发指南](https://developers.weixin.qq.com/minigame/dev/guide/open-ability/privacy.html) `[官方]`。
- 用户生成内容场景声明：显示「未声明」。游戏里没有用户发布的内容就不用填。
- 订单中心 path：纯广告游戏不用填。

**「版本设置」区**
- 有「最低可用版本」「基础库最低版本」等项，默认 1.0.0。
- Unity 包要求的最低基础库版本，官方没有给出明确数字（**未确认**），所以先不改。
- 可以参考的官方信息：安卓微信 8.0.24 起支持 WebGL2；iOS 高性能模式要求 iOS 15.0 及以上 `[官方]`（[WebGL2](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/WebGL2.html)）。

## 适龄提示

**依据**
- 入口：「账号设置 → 基本设置 → 适龄提示 → 去设置」。下面的表单字段已在后台核对，提交后的审核本仓库还没有实测。
- 官方原文：「审核通过后平台统一协助在启动页向用户展示提示标记」，标记「固定展示在小游戏启动页《健康游戏忠告》右侧」 `[官方]`（[适龄提示设置指引](https://developers.weixin.qq.com/minigame/introduction/guide/slts.html)）。
- 所以《健康游戏忠告》和适龄标识都由平台展示，游戏里不用另做。
- 适龄提示没设置或没通过，会拖慢小游戏备案 `[官方]`（[小游戏备案 Q2](https://developers.weixin.qq.com/minigame/introduction/guide/nrjs.html)）。提审时也要求已完成 `[官方]`（[版本审核](https://developers.weixin.qq.com/minigame/introduction/guide/bbsh.html)）。所以要在 P4 就提交。

**表单字段和推荐取值**（以休闲益智、单人、卡通画风的游戏为例；按实际游戏改）

| 字段（页面原文） | 推荐取值 |
|---|---|
| 适龄等级自评：8+ / 12+ / 16+ | 8+。8+ 的说明原文：「玩法简单如休闲益智、模拟经营、音舞类等；需满足：没有写实或近写实画面风格和竞技表现、没有基于现实和历史改编、没有基于文字和语音自由交流的社交系统」 |
| 游戏类型 | 自动带出，例如「休闲」 |
| 虚拟支付 | 自动带出，「未开通内购」 |
| 游戏背景（≤10 字，页面示例「幻想虚构」） | 照实写，例如「日常生活」 |
| 画面风格和竞技表现：非写实（卡通/动漫/手绘等）/ 写实或3D写实 | 非写实（卡通/动漫/手绘等） |
| 有无基于历史和现实事件改编：无 / 有 | 无 |
| 主要玩法：单人模式 / 组队模式 | 单人模式 |
| 有无基于语音和文字的陌生人社交系统：无 / 有 | 无 |
| 游戏主题（≤10 字，页面示例「太空世界」） | 照实写，例如「逻辑解谜」 |
| 促进功能和价值（≤100 字） | 照实写游戏能锻炼什么，例如「通过逐步推理完成谜题，锻炼逻辑思维和专注力」 |

- 右侧会预览启动页上的 8+ 标识和适龄提示全文。
- 页面注释原文：「若游戏内已有适龄提示等级的展示，请与提交审核的适龄等级保持一致」。
- 按钮是「提交审核」。点之前把选择给用户看一遍，用户确认后再点。

## 用户隐私保护指引

入口：「账号设置 → 基本设置 → 服务内容声明 → 用户隐私保护指引 → 去完善」。下面的页面内容已在后台核对。

**单机游戏**
- 页面原文：「你的小游戏类型为单机游戏，已承诺并确保不处理用户个人信息。如需调整请到「版本管理-网络类型设置」修改为联网模式」。
- 选项有两个：
  - 「本小程序开发者承诺并保证，未以任何方式处理用户的任何信息…」：单机只能选这个。
  - 「本小程序处理了用户信息…」：单机时是灰的，不能选。
- 按钮「确认以上内容」。这是用户本人的承诺：你用一两句话讲清含义，即游戏不收集、不使用、不上传任何用户信息，等用户同意后再点。

**联网游戏，或者用到隐私接口的游戏**
- 先在「版本管理 → 网络类型设置」改成联网，再回来选「处理了用户信息」，逐项声明信息类型和用途 `[官方]`（[隐私合规开发指南](https://developers.weixin.qq.com/minigame/introduction/guide/privacy.html)）。
- 这条路本仓库没有实测。

**对能力接入的影响（推断）**
- 依据：单机时只能声明「未处理任何信息」。官方原文：声明了未使用任何用户信息时，调用隐私接口会报 1025/1026 `[官方]`（[隐私合规开发指南](https://developers.weixin.qq.com/minigame/dev/guide/open-ability/privacy.html)）。
- 由此推断，单机游戏用不了这些接口：好友排行榜（`setUserCloudStorage`、开放数据域）、剪贴板、昵称头像、游戏圈数据（`getGameClubData`）。
- 不受影响的：广告、分享、游戏圈按钮、本地存储、震动、系统信息。
- 什么时候决定：网络类型要在**小游戏备案之前**定好。备案第 1 步也有「网络类型」一项，备案以后再改信息，要重新走备案。

## 账号设置 → 游戏设置 → 资质与授权管理（已在后台核对）

| 区块 | 页面内容 | 要做什么 |
|---|---|---|
| 资质管理 | 「前往管理」，附《开发者自查指引》 | 提交资质（情况二），见 [filing.md](filing.md#资质提交) |
| 境外用户展示 | 未开通 | 不用动 |
| 中宣部实名认证系统接入 | 原文：「开通虚拟支付的网络游戏企业，请前往中宣部实名认证系统按要求完成接入。不开通虚拟支付，平台已统一协助接入中宣部实名系统，无需开发者操作。」 | 纯广告游戏不用操作，游戏里也不用接实名和防沉迷 |
| 运营授权验证 | — | 只有代理运营别人的游戏时才涉及（**未确认**） |

旁边还有一个子页签「功能设置」。

## 开发管理 → 开发设置（已在后台核对）

| 区块 | 字段 | 用途 |
|---|---|---|
| 开发者ID | AppID(小程序ID)；AppSecret(小程序密钥)，按钮「生成」 | AppID 写进 build.json。AppSecret 本流程用不到，不要生成 |
| 小程序代码上传 | 说明原文：「开发者可基于配置信息调用微信开发者工具提供的代码上传模块」。「小程序代码上传密钥」，按钮「生成」；「IP白名单」，按钮「编辑」 | 只有用 miniprogram-ci 时才要，见 [automation.md](automation.md#上传密钥和-ip-白名单) |
| 服务器域名 | 「开始配置」 | 单机游戏、首包资源放在包内时不用配。改用 CDN 或联网时才配，要 HTTPS 和 ICP 备案过的域名 |

## 游戏能力地图 → 研发能力 → 生产提效包 `[实测]`

| 项 | 用途 |
|---|---|
| 快适配 | Unity 包必须开通。点「立即申请」后立即变成「已开启」，见 [onboarding.md](onboarding.md) 第 5 节 |
| 高性能模式 | iOS 高性能模式。开通后 `game.json` 里的 `iOSHighPerformance: true` 才生效 `[官方]`（[iOS 高性能模式](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity-webgl-transform/Design/iOSOptimization.html)）。实测没开通，iOS 上的表现未验证 |
| 空间提升 | 本地存储从 200MB 提到 1G，条件是上传通用素材封面 `[官方]`（[空间提升](https://developers.weixin.qq.com/minigame/dev/guide/performance/diskspace.html)）。小游戏一般用不到；封面怎么做见 [listing.md](listing.md#运营素材) |

## 版本管理（已在后台核对）

- 三个页签：线上版本、审核版本、开发版本。
- 还没有开发版本时，页面显示：「你尚未上传任何开发版本，可前往开发者工具上传代码」。
- 提交审核的表单要先有开发版本才能看到，字段**未核对**，见 [review.md](review.md)。
- 「网络类型设置」在这里，用来在单机和联网之间切换（见上方「用户隐私保护指引」）。

## 成员管理

- 项目成员：运营者、开发者、数据分析者。
- 体验成员：只能打开体验版。
- 见 [onboarding.md](onboarding.md#成员和体验成员)。

## 流量主

入口：「功能 → 流量主」。开通条件见 [capabilities.md](capabilities.md#广告流量主)。上线前点不开，这是正常的。

开通后的财务资料和结算在「流量主 → 财务管理」，见 [payouts.md](payouts.md)。
