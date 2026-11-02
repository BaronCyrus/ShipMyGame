# 排查：现象 → 原因 → 解决

标 `[实测-失败]` 的条目是真实发布中踩过的坑，表里的「解决」都是实际修好的做法。

## 1. 构建和运行

| 现象 | 原因 | 解决 |
|---|---|---|
| 游戏完全没有声音（音乐和音效都没有）。两款游戏都这样上线了好几个版本，是用户在真机上发现的 `[实测-失败]` | 场景里没有 `AudioListener`。用代码生成相机时最容易漏 | 在每个会启动的场景里放且只放一个 AudioListener。`ShipAndroidBuild` 发现数量不等于 1 时会打印 `[Ship] WARNING`，不要忽略。修好后请用户真机外放确认。先怀疑混音、加载方式是白费工夫 |
| 模拟器上验证不了声音：`pcm_writei failed … I/O error`；`AudioSource.GetOutputData` 一直是 0；QEMU wav 后端只录出 0 字节 `[实测-失败]` | 无头模拟器的音频输出不可用 | 声音只能请用户在真机上确认 |
| 打第二个版本时把上一版的 AAB 覆盖了 `[实测-失败]` | 构建脚本在设置新版本号之前就读了版本号来命名文件 | 用 `ShipAndroidBuild` 的 `-shipVersion -shipVersionCode`，文件名里带版本号 |
| 预览页提醒「没有与此 App Bundle 关联的去混淆文件…」 | 没开 R8 / ProGuard | 没开就忽略，不挡送审 `[实测]` |
| 预览页提醒「此 App Bundle 包含原生代码，您尚未上传调试符号文件」 | 符号表没打进 AAB | Unity 6 把 `DebugSymbols.level` 设为 `SymbolTable`，`format` 设为 `Zip` 加 `IncludeInBundle`，`ShipAndroidBuild` 已内置。旧写法 `androidCreateSymbols` 消除不了这条提醒 `[实测]` |
| 预览页「设备支持范围变化」表格 | 清单或 bundle 有变化 | 「不再支持的设备 / 新支持的设备」都是 0 就忽略 `[实测]` |
| 广告回调里报 `TypeInitializationException` `[实测-失败]` | 广告 SDK 的回调在 Java 线程上触发，直接碰了 Unity 对象 | 所有回调都用 `MobileAdsEventExecutor.ExecuteInUpdate` 派回主线程（`ShipAds.cs` 已这样写） |
| Gradle 下载依赖时 TLS 握手失败 `[实测]` | 本机网络要走代理 | 在 `Assets/Plugins/Android/gradleTemplate.properties` 里加 `systemProp.http.proxyHost/Port` 和 `systemProp.https.proxyHost/Port`，指向本机代理 |
| 构建中途报「Gradle build daemon has been stopped」`[实测]` | 多个会话共用一个 Gradle 守护进程，被别的会话杀掉了 | 每个工程用独立的 `GRADLE_USER_HOME` |
| Unity 6.6 编译报错：`PlayerSettings.Android.startInFullScreen` 不存在；`Round` / `Legacy` 图标类型报 CS0619 `[实测]` | API 已删除或过时 | 删掉这些行；新 Input System 下也不要再写 `Input.multiTouchEnabled` |
| AdMob 插件 `.unitypackage` 导入后 meta 文件格式偏旧 `[实测]` | PluginImporter serializedVersion 1 | 升级到 v3 格式，或改用 OpenUPM 安装 |
| `keytool` 报错或没反应（macOS） | `/usr/bin/keytool` 是空壳，没装 JDK | 用 Unity Android 模块自带的 keytool，`make_upload_keystore.sh` 会自动找 |
| Play 拒收或警告 16 KB 不兼容 | 旧的原生插件，或 Burst < 1.8.21，或 Unity 补丁版本太旧 | 升级 Unity 到 6000.0.38f1+ / 2022.3.56f1+，Burst 1.8.21+，更新第三方原生 SDK；按 [build.md](build.md) 第 4 节检查 [官方](https://developer.android.com/guide/practices/page-sizes) |
| 上传时 targetSdk 不合规 | 2026-08-31 起新应用和更新都要 target 36 | Target API Level 设成 36，构建后用 bundletool 确认 [官方](https://support.google.com/googleplay/android-developer/answer/11926878) |

## 2. 上传文件和页面操作

| 现象 | 原因 | 解决 |
|---|---|---|
| 点「上传」后标签页卡死，报 `Runtime.evaluate timed out … The renderer may be frozen`，浏览器工具和屏幕点击都没反应 `[实测-失败]` | 弹出了系统原生的文件选择框 | 关掉这个标签页（对话框会跟着消失）→ 新开标签页 → 先装 [listing.md](listing.md) 第 3 节的 click 补丁 → 对 DOM 里的 file input 直接注入文件 → 重新填没保存的文字 |
| AAB 传不上去：「total upload size would exceed 10 MB」`[实测-失败]` | 浏览器工具单次上传上限 10 MB；AAB 有 59～88 MB | 首个 AAB 请用户拖拽，见 [uploads.md](../../ship-my-game/references/uploads.md) 第 4 节。之后可以走 API（[automation.md](automation.md)） |
| 把 AAB 切块在页面里拼接，第二块被权限系统拦截 `[实测-失败]` | 被判为绕过限制 | 立即停止，清理页面里的临时数据和本地分块，改请用户拖拽。**不要想办法绕过拦截** |
| 一次添加 8 张截图，顺序反了或乱了 `[实测-失败]` | 资源库按选中顺序以外的规则排序 | 先「全部取消勾选」，按 01→08 依次勾选，再点一次「添加」；或逐张添加。最后读 aria-label 核对顺序 |
| 「使用 AI 导入译文」后，某种语言少了一个字 `[实测-失败]` | 导入时 AI 改了文字 | 逐语种读回，和本地文本比对长度或哈希；用 setter 改回后整轮再核对 |
| 填好的隐私政策网址被清空 `[实测-失败]` | 误点了「保存」左边的「舍弃」，在确认框里点「取消」后输入框被清空 | 重填，点最右边的蓝色「保存」，看到「更改已保存」 |
| 内容分级问卷只答了一部分 `[实测-失败]` | 手风琴结构，批量点击只对已展开的组生效 | 用 [console.md](console.md) 第 8 节的按组脚本，再逐题核对 |
| 标签选成了无关的；广告 ID 用途把「分析」勾成了「应用功能」`[实测-失败]` | `find` 返回的 ref 在表格行里错位 | 先搜索再点；点完一定读回已选项核对 |
| 坐标点击落在输入框外 `[实测-失败]` | 页面布局偏移，或页面横向滚动了 | 把所有元素的 `scrollLeft` 置 0；读回输入值核对 |
| 直接打开 `main-store-listing` 跳回了首页 `[实测-失败]` | 网址不对 | 从 `store-listings` 进入，再点「修改默认商品详情」 |
| 数据安全对话框保存后又回到了草稿 | 点成了「保存为草稿」 | 用文字区分两个按钮，点对话框里最后一个可见的「保存」`[实测]` |
| 「发布概览」的新手引导用 Escape 关不掉 | — | 点三次「下一步」`[实测]` |

## 3. 后台和送审

| 现象 | 原因 | 解决 |
|---|---|---|
| 发布概览报「“广告 ID”声明不完整」，送不了审 `[实测-失败]` | 「广告 ID」不在信息中心的 11 项设置任务里，被漏掉了 | 「应用内容 → 广告 ID」选「是」并勾选用途。以后送审前一律先把「应用内容」页清空 |
| 点「提交 N 项更改以供审核」被权限系统拦下 `[实测-失败]` | 用户在当轮对话里没有明确要求提交 | 走提审闸门，等用户说「提交」；仍被拦就把页面停在按钮上，请用户点 |
| 弹出「要重新发送审核申请吗？…系统会取消当前审核进程并重新开始审核」`[实测]` | 已有改动在审核时又提交了新改动 | 能等就点「取消」，等这次审核结束再一起提交。用户坚持就重新发送，但等待时间会增加 |
| 点「创建应用」时浏览器扩展断线 `[实测]` | 扩展间歇性断线 | 不要重试，否则会建出两个应用。截图确认按钮变灰、左下角在转圈，等它完成 |
| 测试人员数突然变少 `[实测]` | 从邮件列表切到了 Google 群组，原列表里的人不再算测试者 | 请这些人（包括用户自己的其他账号）加入群组 |
| 测试者打不开加入链接 | 首次发布后链接要过几个小时才生效；用了群组时，要先加入群组 | 等几个小时；提醒测试者先用同一个账号加入群组 [官方](https://support.google.com/googleplay/android-developer/answer/9845334?hl=zh-Hans) |
| 首次发布后，测试者看到的是临时应用名 | 首次上传后最多 48 小时显示临时名称和商品详情 | 等待 [官方](https://support.google.com/googleplay/android-developer/answer/9845334?hl=zh-Hans) |
| 更新被拒 / 应用被拒 | 不符合政策（本仓库实测没遇到过） | 读邮件和「政策状态」里的原因，修好后在「发布概览」手动点「送审」，或者申诉。见 [review.md](review.md) 第 4 节 |
| 申请正式版后被要求继续测试 | opt-in 不足 12 人，或参与度不够 | 继续招人、发更新、收集反馈，满足后重新申请 [官方](https://support.google.com/googleplay/android-developer/answer/14151465?hl=zh-Hans) |

## 4. Play 游戏服务

| 现象 | 原因 | 解决 |
|---|---|---|
| OAuth 同意屏幕的「发布应用」是灰的 `[实测]` | 「品牌塑造」页没填完 | 填应用首页、隐私权政策链接、已获授权的网域、开发者联系信息后保存。不要上传徽标，上传后要过验证 |
| 点「发布应用」或「发布游戏项目」被权限系统拦下 `[实测-失败]` | 属于账号级设置变更 | 由用户点，你说明原因：不点的话，除测试人员外没人能登录 |
| 模拟器日志 `sign-in: Canceled` `[实测]` | 模拟器没登录 Google 账号 | 正常现象；请用户在真机上、从测试轨道安装后验证 |
| 真机登录失败或报 OAuth / 404 错误 | 常见原因：SHA-1 或包名和凭据不符；账号不在测试人员名单里；PGS 配置或同意屏幕没发布；开了反盗版却用 adb 安装；`app_id` 填了完整客户端 ID | 逐项核对 [capabilities.md](capabilities.md) 第 1.3、1.7、1.9 节；打开 `adb shell setprop log.tag.Games VERBOSE` 看日志 [官方](https://developer.android.com/games/pgs/android/troubleshooting) |
| PGS「添加或移除语言」对话框不认 JS 点击 `[实测-失败]` | 这个组件只响应真实输入 | 在搜索框里逐个输入语言代码再勾选 |
| 排行榜语言下拉用 JS 打不开 `[实测]` | 选项只有在真实点击后才会渲染 | 先真实点击打开下拉，再用 `__setLoc` 选语言 |
| 成就导入报格式错 | CSV 里有英文逗号、带了表头、有子目录，或本地化里写了默认语言 | 用 `scripts/pgs_achievements_zip.py` 生成；本地化里的语言要先在「属性」里加好 |

## 5. AdMob

| 现象 | 原因 | 解决 |
|---|---|---|
| 真实广告单元 ID 加载失败：`Ad failed to load : 3`，`Account not approved yet` `[实测]` | 新账号还在审核 | 测试期一律用测试 ID；拿不到广告时隐藏「看广告」入口 |
| GDPR 消息的「发布」一直是灰的 `[实测]` | 没有设置「不同意」按钮 | 打开「不同意」设置，用表头开关对所有国家开启 |
| 应用一直「需要审核」或广告受限 | 应用还没公开上架，或没有关联商店 | 正式版上线后关联商店，并完成 app-ads.txt 验证（[capabilities.md](capabilities.md) 第 2.7 节） |
| app-ads.txt 找不到 | 不在开发者网站域名的根目录（例如放在 GitHub Pages 的项目站点下） | 用用户站点的根目录；`curl` 确认返回 200 |
| 启动就崩溃：`Missing application ID` | 没填 AdMob App ID | 在 GoogleMobileAdsSettings 里填（测试期填示例 App ID）[官方](https://developers.google.com/admob/android/quick-start) |

## 6. API

| 现象 | 原因 | 解决 |
|---|---|---|
| 403 或「没有权限」 | 服务账号权限还没生效（最长 48 小时），或没给这个应用授权 | 等待；在「用户和权限」里核对应用权限 [官方](https://developers.google.com/android-publisher/api-ref/rest/v3/users) |
| `Only releases with status draft may be created on draft app` | 应用还没在任何轨道发布过（只有第三方文档记录，官方没有明文） | 用 `--status draft`，到网页完成第一次发布 |
| `The query parameter changesNotSentForReview must not be set` | 这个应用的改动会自动送审 | 停下来走提审闸门，用户同意后用 `--send-for-review` 重跑（[automation.md](automation.md) 第 3 节） |
| `Please set the query parameter changesNotSentForReview to true` | 上一次提交被拒，改动不能自动送审 | 带上这个参数提交，再到网页手动点「送审」 |
| 400 `CHANGES_ALREADY_IN_REVIEW` | 有改动正在审核，`ERROR_IF_IN_REVIEW` 起了作用 | 等那次审核结束再提交 |
| edit 提交时报 edit 无效 | 期间有人在网页上改了这个应用，或者又新建了一个 edit | 重新跑一遍脚本；运行期间不要在网页上操作这个应用 |

## 7. 浏览器工具

| 现象 | 解决 |
|---|---|
| 读页面只拿到侧栏导航 | 执行 `(document.querySelector('main')||document.body).innerText` `[实测]` |
| 抓取工具读不了 support.google.com、developers.google.com（报无法验证域名是否安全） | 在浏览器里读，或用 `curl` `[实测-失败]` |
| reddit.com 打不开（「This site is not allowed due to safety restrictions」） | 帖子由你写好，用户自己发 `[实测-失败]` |
| 页面变成 `chrome-error://chromewebdata/` | 网络或代理抖动：用 `curl` 轮询 `https://play.google.com/console/`，等返回非 000 后重新打开页面 `[实测]` |
| 扩展报「not connected」或「connection was lost」 | 会改状态的操作先截图确认结果，再决定要不要重试；批量操作每批控制在 4～5 个动作 `[实测]` |
| 同时连着两台机器的 Chrome，下载的文件不在本机 | 先列出已连接的浏览器，选标记为本机的那台 `[实测]` |
| 标签组被自动解散（关掉了组里最后一个标签页） | 新建标签页继续 `[实测]` |
| GitHub 免费账号的私有仓库开不了 Pages | 用公开仓库，或者由用户决定是否付费升级 `[实测]` |
