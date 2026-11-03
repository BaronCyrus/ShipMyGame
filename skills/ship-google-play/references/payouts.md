# 收款：AdMob 广告收入和 Play 商家付款

个人开发者在中国大陆怎么把 Google 的钱收到手。两条线分开设置：

- **AdMob**：广告收入，在 AdMob 后台的「收款」里设置。
- **Play 商家账号**：内购、付费下载的收入，在 Play 管理中心的「付款资料」里设置。个人账号也能卖：中国大陆支持商家注册，默认币种美元 [官方](https://support.google.com/googleplay/android-developer/answer/9306917?hl=zh-Hans)。

本仓库的实测账号还没收到过付款（Play 停在封闭测试，用的是测试广告），所以本文全部是 `[官方]`，读取日期 2026-11-03。

**本人事项**：身份证件、PIN 码、银行账户、税务信息都由**用户**填。你打开页面、讲清每个字段，不替用户输入账号和证件号，也不把它们写进进度档案。

钱进了国内银行以后的事（外币账户、结汇额度、个税），见 [ship-my-game 的 payouts-china.md](../../ship-my-game/references/payouts-china.md)。

## 1. AdMob

### 1.1 门槛和每月节奏

门槛按账号的收入报告币种计算，官方表格里没有人民币这一档。美元账号的门槛 [官方](https://support.google.com/admob/answer/2772208?hl=zh-Hans)：

| 余额达到 | 发生什么 | 谁做 |
|---|---|---|
| 0 美元起 | 按所在地，可能要求提交税务信息（见 1.4） | 用户 |
| 10 美元 | 要求验证身份和地址（PIN 码）；可以选收款方式 | 用户 |
| 100 美元 | 达到起付金额，按月付款 | — |

余额以「收款」页的交易记录为准，不是估算收入。

每月节奏 [官方](https://support.google.com/admob/answer/3289671?hl=zh-Hans)：

1. 每月 3 日：上个月的收入定稿，计入余额。
2. 20 日（含）之前：改付款信息、解除暂停付款都要在这之前做完，余额也要在这天达到起付金额，否则顺延到下个月。
3. 21 日（遇周末节假日顺延）：发起付款，交易里出现「正在付款」。电汇可能要 10 天到账。

### 1.2 验证身份和地址（PIN 码）

依据：[获得付款的步骤](https://support.google.com/admob/answer/3001017?hl=zh-Hans)、[PIN 码概览](https://support.google.com/admob/answer/2772302?hl=zh-Hans)、[PIN 码问题](https://support.google.com/admob/answer/3198657?hl=zh-Hans)。

1. **身份**：收入到了验证门槛，首页会提示。**用户**在「收款 → 验证」提交身份证件，从第一次被要求起有 45 天。
2. **地址**：身份验证通过后，Google 用国际平邮把 6 位 PIN 码寄到收款地址。没有跟踪号，一般要等 3 周。
   - 收款地址要能收到平信。官方给的写法：第 1 行写城市、区县、街道、小区；第 2 行写楼栋、单元、楼层、门牌号；再填邮编。
   - 收到后，**用户**在「收款 → 验证」的「地址验证」里输入 PIN 码 [官方](https://support.google.com/admob/answer/17206017?hl=zh-Hans)。
   - PIN 码生成后 4 个月内要输入，过期会停止展示广告。输错 3 次也算失败，要申请替代 PIN 码。
   - 3 周没收到可以申请替代 PIN 码。替代 PIN 码和原来的一样，哪封先到用哪封。
3. **电话**：部分国家/地区还要验证电话，需要时首页会提示。号码的国家代码要和注册 AdMob 时选的国家一致 [官方](https://support.google.com/admob/answer/6149211?hl=zh-Hans)。

**先把姓名和地址填对**：身份验证后，收款人姓名一般就改不了；收款地址所在的国家/地区任何时候都不能改，搬到别的国家只能撤销账号重开 [官方](https://support.google.com/admob/answer/2772306?hl=zh-Hans)。

把「PIN 码在某日前输入」写进进度档案的「等待中」，PIN 码本身不写。

### 1.3 选收款方式

入口：「收款 →『您如何获得付款』→ 添加支付方式」，按页面说明选 [官方](https://support.google.com/admob/answer/2772229?hl=zh-Hans)。余额没到选收款方式的门槛时选不了。

**中国大陆能选什么**：

- AdSense 帮助中心按国家列的表里，中国是「电汇」和「PayPal Hyperwallet」，没有支票和电子转账（EFT）[官方](https://support.google.com/adsense/answer/1714397?hl=zh-Hans)。
- 2025-10-30 的公告说，中国的发布商可以用 Hyperwallet 接收 AdSense、AdMob、Ad Manager 的收入 [官方](https://support.google.com/adsense/answer/16691394?hl=zh-Hans)。但 AdMob 的付款时间页还写着「PayPal Hyperwallet（仅限美国境内的发布商）」[官方](https://support.google.com/admob/answer/3289671?hl=zh-Hans)。
- 两处说法不一致，以「添加支付方式」里实际出现的选项为准，看到的结果记进进度档案的「与 skill 不符」。

**电汇**（依据是 AdSense 的帮助页，AdMob 后台的按钮名称可能略有不同）[官方](https://support.google.com/adsense/answer/3372975?hl=zh-Hans)：

- Google 以美元电汇。
- 必填：账户所有人姓名、银行名称、SWIFT-BIC（8 或 11 位）、IBAN。选填：中转银行和它的 SWIFT-BIC、最终收款人（FFC/FBO）。
- 字段表有两处和国内对不上：没写收款人姓名用中文还是拼音；国内账户没有 IBAN。**填之前请用户向开户行要「境外汇入汇款」的收款指引**，照指引填。Google 也建议先向银行问清从国外接收电汇需要哪些信息。
- 所有信息都要和银行登记的完全一致。付款失败最常见的原因是：户名或账号对不上、没填 SWIFT 代码、没填中转银行 [官方](https://support.google.com/adsense/answer/6025222?hl=zh-Hans)。
- 手续费由银行收，问开户行。

**Hyperwallet**（出现这个选项时）[官方](https://support.google.com/adsense/answer/15292512?hl=zh-Hans)：

- 设置时要新建一个 Hyperwallet 账号，不能用已有的。
- 页面会给一个「收款人 ID」，激活 Hyperwallet 账号时要用。它不能告诉别人，也不写进进度档案。
- 收入到账后，再在 Hyperwallet 里添加转账方式提现。有些方式要付费，费用在 Hyperwallet 账号里看。

### 1.4 美国税务信息

- 入口：「收款 →『设置』→ 管理设置 →『付款资料』→ 美国税务信息 → 管理税务信息」[官方](https://support.google.com/admob/answer/2772513?hl=zh-Hans)。
- 不是所有发布商都要交。要交的话，20 日前交才能收到当月付款。
- 交哪张表按后台的税务向导选。Google 不提供税务建议，向导解决不了就请用户问税务顾问 [官方](https://support.google.com/admob/answer/4381375?hl=zh-Hans)。
- 同一页的常见问题写着：税务信息不完整或不正确时，可能被预扣最高 30% 的美国联邦税；已经扣的 Google 不退，要自己找美国国税局 [官方](https://support.google.com/admob/answer/4381375?hl=zh-Hans)。

**不用给 Google 开发票**：在 AdMob 页面底部点「条款」，第一段写着签约的 Google 实体 [官方](https://support.google.com/admob/answer/2772511?hl=zh-Hans)。Google Inc. 和 Google Advertising (Shanghai) 两个实体的纳税信息页都写着，按协议不需要你向 Google 开增值税发票 [官方](https://support.google.com/admob/answer/4381375?hl=zh-Hans)、[官方](https://support.google.com/admob/answer/4380951?hl=zh-Hans)。

### 1.5 钱没到

依据：[排查未收到付款](https://support.google.com/admob/answer/18561378?hl=zh-Hans)、[电汇常见问题](https://support.google.com/adsense/answer/6025222?hl=zh-Hans)。

1. 打开「收款 → 收款信息 → 查看交易」。没有付款条目，说明余额没到起付金额，或者账号处于暂停状态（税务信息、地址验证、身份验证没做完）。
2. 有付款条目但钱没到：从付款日起等满 15 个工作日，先问银行，再填 AdMob 的「未收到付款」表单。Google 可以提供 SWIFT 凭证（MT103 / UTR），给银行查款用。
3. 银行信息填错被退回：请银行取消这笔付款，Google 收回后重新发放，约 15 个工作日。

## 2. Play 商家账号（内购、付费下载）

开始在 Google Play 上创收后，个人账号会公开完整地址，见 [onboarding.md](onboarding.md) 第 1 节。只有账号所有者能管理商家付款账号 [官方](https://support.google.com/googleplay/android-developer/answer/13628312?hl=zh-Hans)。

### 2.1 建商家付款资料

- 入口：Play 管理中心「设置」→「变现」下的「付款资料」[官方](https://support.google.com/googleplay/android-developer/answer/13628312?hl=zh-Hans)。可以沿用注册开发者账号时的付款资料。
- 要填 [官方](https://support.google.com/paymentscenter/answer/7161426?hl=zh-Hans)：
  - 商家法定名称：会显示给买家，也会出现在收据上；
  - 法定地址：不能用邮政信箱；
  - 主要联系人；
  - 公开的商家信息：网站、商品类别、客服邮箱、买家银行卡对账单上显示的名称。
- 营业地点所在的国家/地区以后不能改。
- 要卖内购，还得先上传带结算权限的包，再建商品，见 [capabilities.md](capabilities.md) 第 4 节。

### 2.2 添加银行账户

中国商家的收入从美国以美元电汇发出 [官方](https://support.google.com/paymentscenter/answer/7161440?hl=zh-Hans)、[官方](https://support.google.com/googleplay/android-developer/answer/2700656?hl=zh-Hans)。

- 账户要求：能接收电汇；开在付款资料所在的国家/地区；以美元计价。Google 建议，开户行能开美元账户就开一个，否则银行会把美元换成本币入账。
- 步骤：付款资料 →「您如何获得付款」→ 添加付款方式（可能要重新验证身份）→ 账户持有人姓名 → 账户类型 → 银行信息 → 保存。
- 中国的字段：账户持有人姓名（最多 60 个字符，**必须和银行对账单上的完全一致**）、银行名称（最多 70 个字符）、SWIFT-BIC、IBAN；选填中转银行和中转 SWIFT-BIC、最终收款方（FFC/FBO）。
- 户名写法和 IBAN 栏同样对不上国内账户：**先请用户向开户行要「境外汇入汇款」的收款指引**，照指引填。
- 验证：可能要做存款测试，或者上传银行官方文件，最多 5 天 [官方](https://support.google.com/googleplay/android-developer/answer/13628312?hl=zh-Hans)。官方写存款测试只用于能用电子转账（EFT）的国家/地区 [官方](https://support.google.com/paymentscenter/answer/7161440?hl=zh-Hans)，中国走电汇，按页面提示做。

### 2.3 什么时候到账

- 每月 15 日开始支付上个月的销售收入；周末和节假日不付，顺延 [官方](https://support.google.com/paymentscenter/answer/7159355?hl=zh-Hans)、[官方](https://support.google.com/googleplay/android-developer/answer/137997?hl=zh-Hans)。
- 收美元电汇的，余额至少 100 美元才付，不够就累计到下个月 [官方](https://support.google.com/googleplay/android-developer/answer/2700656?hl=zh-Hans)。
- 电汇 5～7 个工作日到账 [官方](https://support.google.com/paymentscenter/answer/7159355?hl=zh-Hans)。银行收电汇的手续费通常 0～50 美元，问开户行 [官方](https://support.google.com/googleplay/android-developer/answer/2700656?hl=zh-Hans)。
- 「付款资料」页不实时更新，付款后 24 小时内更新 [官方](https://support.google.com/googleplay/android-developer/answer/137997?hl=zh-Hans)。
- Google Play 服务费见 [onboarding.md](onboarding.md) 第 3 节。

### 2.4 预扣税

- 卖给部分国家/地区用户的收入会被预扣当地的税，例如巴西（不用巴西雷亚尔收款时，默认按买家所付价格的 25%）、台湾（没有当地税号时 3%）。预扣的税在收入报告里单列，不能选择不扣；不想被扣，只能不在这些地方分发 [官方](https://support.google.com/googleplay/android-developer/answer/9384608?hl=zh-Hans)、[官方](https://support.google.com/paymentscenter/answer/13401799?hl=zh-Hans)。
- 付款资料的「设置」里要求填税务信息时，按页面要求填。Google 不提供税务建议。

## 3. 进度档案

- 「需要用户」写清下一项收款待办和期限，例如「AdMob：PIN 码在某日前输入」「AdMob：20 日前交税务信息」。
- 不写：银行账号、证件号、PIN 码、Hyperwallet 收款人 ID。
