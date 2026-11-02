# 不经过网页的方式：TapTap CLI

TapTap 官方的命令行工具是 `@taptap/cli`（命令 `taptap-cli`）。它能代替网页做的事越来越多，而且上传大文件不受浏览器的 10 MB 限制。本文写的是 2026-11-02 的情况：`@taptap/cli` 2026.11.2，服务端能力目录 catalog 1.0.64。版本更新很频繁，命令以 `taptap-cli --help` 和 `taptap-cli schema <业务域> <动作>` 的输出为准 [官方](https://developer.taptap.cn/v3/cli/command-reference.md)。

## 1. CLI 和网页怎么分工

| 事情 | 用什么 | 依据 |
|---|---|---|
| 上传小游戏包 `game.zip` | **CLI 优先**：`upload-mini-app-package` | `[实测]` 上传成功。官方网页文档写的是「Tap 小游戏上传需前往开发者中心」，和实测相反，见第 4 节 |
| 创建开发版本（等于网页抽屉第 2 步的「提交」） | CLI `package-management submit-mini-app-package` | `[实测]` 记录到的命令；之后的准入测试结果未确认 |
| 查包体列表、版本号、阶段 | CLI `package-management list-mini-app-packages` | `[实测]` |
| **包体准入测试报告和日志** | **只能网页**：小游戏管理 → 包体管理 →「查看报告」 | `[实测]`：CLI 的包体列表里没有准入结果 |
| 自测二维码 | CLI `test-qr-code` 生成 PNG，或网页「自测」 | [官方](https://developer.taptap.cn/v3/cli/skills/taptap-package-management.md) |
| 创建游戏 | CLI `app create-app` 或网页 | [官方](https://developer.taptap.cn/v3/cli/skills/taptap-publish-game.md) |
| 商店资料的文字和图片视频 | CLI `app save-changes`、`upload`、`upload-video`，或网页 | [官方](https://developer.taptap.cn/v3/cli/skills/taptap-app-edit.md)，本仓库未实测；实测是用网页填的 |
| 提审前检查、正式提审 | CLI `app precheck-app-review` → `app submit-app-review`，或网页 | [官方](https://developer.taptap.cn/v3/cli/command-reference.md)，未实测 |
| 隐私保护指引 | 网页（没有找到对应的 CLI 命令） | `[实测]` 网页 |
| 成就、排行榜的创建和发布 | 网页（CLI 文档里没有对应命令） | `[实测]` 网页 |
| 前置备案（证件、签字、核验、提交） | 网页，用户本人 | [官方](https://developer.taptap.cn/v3/cli/skills/taptap-qualification.md)：CLI 的资质命令只处理「非敏感材料草稿」 |

## 2. 安装

```bash
npm i -g @taptap/cli --registry=https://registry.npmjs.org/                                   # 官方命令
npm i -g @taptap/cli --registry=https://registry.npmjs.org/ --prefix <空间充足的目录>          # 系统盘紧张时用，再把 <目录>/bin 加进 PATH
taptap-cli version
taptap-cli auth status        # 没登录时显示「认证：未配置」
```

- 包里带各个平台的二进制，解开约 100 MB；要求 Node 16 及以上 `[实测]`。
- 更新用 `taptap-cli update`。服务端有最低客户端版本，太旧的版本会被要求升级 [官方](https://www.npmjs.com/package/@taptap/cli)。
- CLI 自带给 agent 看的说明：`taptap-cli skills list`、`taptap-cli skills read taptap-package-management`。遇到本文没写的情况，先读这些说明 `[实测]`。
- 官方文档里的 `npx skills add taptap/cli` 要从 GitHub 拉 `taptap/cli` 仓库，这个仓库没有公开，不要用。用上面的 `skills read` 就够了。

## 3. 登录：用户的「一次登录」

CLI 不用在后台生成任何密钥，登录走设备链接授权 `[实测]`：

1. 运行 `taptap-cli auth login --no-wait --json`。返回里有 `verification_url`（形如 `https://accounts.taptap.cn/device?...`）、`device_code`、过期时间（300 秒）。
2. **把链接单独放在你这一轮最后一条消息里**，告诉用户：「5 分钟内在已经登录 TapTap 开发者后台的浏览器里打开，确认授权，然后回来说一声」。实测链接写在消息中间，用户没看到；还有一次放在最后但用户没及时点，过期了。
3. 发完链接**立刻**在后台运行轮询命令（参数原样用第 1 步返回的值）：
   ```bash
   taptap-cli auth login --device-code <device_code> --expires-at-unix <过期时间戳> --interval-seconds 1
   ```
4. 用户授权后轮询结束，`taptap-cli auth status` 显示「认证：Bearer Token」。
5. 链接过期时会报 `token_expired`（「等待授权超时」）。旧的 `device_code` 不能再用，从第 1 步重新生成。

凭据保存在 `~/.taptap-cli/taptap.json`，有效期约 3 个月 `[实测]`。它和密码一样敏感：不打印、不贴进聊天、不进 git。过期后从第 1 步重新登录。

## 4. 上传小游戏包并创建开发版本

**冲突说明**：官方 CLI 网页文档写「Tap 小游戏上传需前往开发者中心」[官方](https://developer.taptap.cn/v3/cli/command-reference.md)，但 2026-11-02 实测 2026.11.2 版能直接上传。**以实测为准**。遇到下面两种情况，就改用网页（见 [console.md](console.md) 第 3.1 节）：
- 命令报「未开放」或者其他错误；
- 已经执行了下面的 `submit-mini-app-package`，网页「包体管理 → 开发版本」里还是没有这个版本。

注意：只上传、还没 submit 时，网页上看不到这个版本，这是正常的。

先拿到 `<开发者ID>`（即厂商 ID）和 `<游戏ID>`：用 `taptap-cli developer +list`、`taptap-cli app +list --dev-id <开发者ID> --kw <关键字>` 查，或者从后台网址 `/<开发者ID>/app/<游戏ID>/` 里取 `[实测]`。

```bash
cd <工程>/Builds/taptap        # 文件必须在当前目录下，目录外的路径会被拒绝
taptap-cli upload-mini-app-package game.zip --app-id <游戏ID> --dev-id <开发者ID> --dry-run
taptap-cli upload-mini-app-package game.zip --app-id <游戏ID> --dev-id <开发者ID> --yes
```

- `[实测]` 上传成功。
- 返回里有产物 ID 和任务 ID，记下来给下一步用。
- 上传成功后**不要再调用** `complete-mini-app-package-upload`。这时状态显示 `artifact_only`，意思是还没建包体记录，不是上传失败 [官方](https://www.npmjs.com/package/@taptap/cli)（随包说明）。

创建开发版本属于高风险写操作，效果等于网页抽屉第 2 步的「提交」，会触发包体准入测试：

```bash
taptap-cli package-management submit-mini-app-package \
  --data '{"artifact_id":<产物ID>,"enable_split":false,"package_version":"1.0.x","task_id":"<任务ID>"}' --dry-run
```

- 把预览给用户看：版本号、是否分包、改了什么。
- 用户明确同意后，用同样的参数把 `--dry-run` 换成 `--yes`。
- 上面是 `[实测]` 记录到的命令；之后的准入测试结果还没确认。
- 报「缺少上下文」（`context_missing`）时，补上 `--dev-id <开发者ID> --app-id <游戏ID>`，或者先运行 `taptap-cli app +select --dev-id <开发者ID> --app-id <游戏ID>`。
- `package_version` 要比上一个提交过的版本高，并且和 `build.json` 的 `version` 一致。
- 你的权限系统拦下 `--yes` 时，把完整命令给用户，请他在自己的终端里运行。

查看版本：

```bash
taptap-cli package-management list-mini-app-packages --app-id <游戏ID> --dev-id <开发者ID> --data '{"page":1,"page_size":20}'
```

能看到版本号、大小、提交时间和阶段，**看不到准入测试结果** `[实测]`。阶段字段实测见过 `dev`（开发版本）；随包说明里还有 `audit`（审核）和 `published`（线上）。准入报告和日志去网页「包体管理」看。

## 5. 自测二维码

依据 [官方](https://developer.taptap.cn/v3/cli/skills/taptap-package-management.md)：

```bash
taptap-cli test-qr-code --app-id <游戏ID> --dev-id <开发者ID> --data '{"package_id":<开发版本的包体ID>}' \
  --output publish/taptap/test-qr.png --json
```

- 只有开发版本（`stage=dev`）有自测二维码。
- 生成后**把 PNG 作为图片发给用户**，不要只发链接。

## 6. 商店资料和素材

依据 [官方](https://developer.taptap.cn/v3/cli/skills/taptap-app-edit.md)，本仓库未实测：

```bash
taptap-cli app analyze-app-status --dev-id <开发者ID> --app-id <游戏ID>     # 列出提审前还缺什么
taptap-cli app get-app-module --dev-id <开发者ID> --app-id <游戏ID> --data '{"module_id":"basic-info"}'
taptap-cli app save-changes --dev-id <开发者ID> --app-id <游戏ID> --data @changes.json --idempotency-key <ASCII 键> --dry-run
taptap-cli upload <图片> --dev-id <开发者ID> --app-id <游戏ID> --idempotency-key <ASCII 键> --yes
taptap-cli upload-video <mp4> --scene gameplay_demo_video --dev-id <开发者ID> --app-id <游戏ID> --yes
```

- 资料共 9 个模块，例如 `basic-info`、`assets-upload`、`platform-status`、`release-settings`、`package`、`other-settings`。
- `save-changes` 每批最多 5 个字段，每个字段都要带读取时的当前值作 `expected`。
- 图片上传只是收进素材库，还要再写进对应字段，才算换了图。
- 把小游戏包绑定为主包体（等于网页「选择包体」）：先 `app list-packages`，再 `app select-package --data '{"slot":"main","package":{"type":"mini_app","id":"<包体ID>"},"expected":<当前值>}'`。
- 每次写入后，回网页对应页签读一遍，确认确实生效了。

## 7. 提审（仍然走提审闸门）

依据 [官方](https://developer.taptap.cn/v3/cli/command-reference.md)，本仓库未实测：

```bash
taptap-cli app prepare-review-snapshot --dev-id <开发者ID> --app-id <游戏ID> --data '{"release_schedule":{"kind":"immediate"}}'
taptap-cli app precheck-app-review --dev-id <开发者ID> --app-id <游戏ID> --data @review-precheck.json   # 只检查，不提交
# 把预检结果（阻塞项、风险、要同意的协议）和 review.md 第 2 节的清单一起给用户看；用户说「提交」后：
taptap-cli app submit-app-review --dev-id <开发者ID> --app-id <游戏ID> --data @review-submit.json --idempotency-key <ASCII 键> --yes
```

- 三步用同一个 `review_fingerprint`（第一步返回的）和同一个 `release_schedule`。
- 预检通过不代表审核一定通过；预检显示「暂时无法检查」，也不代表内容有问题。
- 提交结果不明时，先用 `taptap-cli app list-app-versions` 读版本状态，**不要换一个幂等键重新提交**。
- 2026.11.2 还有一个快捷命令 `audit:submit`，属于不可逆写操作，必须带 `--dry-run` 或 `--yes` `[实测：帮助输出]`。
- 提交后到网页「版本管理」确认状态是「审核中」。

## 8. 通用规则

依据 [官方](https://developer.taptap.cn/v3/cli/command-reference.md)：
- **写操作**：先 `--dry-run` 预览，用户确认后再 `--yes`。两次用同一个 `--idempotency-key`，键只用 ASCII 字母、数字和短横线。
- **退出码**：
  - 0 成功；
  - 1 业务错误；
  - 2 参数错误；
  - 3 认证、授权或配置错误；
  - 4 网络错误；
  - 6 被安全策略拦截；
  - 10 缺少 `--yes`。
- **判断成败**：JSON 输出看 `ok` 字段，不要看 `data` 里的业务码。
- **高风险写操作**（创建开发版本、提交审核、撤审、上线）：每一次都在对话里单独征得用户同意，不要连着批量执行。这也是官方给 agent 的要求 [官方](https://developer.taptap.cn/agents.md)。

## 9. 密钥

- CLI 只有第 3 节那份登录凭据，没有别的密钥。
- 「开放能力」页的 Secret 只在服务端用 code 换 openid 时需要。单机游戏用不到：不要打开，也不要写进工程或进度档案 `[实测]`。真要用时，按 [ship-my-game 的「密钥与隐私」](../../ship-my-game/SKILL.md) 存到 `~/.shipmygame/keys/<游戏标识>/`。
- 官方另有「API 上传 APK」的接口，用应用配置里的 Server Secret 签名，只适用于 APK 路线，和小游戏无关 [官方](https://developer.taptap.cn/docs/sdk/apk-upload/guide/)。

## 10. TapTap MCP

官方的 `@taptap/instant-games-open-mcp` 原文写「当前 MCP 仅支持 H5 小游戏。Unity 及其他引擎的 TapTap 小游戏的 MCP 支持正在开发中」[官方](https://developer.taptap.cn/minigameapidoc/quick-start/mcp-guide/mcp-setup/)。Unity 游戏不要用它上传包体。

## 11. 网页里能读的接口（非公开，只读）

在已登录的后台页面里，用同源 `fetch` 读，用来核对状态 `[实测]`。这些是非公开接口，随时可能变，只读，不写：
- 成就列表：`/api/achievement/v2/list?client_id=<Client ID>&page_size=20&page_num=1`
- 准入测试的性能曲线：`/api/mini-app/v1/automated-testing/performance-line-chart?…&task_id=<任务ID>&device_id=<设备ID>&type=fps`
- 上传后的解析任务：`/api/mini-app/v1/package/upload-miniapp-artifact-parse-task`

工具记录网络请求时，只记第一次调用之后的请求：要排查某个操作的请求，先调用一次读取工具，再做那个操作 `[实测]`。
