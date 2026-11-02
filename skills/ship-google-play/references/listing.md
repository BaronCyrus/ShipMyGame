# 商店资料：文案、素材、多语言

入口：左侧导航「商店发布 → 商品详情」（页面 `store-listings`）→「创建默认商品详情」或「修改默认商品详情」→ 编辑页 `store-listings/default/edit`。

- 直接打开 `main-store-listing` 会跳回首页，要从 `store-listings` 进 `[实测-失败 → 已修复]`。
- 编辑页分两步：「1 资源 → 2 评价」。
- 商品详情在所有测试轨道和正式版之间共用 [官方](https://support.google.com/googleplay/android-developer/answer/9859152?hl=zh-Hans)。

## 1. 文字字段

| 字段 | 上限（界面计数器） | 写法 |
|---|---|---|
| 应用名称 | 30 | 游戏名，可以加一个核心卖点。不要堆关键词，不要用别人的商标词 |
| 简短说明 | 80 | 一句话说清玩法和亮点。原文：「用户可以展开，以查看完整说明」 |
| 完整说明 | 4000 | 玩法、特色、模式、离线可玩等。不要写「最好」「第一」这类说法，也不要写价格促销 |

- 上限对全角、半角字符一样 [官方](https://support.google.com/googleplay/android-developer/answer/9859152?hl=zh-Hans)。重复、无关的关键词可能导致应用被暂停。
- 实测字数：名称 28、简短说明 78～79、完整说明 1305～1640 `[实测]`。
- 注入文字用 [console.md](console.md) 第 8 节的 `setv`，或者点击后逐字输入。填完读计数器（如「28/30」）确认框架已识别 `[实测]`。

## 2. 图形资源

| 素材 | 界面原文（实测） | 官方规格 | 本仓库做法 |
|---|---|---|---|
| 应用图标 | PNG 或 JPEG，≤1 MB，512×512 | 32 位 PNG（含 alpha），≤1024 KB，512×512 [官方](https://support.google.com/googleplay/android-developer/answer/9866151) | 512×512 PNG |
| 置顶大图（必填） | PNG 或 JPEG，≤15 MB，1024×500 | JPEG 或 24 位 PNG（**不含 alpha**），1024×500 | 1024×500，存成不含 alpha 的 PNG 或 JPEG，两边的要求都满足 |
| 手机截图 | 「为了符合宣传条件，请至少提供 4 张屏幕截图…」 | 至少 2 张；JPEG 或 24 位 PNG（不含 alpha）；边长 320～3840 px；长边不超过短边的 2 倍；每种设备最多 8 张。游戏想进推荐位：至少 3 张 16:9（≥1920×1080）或 9:16（≥1080×1920）的**实际游戏画面** | 8 张 1080×1920 JPEG（quality 90），8 张共约 2.7 MB `[实测]` |
| 平板、Chromebook、PC、XR 截图 | 选填 | 平板 / Chromebook 边长 1080～7680 | 不传 |

- 截图里不要写「Best」「#1」「Top」「New」「Discount」「Sale」「Million Downloads」这类排名、促销字样，也不要写「Download now」「Play now」这类行动号召；标语不超过画面的 20%；前三张优先放真实游戏界面 [官方](https://support.google.com/googleplay/android-developer/answer/9866151)。
- 截图和宣传图的做法（截图构建、adb、Pillow 合成）见 [store-assets.md](../../ship-my-game/references/store-assets.md)。
- 用 AI 生成或编辑过的图片要如实标注，见第 4 节。

## 3. 上传流程（资源库侧栏）`[实测]`

1. **先在页面里装 click 拦截补丁**。点「上传」按钮会弹出系统文件框，标签页会被冻住，浏览器工具也读不了这个页面 `[实测-失败]`：
   ```js
   if (!HTMLInputElement.prototype.__shipPatched) {
     const orig = HTMLInputElement.prototype.click;
     HTMLInputElement.prototype.click = function () {
       if (this.type === 'file') {
         if (!this.isConnected) { this.style.display = 'none'; document.body.appendChild(this); }
         window.__lastFile = this;          // 不弹系统框；随后对这个 input 直接注入文件
         return;
       }
       return orig.apply(this, arguments);
     };
     HTMLInputElement.prototype.__shipPatched = true;
   }
   ```
2. 点某个槽位的「添加资源」，右侧会滑出资源库面板。
3. 对面板里的 `input[type=file]` 直接用浏览器工具的上传功能注入文件，**不要点面板上的「上传」**。
   - 单次上传最多 10 MB，同一批上传的总和也算在内。截图转成 JPEG 一般够用；超过就分批，或用[本机中转](../../ship-my-game/references/uploads.md)。
4. 文件上传后会进入资源库并自动选中，点面板底部的「添加」放进槽位。
5. 核对：读 `img.naturalWidth/naturalHeight`，再看槽位按钮的 aria-label（如「移除“01_game.jpg”」）。
6. 标签页已经被冻住时：关掉这个标签页（对话框会跟着消失）→ 新开标签页 → 先装补丁 → 重新注入没保存的文字。

**一次添加 8 张截图，顺序会乱**（一次全部反序，一次乱序）`[实测-失败 → 已修复]`：
- 修法：在资源库里先「全部取消勾选」，再按 01→08 依次勾选，最后点一次「添加」。
- 或者逐张「选中 → 添加」。
- 添加完按 aria-label 读出槽位顺序核对。

## 4. AI 资源声明（第 2 步「评价」）`[实测]`

- 原文：「AI 资源声明：根据相关法规，在某些情况下，如果内容使用了 AI 生成的资源，则必须添加标签。」
- 选项：「不为资源添加标签」/「将资源标记为使用 AI 生成或编辑（您将在下一步中为各个资源添加标签）」。
- 图标、置顶大图、截图里有 AI 生成或编辑过的内容时（包括代码合成、但素材来自 AI 的图），选后者。在弹窗「为 AI 生成的资源添加标签」里逐个点选这些素材，点「为资源添加标签并提交」，再点底栏「保存」。
- 不确定某张图的来源时，先查做图脚本和素材出处，再决定。
- 保存后状态变成「可以送审」，会弹「要前往“发布概览”吗？」。

## 5. 多语言：「使用 AI 导入译文」`[实测]`

1. 你来写译文，存成 JSON，格式照 `templates/store-translations.example.json`：每种语言一项，包含 `languageCode`、`appName`（≤30）、`shortDescription`（≤80）、`fullDescription`（≤4000）。生成前在本地检查每个字段的长度。
2. 实测用过的 19 种语言：es-419、es-ES、pt-BR、fr-FR、de-DE、it-IT、ru-RU、uk、tr-TR、pl-PL、nl-NL、id、vi、th、ja-JP、ko-KR、zh-CN、zh-TW、hi-IN。加上默认的 en-US 共 20 种。两份 35～49 KB 的文件都被识别为 19/19。
3. 编辑页顶部横幅「使用 AI 导入译文」→「导入文件」→ 对话框里的 `input[type=file]` 直接注入 JSON → 等「正在从您的文件中导入译文…」（约 30 秒）→ 在「已识别的新语言」列表里点「全选」→「导入 19 份译文」→「下一步」→「保存」。
4. **必须逐语种核对，AI 会改字**：
   - 实测繁体中文的完整说明被吞了一个字（433 字变 432 字）`[实测-失败 → 已修复]`。
   - 方法：用页面上的「下一种语言」逐个切换，读三个字段，和本地文本的长度或哈希比对。
   - 不一致的地方用 `setv` 改回，然后整轮再核对一次。
5. 名称超过 30 字符时先删减，只留核心卖点，次要关键词挪进简短说明。
6. 首个版本已经在审核时导入了译文：保存时选「以后再说」，不要单独送审，等下一个版本一起提交。重新送审会取消正在进行的审核，见 [review.md](review.md)。

## 6. Play 游戏服务的「属性」素材

接 PGS 时，「Play 游戏服务 → 设置和管理 → 配置 → 修改属性」还要一套显示名称、说明、类别、游戏图标（512×512，≤1 MB）和置顶大图（1024×500，24 位 PNG 或 JPEG，**≤1 MB**）。可以直接复用商店素材，但置顶大图要压到 1 MB 以内 `[实测]`。翻译见 [capabilities.md](capabilities.md) 第 1.4 节。
