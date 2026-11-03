# 注册与认证：Play 开发者账号、AdMob 账号

每一步都标了谁来做：**你**（agent）还是**用户**。付款、证件、验证码、法律协议的勾选都属于本人事项（规则见 [ship-my-game](../../ship-my-game/SKILL.md)）。

本仓库的实测账号是**个人账号**，用户在会话开始前就自己注册完了，所以注册流程本身没有实测记录。下面第 2～4 节都是 [官方] 依据，读取日期 2026-11-02。

## 1. 注册前先告诉用户：哪些信息会公开在 Google Play 上

注册前把这一节讲给用户，用户知情并同意后再开始。

| 信息 | 个人账号 | 组织账号 | 依据 |
|---|---|---|---|
| 开发者名称 | 公开；可以和法定姓名不同，随时可改 | 公开 | [官方](https://support.google.com/googleplay/android-developer/answer/13628312?hl=zh-Hans) |
| 法定姓名 / 组织名称 | **公开法定全名**（取自注册时关联的 Google 付款资料） | 公开 | 同上 |
| 国家或地区 | 公开（按法定地址） | 公开（随完整地址） | 同上 |
| 开发者邮箱 | 公开 | 公开 | 同上 |
| 完整地址 | 「在 Google Play 上创收」后公开 | 公开 | 同上 |
| 开发者电话 | 默认不公开。例外：声明为「交易者」后公开（见下）；韩国开发者的个人账号也必须公开 | 公开 | 同上；[官方](https://support.google.com/googleplay/android-developer/answer/6223646) |
| 联系邮箱、联系电话 | 不公开，只供 Google 联系你 | 不公开 | [官方](https://support.google.com/googleplay/android-developer/answer/13628312?hl=zh-Hans) |

**交易者身份声明（欧洲经济区和英国法规，2026 年起全球所有账号都要声明）** [官方](https://support.google.com/googleplay/android-developer/answer/17594752?hl=zh-Hans)：

- 新账号**创建时就要声明**；已有账号的截止日期是 2027-02-01，不声明可能被禁止更新、全球下架。
- 2026-10-29 的政策公告写的是「自 2026 年 11 月起，须按新的管理中心要求声明交易者身份」[官方](https://support.google.com/googleplay/android-developer/answer/18100766)。
- 判断标准举例（来自欧盟委员会指引）：
  - 以营利为目的，定期或以商业方式获得收入，例子里明确列出了付费应用、应用内购买和**靠广告获利的应用**；
  - 交易量大或频繁；
  - 按生意来经营应用开发和发布；
  - 有可以从事商业活动的法律身份，例如 D-U-N-S 编号。
  
  个人爱好、非商业活动一般不算交易者。拿不准就请用户咨询专业人士；Google 不替你判断。
- **声明为交易者的个人账号**：必须提供一个会在 Google Play 上显示的联系电话，**地址也会公开**。自 2027-03-01 起，各地用户都能看到这些联系信息。
- **声明为非交易者**：Google 会告诉欧洲经济区和英国的消费者，消费者保护法赋予的权利不适用于他们和你之间的合同。**不能为了不公开信息而虚报非交易者**：欧盟 UCPD 和英国 DMCCA 禁止这样做，Google 也可能因此在全球下架应用或终止账号。
- 声明位置：「管理账号 → 账号详情 → 关于您」。声明为交易者后，还要在每个应用的「监控和改进 → 政策和计划 → 应用内容」里确认交易者身份。

**其他地区的公开要求**：向日本用户卖付费应用或应用内购买时，要公开经营者的名称、电话和地址 [官方](https://support.google.com/googleplay/android-developer/answer/6223646)。

**个人想少暴露信息，可以这样做**（只在规则允许的范围内）：

1. 开发者邮箱单独用一个专门的邮箱，别用常用的私人邮箱。联系邮箱和开发者邮箱可以不同 [官方](https://support.google.com/googleplay/android-developer/answer/13628312?hl=zh-Hans)。
2. 开发者名称可以用工作室名或笔名，但个人账号**仍会显示法定全名**。
3. 只靠 AdMob 广告、不卖付费应用和内购时，是否算「在 Google Play 上创收」从而公开完整地址：官方原文只写了「如果您决定在 Google Play 上创收」，没有定义广告算不算，**未确认**。但广告收入会影响交易者身份的判断（见上）。
4. 不想公开家庭住址和个人电话时，可以考虑组织账号：公开的是组织的地址和电话，但需要 D-U-N-S 编号（免费，最多 30 天）和组织证件。注册哪种账号由用户决定。
5. 证件、姓名、地址一律用真实信息，并且和付款资料完全一致。验证不过的话注册费不退，开发者资料和应用可能被移除 [官方](https://support.google.com/googleplay/android-developer/answer/10841920)。

完成标准：用户明确说了「知道了，继续」，并选定了个人或组织账号。这个选择写进进度档案。

## 2. 注册 Play 开发者账号（用户做，你陪同）

依据：[官方 6112435](https://support.google.com/googleplay/android-developer/answer/6112435)、[13628312](https://support.google.com/googleplay/android-developer/answer/13628312?hl=zh-Hans)、[10841920](https://support.google.com/googleplay/android-developer/answer/10841920)。

1. **你**：打开 https://play.google.com/console ，请用户用打算长期使用的 Google 账号登录，进入注册流程。用户必须年满 18 岁。
2. **用户**：选择账号类型（个人 / 组织），填写：
   - 开发者名称；
   - 关联或新建 Google 付款资料（法定姓名、地址取自这里）；
   - 联系邮箱、联系电话（不公开）；
   - 开发者邮箱（公开）；组织账号还要填开发者电话（公开）和组织电话、网站。
   - 每个邮箱和电话都要填 6 位一次性验证码。
3. **你讲要点，用户勾选**：《开发者分发协议》。
4. **用户**：付 **US$25**（一次性）。可用 Mastercard、Visa、American Express、Discover（仅限美国）、Visa Electron（仅限美国以外）；**不接受预付卡**。
5. **用户**：身份验证。
   - 个人：政府签发的身份证件。
   - 组织：D-U-N-S 编号、政府证件、组织证明文件。
   - 材料必须和付款资料一致。各国可用的证件类型以页面列表为准。
6. **用户**：声明交易者身份（见第 1 节；新账号创建时就要声明）。
7. **用户（个人账号）**：Android 设备验证 [官方](https://support.google.com/googleplay/android-developer/answer/14316361?hl=zh-Hans)：
   1. 用账号所有者身份打开 Play 管理中心网页版 →「首页」→ 找到「验证您是否有权访问 Android 移动设备」→「查看详情」；
   2. 扫二维码，安装或打开「Play 管理中心」移动应用；
   3. 用账号所有者登录 → 选开发者账号 → 点按「验证」。
   
   要求：未 root 的 Android 实体机，Android 10 及以上。一台设备可以验证多个账号，通常不到 1 分钟。
8. **用户**：电话验证。前提是先完成身份验证，以及设备验证（个人）或网站验证（组织）。待办都显示在首页 [官方](https://support.google.com/googleplay/android-developer/answer/10841920)。

完成标准：
- 首页不再有待完成的验证任务。官方原文：账号验证完成前不能提交应用。
- 账号类型、注册日期（判断是否适用「12 人 × 14 天」）、验证状态都写进了进度档案。

## 3. 费用和时长

| 事项 | 费用 / 时长 | 依据 |
|---|---|---|
| 注册费 | US$25，一次性 | [官方](https://support.google.com/googleplay/android-developer/answer/6112435) |
| 设备验证 | 通常不到 1 分钟 | [官方](https://support.google.com/googleplay/android-developer/answer/14316361?hl=zh-Hans) |
| 身份证件审核 | **未确认**，官方没给时长；实测账号的通知显示「您的身份已成功通过验证」，但具体用时没有记录 | `[实测：通知原文]` |
| D-U-N-S 编号（组织） | 免费，最长 30 天；警惕收费代办 | [官方](https://support.google.com/googleplay/android-developer/answer/13628312?hl=zh-Hans) |
| 收款方式验证（卖付费应用或内购时） | 最长 5 天（小额存款验证或上传银行文件），只有账号所有者能操作 | 同上 |
| Google Play 服务费 | 加入 15% 档后，每年首 100 万美元收入按 15% 收取；实测账号收到过「您的账号群组已注册 15% 的服务费费率」 | [官方](https://support.google.com/googleplay/android-developer/answer/112622)、`[实测：通知原文]` |

中国大陆：支持开发者注册和商家注册，默认币种 USD [官方](https://support.google.com/googleplay/android-developer/answer/9306917)。

## 4. 账号建好后你要检查的事

1. 「首页」上的待办：身份、设备、电话验证，以及交易者身份声明。
2. **Android 开发者验证**：2026-09-30 起在部分地区生效，Play 开发者多数无需额外操作。
   - 约 99% 的 Play 应用已自动登记包名，新应用在 Play 管理中心创建时也会自动登记。
   - 在首页和「Android 开发者验证」页确认状态。
   - 依据：[官方](https://developer.android.com/developer-verification/guides/google-play-console)。
3. 要用 Developer API 上传包体时，在「用户和权限」里邀请服务账号，见 [automation.md](automation.md)。

## 5. AdMob 账号（要接广告时；用户做）

实测流程 `[实测]`：

1. **你**：打开 https://admob.google.com/ ，建议用户用和 Play 开发者账号同一个 Google 账号登录。
2. **用户**：在「让我们协助您开始」页面依次完成：
   - 选择是否接收个性化帮助；
   - 选择结算地址所在的国家/地区（以后不能改）；
   - 条款：你先讲要点，用户勾选「我已阅读并接受协议」；
   - 通过人机验证 → 点「开始使用 AdMob」。
3. **用户**：手机号验证（需要时）；填付款资料（姓名、账号类型、地址）。账号验证一般 24 小时内完成，少数要 2 周；验证完成前，应用一直显示「准备中」[官方](https://support.google.com/admob/answer/7356219)。
4. 填付款资料时提醒用户：
   - 地址要真实、能收到平信：收入到 10 美元后，Google 会把 PIN 码平邮寄到这里 [官方](https://support.google.com/admob/answer/2772302?hl=zh-Hans)；
   - 收款人姓名在身份验证后一般就改不了，收款国家任何时候都不能改 [官方](https://support.google.com/admob/answer/2772306?hl=zh-Hans)；
   - 银行和税务资料可以等收入到了门槛再补（经验建议）。收款全流程见 [payouts.md](payouts.md)。

建应用、广告单元、隐私消息、app-ads.txt 见 [capabilities.md](capabilities.md) 第 2 节。
