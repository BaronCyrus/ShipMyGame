# 账号：认证开发者、创建游戏、开通小游戏

本文对应 SKILL.md 的 P1。表格里的「谁做」：**你**指 agent，**用户**指本人事项。

## 1. 成为认证开发者

费用：官方文档没有提到任何费用，按免费处理 [官方](https://developer.taptap.cn/minigameapidoc/quick-start/guide/registration-login/)。本仓库的实测账号在记录开始前就已完成认证，这一节没有实测过程。

两种开发者类型 [官方](https://developer.taptap.cn/minigameapidoc/quick-start/guide/registration-login/)：

| 类型 | 要填的资料 | 审核 | 限制 |
|---|---|---|---|
| 注册开发者 | 厂商名称、邮箱、手机号（要验证）；英文名可选 | 不用审核 | 只能体验部分功能；最多建 2 个「未发布」游戏；**游戏不能提交审核**；不对玩家展示 |
| 认证开发者（个人） | 厂商名称、开发者姓名、常用地址、身份证正反面扫描件、邮箱、手机号（要验证）；英文名、QQ/微信可选 | 要审核，工作人员 2 个工作日内处理 | 可以上线游戏 |

要上线就必须是认证开发者。已经是注册开发者的，在「财务与管理 → 厂商设置 → 成为认证开发者」里转，不用重新申请；认证开发者不能再转回注册开发者 [官方](https://developer.taptap.cn/minigameapidoc/quick-start/guide/registration-login/)。

### 操作步骤

| # | 做什么 | 谁做 |
|---|---|---|
| 1 | 打开 https://developer.taptap.cn/ →「进入控制台」，请用户用自己的 TapTap 账号登录（一次登录） | 你开页面；用户登录 |
| 2 | 选开发者类型「认证开发者」，身份选「个人」 | 你 |
| 3 | 厂商名称：先和用户商量好，再由你填。规则见下文 | 你 |
| 4 | 英文名、邮箱、QQ/微信 | 你按用户给的信息填 |
| 5 | 开发者姓名、常用地址、身份证号、身份证正反面扫描件、手机号和短信验证码 | **用户** |
| 6 | 用三五句话讲清《TapTap 平台开发者协议》的要点，由用户勾选并提交 | **用户** |
| 7 | 等审核（2 个工作日内）。把开始日期和查看位置记进进度档案的「等待中」 | 你 |

### 厂商名称规范

来源：[官方](https://developer.taptap.cn/minigameapidoc/quick-start/guide/registration-login/) 和审核规范 2.1。

- 中国大陆开发者用中文名称，不要用全英文。
- 不要用昵称、游戏名或个人姓名。个人开发者**建议用「xxxx 工作室」**。
- 个人不得用企业名或其他平台的名称，例如带「科技 / 网络 / 软件 / 互娱 / 有限公司」字样。
- 不得含 #、*、& 这类符号、占位文字或乱码。
- 名称提交后**不能自己改**，要走工单。所以提交前要让用户确认最终名称。

### 身份资料要求

- 姓名必须和身份证一致。
- 身份证在有效期内，扫描件清晰。
- 加水印时，不能遮住姓名、号码、住址和有效期。

来源：[官方](https://developer.taptap.cn/minigameapidoc/quick-start/guide/registration-login/)。

### 以后想做广告时

TapADN（Dirichlet）账号的主体必须和 TapTap 开发者主体一致。这条是 2026-10 调研得出的，不在 TapTap 官方文档里，只当线索。Dirichlet 协议要求每次结算都附增值税专用发票，个人能不能代开还没确认，见 [payouts.md](payouts.md)。打算以后用个体户或公司收广告钱的，从认证这一步就要用那个主体。

### 信用等级

新开发者一般是 C 级，显示在开发者主页 `[实测]`。C 级的新游戏先进「专属流量池验证」；封闭测试最多 1 万人，每个游戏每季度 1 次，每轮不超过 30 天 [官方](https://developer.taptap.cn/docs/store/operation/developer-credit/)。这不影响提审。

## 2. 同意《TapTap 小游戏开发者协议》

- 协议更新后，打开任何小游戏页面都会被弹窗挡住，弹窗写着「开发者协议发生更新，确认内容并同意协议后可继续使用小游戏功能」`[实测]`。
- 这份协议以用户的名义签署，**你不点**：讲清要点，请用户滚到底部点同意。实测用户从读到点完约 10 分钟 `[实测]`。
- 以后每次打开后台，先看有没有这个弹窗。

## 3. 创建游戏

没有游戏时按下面做；已有游戏就跳过。

### 网页

步骤 [官方](https://developer.taptap.cn/minigameapidoc/quick-start/guide/creation-improvement/)：

1. 开发者中心 →「创建游戏」。
2. 填基础信息。**游玩提供方式**：只发小游戏就选「小游戏」。如果同时提供 APK 下载和小游戏，必须接 TapTap 登录，并保证两个版本的用户数据互通。
3. 点「保存并提交」，得到游戏 ID。这时游戏处于「未发布」状态，玩家搜不到。

其他规则：
- 「未发布」状态的游戏最多 20 个 [官方](https://developer.taptap.cn/docs/store/release/publish/create-game/)。
- 创建后会自动生成一个未发布版本「V-<日期>」，也就是商店资料草稿 `[实测]`。

### CLI（可选）

先按 [automation.md](automation.md) 装好 CLI 并登录，然后：

```bash
taptap-cli developer +list                      # 拿到 <开发者ID>
taptap-cli schema app create-app                # 先核对字段和枚举
taptap-cli app create-app --dev-id <开发者ID> \
  --data '{"title":"<游戏名>","category":"<类型枚举>","developer_role":"developer","package_type":"mini_app"}' \
  --idempotency-key create-<ASCII 短串> --dry-run
# 用户确认预览后：用同样的 --data 和 --idempotency-key，把 --dry-run 换成 --yes
```

- `package_type` 可选 `apk`、`mini_app`、`windows`、`h5`。这四个值来自 CLI 随包说明 [官方](https://www.npmjs.com/package/@taptap/cli)；`mini_app` 本仓库没实测过，执行前用 `schema` 核对。
- `category` 是英文枚举，例如 `casual` 休闲、`puzzle` 解谜、`trivia` 益智。完整列表看 `schema` 的输出。

## 4. 开通小游戏，拿到 ID

### 小游戏是否已开通

- 打开「商店 → 游戏包管理 → 小游戏管理 → 小游戏介绍」，页面显示「当前已开通 / 若需关闭，请联系我们」就是已开通 `[实测]`。
- 页面上「小游戏开通文档（必看）」这个链接失效了（404）`[实测]`。
- 没开通时的处理：
  - 网页上的开通按钮本仓库没见过，按页面提示操作；
  - CLI 有一个确认式开通命令，叫 `package-management enable-mini-app`（网页文档的写法）或 `get-or-create-mini-app`（随包说明的写法），以 `taptap-cli package-management --help` 为准。只有用户确认后才执行 [官方](https://developer.taptap.cn/v3/cli/skills/taptap-package-management.md)。

### 关键 ID 在哪

| ID | 位置 | 用途 | 依据 |
|---|---|---|---|
| MiniApp ID | 小游戏管理 → 开放能力 →「小游戏基本信息」：`MiniApp ID：… 复制`。同一处还有 `Secret：****** 查看` 和「适用地区：中国大陆」 | 打包时填 `publish/taptap/build.json` 的 `appid`。Secret 只在服务端换 openid 时用，单机游戏用不到，**不要查看、不要外传** | `[实测]` |
| 游戏 ID、开发者 ID | 后台网址 `https://developer.taptap.cn/<开发者ID>/app/<游戏ID>/…`；页面顶部也显示游戏 ID | CLI 的 `--app-id`、`--dev-id` | `[实测]` |
| Client ID | 「游戏服务 → 应用配置」，先点「立即开启」 | 主要给 APK 版 TapSDK 用；成就功能要求先开启应用配置 | [官方](https://developer.taptap.cn/docs/sdk/access/get-ready/) |
| 排行榜 ID | 创建后在排行榜列表的「排行榜 ID」列，由平台生成 | 代码里提交分数用 | `[实测]` |
| 成就 ID | 自己定义（英文和数字，不超过 40 个字符） | 代码里解锁成就用 | `[实测]` |

进度档案里可以写这些标识；Secret、密码、证件号不写（按 [state-file.md](../../ship-my-game/references/state-file.md)）。

## 5. 后台结构（找页面用）

**顶栏**：概览 / 商店 / 游戏运营 / 游戏服务 / 数据分析 / 游戏详情页；右上角是 TapTap CLI / 文档 / 论坛 / 工单 `[实测]`。

**「商店」左栏** `[实测]`：
- 版本管理：未发布版本「V-<日期>」。
- 游戏测试。
- 游戏包管理 → 小游戏管理：数据分析 / 基础配置 / 开放能力 / 包体管理 / 小游戏资质。另有 PC 包体管理。
- 资质与审核：游戏资质、版本审核记录。
- 买断制游戏。

**「游戏服务」左栏** `[实测]`：
- 应用配置。
- 基础服务：TapTap 登录、启动校验、合规认证、正版验证、云存档、多人联机、内购服务。
- 运营工具：TapTap 好友、成就、排行榜、内嵌动态、游戏角色查询、礼包、TapLink、直达礼包。
- 发行服务：APK 加固、推广平台、广告联盟、资源置换、内容营销。

**页面网址**：`https://developer.taptap.cn/<开发者ID>/app/<游戏ID>/<slug>`。直接拼网址能少点几层菜单 `[实测]`。

| 页面 | slug |
|---|---|
| 小游戏介绍 | `tap-mini-app` |
| 基础配置 | `tap-mini-app-config` |
| 隐私保护及权限设置 | `tap-mini-app-config/privacy-policy` |
| 开放能力 | `tap-mini-app-capability` |
| 包体管理 | `tap-mini-app-package` |
| 小游戏资质 | `tap-mini-app-qualification` |
| 成就 | `achievements_v4` |
| 排行榜 | `rank_v4` |
| 应用配置 | `configuration_center_v4` |
| 商店资料草稿 | `update-app-v2?panel=basic_info&version=V-<日期>` |

完成本文后，回到 SKILL.md 的 P1 完成标准逐项核对。
