# 注册：Steam 账号、Steamworks 合作伙伴、分销计划和注册费

每一步都标了谁来做：**你**（agent）还是**用户**。付款、签协议、密码和验证码都是本人事项（规则见 [ship-my-game](../../ship-my-game/SKILL.md)）。

`[实测]` 的部分来自 2026-11-03 的一次真实注册：中国大陆、个人（独资经营）、新开的 Steam 账号，一直走到「税务和身份审核中」。[官方] 部分读取日期是同一天。

**保密**：Steamworks 后台页面和《Steam 分销协议》都是 Valve 的保密信息。本文只写公开文档里的规则和操作经验，不抄后台原文。

## 1. 注册前：Steam 账号和网络

**用哪个账号**：新开的账号和日常用的账号都可以。注册时登录的那个 Steam 账号，就是以后管理这个 Steamworks 账户、改银行和税务信息的账号。

新开的账号要注意两点：

- **受限账户**：在 Steam 上消费满 5 美元之前，新账号是受限账户，有些功能用不了。其中一条是不能添加某些国家/地区的手机号 [官方](https://help.steampowered.com/zh-cn/faqs/view/71D3-35C2-AD96-AA3A)。`[实测]`：付完 100 美元注册费后，Steam 客服页上不再显示受限提示；注册费算不算进这 5 美元，**未确认**。
- **Steam 令牌和手机号**：一个手机 Steam App 可以同时保护多个账号；一个手机号也可以绑多个账号。但绑同一个号码的账号可能被当作同一个人对待，VAC 封禁和游戏封禁会波及这些账号 [官方](https://help.steampowered.com/zh-cn/faqs/view/7EFD-3CAE-64D3-1C31)。

**网络和商店国家**（最容易踩的坑）：

- 账号的商店国家，按**第一次在 Steam 上购买时所在的位置**确定。以后要改，得到新国家用当地的支付方式买东西，而且三个月只能改一次 [官方](https://help.steampowered.com/zh-cn/faqs/view/2B3F-DAEF-846B-A0E8)。
- 新账号的第一笔购买通常就是 100 美元注册费 `[实测]`。
- 《Steam 订户协议》禁止用 IP 代理等方法隐瞒居住地，违反的可能被停用账号 [官方](https://store.steampowered.com/subscriber_agreement/)。
- `[实测-失败 → 已修复]`：开着代理建号，账号的商店国家被设成了美国，结账页只给了境外信用卡和 PayPal。关掉代理、用中国大陆的网络重新打开结账页后，商店国家变成了中国（大陆）。

所以：**建号和付注册费时都不要开代理。**

## 2. 第一段注册：Steamworks 合作伙伴和保密协议 `[实测]`

1. **先拿到收款账户认的英文户名，再开始填**。法定英文名称提交以后再改很麻烦（见本节末尾），所以这一步放在最前面：
   - Payoneer：**你**在用户登录的后台里看。「收款 → 收款账户」，点开美元账户，在「账户详情」里点「其他收款人名称」。下拉框里列的就是这个账户能收款的全部名称：账户持有人名称，加上审核通过的其他名称 `[实测]`。
   - 国内银行：请**用户**问开户行要境外汇款用的英文户名。
2. **你**：打开 https://partner.steamgames.com/ 。**用户**登录（手机 Steam App 扫码最快）。点「加入 Steamworks」。
3. **用户**填，你逐栏讲解：
   - **法定英文名称**：个人填自己的全名，中文名用拼音写，**照第 1 步拿到的户名原样填**，姓和名的顺序、空格都一样。官方要求：用法定姓名，不用昵称、DBA 或「好记名称」；和银行、税务文件上的名称完全一致；不能用英文以外的特殊字符 [官方](https://partner.steamgames.com/doc/gettingstarted/faq)。以个人身份登记的，银行账户户名必须和这一栏一致 [官方](https://partner.steamgames.com/doc/gettingstarted/onboarding)。
   - **公司形式**：个人填 `Sole Proprietorship` [官方](https://partner.steamgames.com/doc/gettingstarted/onboarding)。
   - **地址**：填当前住址，用英文或拼音写。后面的税务问卷还要填一次，两处保持一字不差；收款平台登记的地址最好也一样。
   - 付款通知邮箱和邮件语言。
4. **你讲要点，用户签**：保密协议。签字人职位，个人填 `Owner`；页面上的验证码由用户输入。

完成标准：主面板能打开；公司信息页显示的名称和地址和刚填的一致，名称和收款账户认的某个户名一字不差。

**名称和户名对不上会怎样** `[实测]`：银行信息存不进去（见 [payouts.md](payouts.md) 第 2 节）。补救的两条路都慢：

- 改 Steam 的法定名称：后台的「更改名称」只让联系客服，改名完成前不要改银行和税务信息。法定姓名本身变了的，官方的做法是用新名字重新登记一个合作伙伴账户，银行和税务信息也要重填 [官方](https://partner.steamgames.com/doc/gettingstarted/faq)。这次问过 Steam 客服，回复如下（这条路没实际走过）：
  - 名称不是本人的法定姓名、又和银行、税务信息对不上，才必须改。
  - 还没走完注册的，从头开始注册最省事：税务问卷还没进身份验证审核的，在注册流程里点「Start Over」，重新填名称。按钮名取自客服的英文回复，中文界面上叫什么没核对。
  - 税务问卷或身份验证在审核中的，审核完成前不能改。要改，得先请客服取消税务问卷和身份验证，「Start Over」才会出现；之后税务问卷和证件都要重新提交、重新排队。
- 让收款平台补登记 Steam 上的写法：要上传截图、等审核。实测第一次被拒，第二次当天通过，见 [payouts.md](payouts.md) 第 3 节。这次走的是这条路：Payoneer 登记了 Steam 上的写法以后，名称、银行、税务三处就一致了，没再改 Steam 的名称。

**拼音的顺序和证件上的姓名**：这次实测的 Payoneer 账户，持有人名称是名在前；护照上是姓在前。实测 Steam 填的是姓在前，身份验证传的是身份证，上面只有中文名。拼音的顺序、身份证上没有拼音，会不会让身份验证不通过，**未确认**，还在等结果。拿不准时先问 [Steam 客服](https://help.steampowered.com/wizard/HelpWithPublishing)。

## 3. 第二段注册：Steam Direct 分销计划和注册费 `[实测]`

1. **你**：主面板 →「添加功能」→「游戏或软件销售」→「现在注册」。第一段已经填过的名称和地址会沿用，不用重填。
2. **你讲要点，用户签**：《Steam 分销协议》。读页面上的协议全文，用三五句话讲给用户：收入分成、哪些东西不能放进游戏、协议期限和怎么终止、适用法律和争议怎么解决。**这些内容只讲给用户，不要写进任何会公开的文件。** 签字人英文全名和法定英文名称一字不差，职位填 `Owner`。
3. **用户**：付注册费。结账在 Steam 商店完成：
   - 每个产品 100 美元，不退；产品的调整后总收入满 1000 美元后，在之后的付款里返还；不能用 Steam 钱包余额 [官方](https://partner.steamgames.com/doc/gettingstarted/appfee)。
   - 能用哪些付款方式，取决于账号的商店国家（见第 1 节）`[实测]`。
   - 付完后，主面板会显示一个可以用来创建新应用的额度 `[实测]`。

完成标准：注册向导进入「输入支付和税务信息」；付费日期写进了进度档案。

## 4. 时间和费用

| 事项 | 时长 / 费用 | 依据 |
|---|---|---|
| Steam Direct 费 | 每个产品 100 美元，不退，收入满 1000 美元后返还 | [官方](https://partner.steamgames.com/doc/gettingstarted/appfee) |
| 银行、税务、身份审核 | 15～30 天；税务部分官方写一般 2～7 个工作日、最长 30 天 | [Steam Direct](https://partner.steamgames.com/steamdirect)、[常见问题](https://partner.steamgames.com/doc/gettingstarted/faq) |
| 交费到可以发售 | 30 天（入门文档写的是 21 天，按 30 天准备） | [Steam Direct](https://partner.steamgames.com/steamdirect)、[入门文档](https://partner.steamgames.com/doc/gettingstarted/onboarding) |
| 「即将推出」页面 | 至少公开 2 周 | [官方](https://partner.steamgames.com/doc/store/coming_soon) |
| 商店页审核 | 一般 3～5 个工作日 | [官方](https://partner.steamgames.com/doc/store/releasing) |
| 构建审核 | 1～5 天 | [官方](https://partner.steamgames.com/steamdirect) |

## 5. 会公开哪些信息

公开文档没写注册时填的信息里，哪些会显示给玩家，**未确认**。用户在意这一点的话，先问 [Steam 客服](https://help.steampowered.com/wizard/HelpWithPublishing)。
