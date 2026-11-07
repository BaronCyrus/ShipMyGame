# 不经过界面的方式：命令行、API、页面脚本

## 选哪条路

| 方式 | 能做什么 | 前提 | 实测情况 |
|---|---|---|---|
| miniprogram-ci（本仓库 `scripts/wx_ci.js`） | 预览出二维码、上传到「开发版本」 | 后台生成「小程序代码上传密钥」，并配置 IP 白名单 | `[官方]`，脚本**未实测** |
| 开发者工具 CLI / HTTP | 登录、预览、自动预览、上传 | 用户在开发者工具「设置 → 安全设置」里打开服务端口；工具已登录 | `[实测-失败]`：服务端口默认关闭，实测没开 |
| computer use 点开发者工具界面 | 导入、编译、预览、截图里的二维码 | 有屏幕控制权限 | `[实测]`，本仓库的主路径 |
| 浏览器里执行 JS 操作 MP 网页 | 读 shadow DOM 正文、填表、传备案截图 | 用户的 Chrome（见总入口 [browser.md](../../ship-my-game/references/browser.md)） | `[实测]` |

两点固定规则：
- **体验版、提交审核、发布都没有可用的接口**，只能在 MP 网页上做，见文末「不能自动化的环节」。
- 上传前想先验证包，用预览二维码就够了，不需要上传。

## 上传密钥和 IP 白名单

**位置**（已在后台核对）
- 「管理 → 开发管理 → 开发设置 → 小程序代码上传」。
- 页面说明原文：「开发者可基于配置信息调用微信开发者工具提供的代码上传模块」。
- 有两项：「小程序代码上传密钥」（按钮「生成」）和「IP白名单」（按钮「编辑」）。

**官方说明** `[官方]`（[miniprogram-ci](https://developers.weixin.qq.com/miniprogram/dev/devtools/ci.html)）
- 原文：「代码上传密钥拥有预览、上传代码的权限，密钥不会明文存储在微信公众平台上，一旦遗失必须重置」。
- 关于白名单，原文：「我们建议所有开发者默认开启这个选项」。

**步骤**
1. **用户**：点「生成」，一般要管理员扫码确认。这是本人事项。浏览器会下载一个私钥文件 `private.<AppID>.key`。
2. **你**：把私钥文件移到 `~/.shipmygame/keys/<游戏标识>/`，然后 `chmod 600`。不进 git，不贴进聊天，进度档案里只写它的位置。
3. **你**：查出运行 CI 的机器的出口公网 IP，请用户在「IP白名单 → 编辑」里加上。
   - 家庭宽带的出口 IP 会变，变了就要更新白名单。
   - 云端 CI（例如 GitHub 托管的 runner）的出口 IP 不固定，要改用固定出口的机器（推断）。
4. 密钥丢了或者泄露了：请用户在同一位置重置，旧密钥会失效。

## 用 `scripts/wx_ci.js` 预览、上传（未实测）

脚本按官方文档和 miniprogram-ci 2.1.31 的类型定义编写，还没有在真实账号上跑过。

```bash
# 装一次，装到工具目录，不装进游戏工程。需要 Node 16.1 以上
npm i --prefix ~/.shipmygame/tools miniprogram-ci@2.1.31

# 预览：生成二维码图片，用户用手机扫
node <本 skill 目录>/scripts/wx_ci.js preview <工程>/Builds/wechat/minigame \
  --appid <AppID> --key ~/.shipmygame/keys/<游戏标识>/private.<AppID>.key --qr publish/wechat/preview.png

# 上传到「版本管理 → 开发版本」
node <本 skill 目录>/scripts/wx_ci.js upload <工程>/Builds/wechat/minigame \
  --appid <AppID> --key ~/.shipmygame/keys/<游戏标识>/private.<AppID>.key --version 1.0.0 --desc "首个版本"
```

参数说明：
- `--robot <1～30>`：选 CI 机器人编号，默认 1。
- `--setting '<JSON>'`：覆盖编译设置。默认是 `{"useProjectConfig": true}`，即沿用 Unity 导出的 `project.config.json`。这个字段官方文档里没写，是包里的类型定义里有的。
- 脚本最后会打印各分包的大小（`subPackageInfo`）：`__FULL__` 是整包，`__APP__` 是主包。

这些设置的依据（官方，部分来自包源码）：
- 项目类型必须是 `miniGame`。CLI 的默认值是 `miniProgram`，官方 CLI 示例里没有写这个参数，所以直接用 CLI 时要加 `--pt miniGame`。依据是 miniprogram-ci 2.1.31 包内的 `dist/cli/index.js`。
- 上传只会进「开发版本」。之后设体验版、提审、发布都在网页上做。

直接用 miniprogram-ci 自带的 CLI 也可以：

```bash
~/.shipmygame/tools/node_modules/.bin/miniprogram-ci upload --pt miniGame --pp <minigame 目录> --pkp <私钥路径> \
  --appid <AppID> --uv 1.0.0 --ud "首个版本" -r 1 --use-project-config
```

## 开发者工具 CLI / HTTP `[官方]`

来源：[命令行调用](https://developers.weixin.qq.com/miniprogram/dev/devtools/cli.html)、[HTTP 调用](https://developers.weixin.qq.com/miniprogram/dev/devtools/http.html)。

**前提**
- 用户在开发者工具「设置 → 安全设置」里打开服务端口。这是安全设置，由用户决定。
- 实测没开时，执行 `cli islogin` 的报错原文：「工具的服务端口已关闭。要使用命令行调用工具，请手动打开工具 -> 设置 -> 安全设置，将服务端口开启。」 `[实测-失败]`

**CLI 位置**
- macOS：`<安装路径>/Contents/MacOS/cli`。用 Homebrew 装在默认位置时，就是 `/Applications/wechatwebdevtools.app/Contents/MacOS/cli`。
- Windows：`<安装路径>/cli.bat`。

**常用命令**

```bash
cli islogin
cli login -f image -o publish/wechat/login-qr.png               # 用户扫码登录
cli open --project <minigame 绝对路径>
cli preview --project <minigame 绝对路径> --qr-format image --qr-output publish/wechat/preview.png --info-output publish/wechat/preview.json
cli upload --project <minigame 绝对路径> -v 1.0.0 -d "首个版本" -i publish/wechat/upload.json
cli cache --clean compile --project <minigame 绝对路径>
cli quit
```

- 返回码 0 表示成功，-1 表示失败。
- `--info-output` 输出的 JSON 里有代码包和分包的大小。
- 上传用的是当前扫码登录的开发者身份。
- HTTP 方式：端口号记在 `~/Library/Application Support/微信开发者工具/<MD5>/Default/.ide` 文件里。例如 `GET http://127.0.0.1:<端口>/v2/upload?project=<路径>&version=<版本号>&desc=<备注>`。

## 开发者工具 Skills / MCP（参考）`[官方]`

来源：[开发者工具 Skills](https://developers.weixin.qq.com/miniprogram/dev/devtools/Skills.html)。

- 官方 2026 年为编程 agent 推出了 `wechatide` 命令和 MCP，能力包括打开项目、编译、模拟器自动化、读日志、预览、「上传体验版」。
- 局限：
  - 要求 Nightly 版开发者工具（2.02.2607032 及以上），而 Unity 文档要求 Stable 版；
  - 文档里的示例全是小程序，**对小游戏能不能用未确认**。
- 本仓库没有用它，主路径仍然是 Stable 版加上本页的其他方式。

## 体验成员

不进网页、批量加体验成员的方式 `[官方]`（[miniprogram-mp-ci](https://developers.weixin.qq.com/miniprogram/dev/devtools/miniprogram-mp-ci.html)）：
- 用 miniprogram-mp-ci，它和 miniprogram-ci 用同一把上传密钥。
- 调用 `ci.manageProjectMember({ project, action: 'add_experiencer', robot: 1, member_list: [{ wechatid: '<微信号>' }] })`。
- 原文：「管理员微信会收到“公众平台安全助手“下发的消息通知，管理员确认后立即生效」。
- 本仓库**未实测**。

## 在 MP 网页里用脚本操作 `[实测]`

- 拼带 token 的网址跳转、读 shadow DOM、点保存：见 [console.md](console.md#读后台的技巧)。
- 改文本框、上传备案截图：见 [filing.md](filing.md#小游戏备案) 和总入口的 [uploads.md](../../ship-my-game/references/uploads.md)（本机中转）。
- 读 AppID：在「开发设置」页执行 `document.body.innerText.match(/wx[0-9a-f]{16}/g)`。

## 不能自动化的环节

| 环节 | 原因 | 依据 |
|---|---|---|
| 设为体验版 | miniprogram-ci 没有这个功能 | [ci 文档](https://developers.weixin.qq.com/miniprogram/dev/devtools/ci.html)列出的能力只有上传、预览、构建 npm、云开发、代理、sourceMap |
| 提交审核 | 官方提审接口 `wxa/submit_audit`，原文：「本接口支持「第三方平台」账号类型代调用……其他账号类型如无特殊说明，均不可调用」。它的错误码表里还有 `87006 this is game miniprogram submitaudit is forbidden`（小游戏不能提交） | [提交代码审核](https://developers.weixin.qq.com/doc/oplatform/openApi/miniprogram-management/code-management/api_submitaudit.html) |
| 发布 | 同样是网页操作，还要管理员微信扫码确认；联网游戏要先拿到小程序备案号（单机实测不用等） | [版本审核](https://developers.weixin.qq.com/minigame/introduction/guide/bbsh.html) |
| 备案的签署、证件、人脸、短信核验 | 本人事项 | [filing.md](filing.md) |

这些环节按 SKILL.md 的阶段，把页面停在对应按钮上，然后走提审闸门。
