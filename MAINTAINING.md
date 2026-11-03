# 维护本仓库

本仓库的价值在于「照着做一定能走到提审」。官方后台和 SDK 一直在变，所以维护有两条线：每周自动巡检官方来源，和把真实发布中遇到的「与 skill 不符」改回来。

## 来源标记

每条步骤带一个标记，改动时同步更新：

- `[实测]`：在真实账号上做过并成功。写清楚是哪个阶段验证的，不写账号、游戏名、ID。
- `[官方]`：按官方文档整理，还没人实测走过。附官方链接。
- 实测失败的做法写进各渠道的「排查」文档，说明现象、原因和正确做法。

只有在真实后台走通后，才能把 `[官方]` 改成 `[实测]`。

## 官方来源巡检

- 来源清单：[`tools/sources.json`](tools/sources.json)。每项写明它决定了 skill 里的哪条规则（`note`）和受影响的文件（`affects`）。
- 脚本：[`tools/watch_sources.py`](tools/watch_sources.py)，只用 Python 标准库。本地运行：`python3 tools/watch_sources.py`。
- 自动运行：[`.github/workflows/watch-official-docs.yml`](.github/workflows/watch-official-docs.yml) 每周一 09:00（北京时间）运行，也可以在 Actions 页面手动运行。它刷新 `tools/snapshots/` 里的快照；有变化时开一个带 `docs-changed` 标签的 Issue，里面有差异节选和受影响的文件。
- 页面里每次都会变、但与规则无关的行（日期、计数器）会造成误报：在该来源的 `ignore` 里加正则。
- 正文外面套着很长的站点导航时（例如 Steamworks 文档的侧边栏，别处加一篇文档就会让每个来源都报变化），用 `from` / `to` 两个正则只保留正文：`from` 匹配要保留的第一行，`to` 匹配正文后第一行不要的。匹配不到时保留整页，页面改版会作为变化报出来。
- 抓取失败（超时、页面改成纯 JavaScript 渲染）只记在报告里，下次重试；连续失败时换一个能直接读到文字的官方页面。

## 处理巡检 Issue

交给 agent 时可以直接说：「处理本仓库最新的 docs-changed Issue」。步骤：

1. 读 Issue 里的每个来源：打开官方页面读全文，不只看差异节选。
2. 判断变化是否影响规则：限制数字（包体大小、测试人数、targetSdk）、流程（新增步骤、按钮改名）、SDK 版本和接口、费用和时长。只是措辞或排版变化就不用改。
3. 改 `affects` 里列出的文件；SDK 新版本要确认 `fetch_*.sh` 的补丁是否仍然需要（脚本会报「already fixed upstream」或「PATCH NOT APPLIED」）。
4. 能在真实后台核实的（例如有账号的维护者），核实后把标记改成 `[实测]`。
5. 提交 PR，描述里逐条写：来源、变化、改了什么、是否实测。合并后关闭 Issue。

## 回收「与 skill 不符」

agent 在真实发布中会把后台与 skill 不一致的地方记进用户工程的 `publish/STATE.md`。收到这类反馈（Issue 或 PR）时，按上面的第 3～5 步处理。

## 隐私规则（所有贡献都适用）

提交前检查：不出现真实姓名、邮箱、手机号、证件号、地址；不出现开发者 ID、应用 ID、AppID、MiniApp ID、项目编号、OAuth 客户端 ID、证书指纹；不出现带个人用户名的网址、本机绝对路径、任何密钥。用 `<包名>`、`<AppID>` 这类占位符。本仓库根目录的 `tools/privacy_scan.py` 可以帮你查一遍。

## 新增渠道

每个渠道一个目录 `skills/ship-<渠道>/`，结构照抄现有渠道：`SKILL.md`（分阶段的步骤，每阶段有完成标准）+ `references/`（按需读取的细节）+ `scripts/`、`templates/`。至少要有一次真实走到「审核中」的经历，才把渠道列进 README 的「已收录」；之前放在「规划中」。
