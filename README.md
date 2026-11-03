<div align="center">

# ShipMyGame

**把这个仓库丢给你的 AI Agent，说一句「帮我把游戏发到 TapTap」，<br>它从注册开发者账号开始，一路做到提交审核。你只需要在浏览器里登录一次。**

Google Play · 微信小游戏 · TapTap 小游戏 ｜ Claude Code · Codex · Cursor 等 Agent 通用 ｜ MIT

</div>

---

## 这个仓库怎么来的

我让 AI 独立做了两款 Unity 6 小游戏，然后让它自己去 Google Play、微信小游戏、TapTap 上架。一路踩过的坑——游戏上线后完全没声音、WebGL 里中文全是方块、微信里黑屏、TapTap 准入测试连挂四版才过、网页后台传不了大文件、点「提交」被拦——全部整理进了这里，变成任何 Agent 都能照着走的步骤。

## Agent 会替你做什么

- **体检工程**：上架前会出事的地方先查一遍（没声音、中文方块、存档丢失、刘海屏和胶囊按钮遮挡、测试广告没换……）。
- **带你注册**：没有开发者账号也没关系。它打开注册页、告诉你每一步，只在付款、实名、签字、扫码时叫你。
- **打包和素材**：签名打包、导出小游戏包、做商店截图和演示视频、写多语言文案、生成隐私政策。
- **接管后台**：填表、上传包体和素材、配置成就和排行榜、写版本说明、核对审核要求。
- **提审前问你**：把要提交的内容列给你看，你说「提交」它才点。
- **记住进度**：审核和备案要等几天到几周，它把进度写在你的工程里，下次接着干。
- **讲清怎么收钱**：AdMob、Play 内购、微信流量主、TapTap 广告分别怎么收款，银行卡和税务资料怎么准备，美元进了国内银行以后结汇和个税要注意什么。

## 快速开始

**方式一：直接发链接**（任何能上网的 Agent）

在你的 Unity 工程目录里打开 Agent，发一句：

```text
用 https://github.com/BaronCyrus/ShipMyGame 帮我把这个游戏发布到微信小游戏
```

**方式二：装成技能**（Claude Code、Codex、Cursor 等支持 Agent Skills 的工具）

```bash
npx skills add BaronCyrus/ShipMyGame
```

**方式三：Claude Code 插件**

```text
/plugin marketplace add BaronCyrus/ShipMyGame
/plugin install ship-my-game@shipmygame
```

装好后直接说「帮我把游戏上架到 Google Play」就行。

## 渠道一览

| 渠道 | 实测走到哪 | 你本人要做的事 |
|---|---|---|
| **Google Play** | 两款游戏：注册 → 建应用 → 全部设置 → 封闭测试上线 → 多次更新送审；正式版申请条件和问卷已在后台核实 | 付 25 美元注册费、身份验证、找 12 名真实测试者连续测 14 天 |
| **微信小游戏** | 注册个人主体 → 开通快适配 → Unity 6 真机运行 → 小游戏备案提交；提审前置项已在后台核实 | 扫码登录、实名和人脸、在手机上截备案截图、认证缴费 |
| **TapTap 小游戏** | 注册认证 → 建游戏 → 命令行上传包体 → **Unity 6 的包通过包体准入测试** → 商店资料填写 → 前置备案材料 | 实名认证、手写签名承诺书并拍照、手机扫码自测 |

每条步骤都标了来源：`[实测]` 是真实账号上跑通过的，`[官方]` 是按官方文档整理、还在等实测的。

## 给 AI Agent 的说明

如果你是被用户叫来使用这个仓库的 Agent：

1. 把仓库放到用户电脑的工具目录，不要放进游戏工程：
   ```bash
   git clone --depth 1 https://github.com/BaronCyrus/ShipMyGame ~/.shipmygame/repo || git -C ~/.shipmygame/repo pull
   ```
2. 读 [`skills/ship-my-game/SKILL.md`](skills/ship-my-game/SKILL.md)，从第 1 步开始照做。
3. 用户的游戏工程就是当前目录，发布进度写在游戏工程的 `publish/STATE.md`。

## 隐私和安全

- 签名密钥、上传密钥只保存在你自己电脑的 `~/.shipmygame/keys/`，不进 git，不发给任何人。
- 付款、填收款银行账户、实名、签字、密码、验证码，永远由你本人完成。
- 点「提交审核」之前，Agent 一定先问你。
- Agent 优先用你自己的 Chrome（你本来就登录着这些后台）；没有浏览器能力时，它会教你装，或者一步步告诉你点哪里。

## 保持最新

各平台的后台和 SDK 一直在变。仓库每周一自动巡检官方文档和 SDK 版本（[GitHub Actions](.github/workflows/watch-official-docs.yml)），有变化就开 Issue，按 [MAINTAINING.md](MAINTAINING.md) 核对更新。

## 路线图

- [ ] Steam
- [ ] App Store / TestFlight
- [ ] 抖音小游戏
- [ ] 快手小游戏、国内安卓应用商店

想要哪个渠道，开 Issue 投票。只有真实走到「审核中」的渠道才会标为已收录。

## 反馈

Agent 在真实发布中发现后台和 skill 不一致时，会记在你工程的 `publish/STATE.md` 的「与 skill 不符」里。欢迎把这些贴到 Issue（去掉你的账号信息），我们核实后更新。

## License

[MIT](LICENSE)

## 支持作者

ShipMyGame 免费开源，赞赏全凭心意。如果它帮你把游戏顺利送进了审核，欢迎扫码支持，让这份上架手册持续跟上各个平台的变化。

<table align="center">
  <tr>
    <td align="center"><img src="docs/assets/sponsor-wechat.png" width="200" alt="微信赞赏码"><br><b>微信赞赏</b></td>
    <td align="center"><img src="docs/assets/sponsor-alipay.png" width="200" alt="支付宝收款码"><br><b>支付宝</b></td>
  </tr>
</table>
