# 不经过网页的方式：Google Play Developer API

Google Play Developer API（androidpublisher v3）可以不经过浏览器上传 AAB、写版本说明、改商品详情、管理封闭测试群组、提交数据安全申报，也没有浏览器上传的 10 MB 限制。

**本仓库还没有在真实账号上跑通过 API**：实测上传都是用户把 AAB 拖进网页。下面除标 `[实测]` 的条目外都是 [官方] 依据，读取于 2026-11-02。

## 1. API 和网页怎么分工

| 事情 | 用什么 | 依据 |
|---|---|---|
| 创建应用 | 只能网页 | API 参考里没有对应方法 |
| **首个 AAB** | 网页（用户拖拽）`[实测]`。官方原文：「you will have to upload at least one APK through the Play Console before you can use this API」 | [官方](https://developers.google.com/android-publisher/edits) |
| 之后的 AAB、版本说明、轨道 | API：`scripts/play_upload.py`（预演模式已测，真实账号未测） | [官方](https://developers.google.com/android-publisher/tracks) |
| 商品详情文字、图片 | API `edits.listings`、`edits.images`，或网页 `[实测]` | [官方](https://developers.google.com/android-publisher/api-ref/rest) |
| 封闭测试的测试人员 | API `edits.testers` **只支持 Google 群组**，或网页 `[实测]` | [官方](https://developers.google.com/android-publisher/api-ref/rest/v3/edits.testers) |
| 数据安全 | API `applications.dataSafety`（提交网页导出的 CSV），或网页 `[实测]` | [官方](https://developers.google.com/android-publisher/api-ref/rest/v3/applications/dataSafety) |
| 应用内容的其他声明、内容分级、目标受众、类别、价格、国家/地区（API 只能读） | 只能网页 `[实测]` | API 参考里没有对应方法 |
| 被拒后的「送审」、自管式发布的「发布更改」、申请正式版 | 只能网页 | [官方](https://support.google.com/googleplay/android-developer/answer/9859654?hl=zh-Hans) |
| 查审核状态 | API `applications.tracks.releases.list`，或网页「发布概览」 | [官方](https://developers.google.com/android-publisher/api-ref/rest/v3/applications.tracks.releases) |
| PGS 成就、排行榜 | 网页 `[实测]`（另有 PGS Publishing API，本仓库不用） | — |

**Google 对 API 用法的限制**（[官方](https://developers.google.com/android-publisher/api_usage)）：
- 测试版更新每天不超过一次。
- 不要把服务账号私钥交给第三方。
- **不要用这个 API 给别人提供代发布服务**。本仓库只在用户自己的电脑上、用用户自己的服务账号运行。

## 2. 服务账号：一次性设置

[官方](https://developers.google.com/android-publisher/getting_started)。官方原文写明现在不再需要把开发者账号关联到 Cloud 项目。

1. **你**：在 Google Cloud 控制台新建或选择一个项目 → 在 API 库里启用「Google Play Android Developer API」。可以和 PGS 共用同一个 Cloud 项目。
2. **你**：「IAM 和管理 → 服务账号 → 创建服务账号」，只填名称。授权项目访问权限之类的可选步骤跳过。
3. **你**：进入该服务账号 →「密钥」→「添加密钥 → 创建新密钥 → JSON → 创建」。
   - 下载的文件只能下载这一次，移到 `~/.shipmygame/keys/<包名>/play-api.json`，`chmod 600`，不进 git，不贴进聊天 [官方](https://cloud.google.com/iam/docs/keys-create-delete)。
   - 2024-05-03 及之后创建的 Google Cloud **组织**默认禁止创建服务账号密钥。个人 Google 账号建的项目一般不受影响。被禁止时，请有权限的人豁免这个项目，或者改用 fastlane 支持的 Workload Identity Federation。
4. **你**：Play 管理中心左侧主菜单「用户和权限 →『邀请新用户』」，填服务账号邮箱（`<服务账号名>@<项目ID>.iam.gserviceaccount.com`）：
   - 在「应用权限」里只给这个应用授权；
   - 权限按下表勾选，**不要给「管理员（所有权限）」**；
   - 点「邀请用户」。服务账号不需要接受邀请。
5. 权限生效最长要 48 小时 [官方](https://developers.google.com/android-publisher/api-ref/rest/v3/users)。刚邀请就报 403 时先等。
6. 旧的「API 访问权限」页面：Developer API 文档说不再需要它，PGS Publishing API 文档仍提到它，是否已对所有账号移除**未确认**。上传 AAB 不依赖这个页面。

| 要做的事 | 权限（帮助中心中文原文） |
|---|---|
| 上传 AAB、在测试轨道建版本 | 将应用发布到测试轨道 |
| 改测试人员 | 管理测试轨道和修改测试人员列表 |
| 发布到正式版 | 发布为正式版，排除设备以及使用 Play 应用签名功能 |
| 商品详情文字和图片 | 管理商店发布 |
| 数据安全（API） | 管理政策声明（推断：帮助页说它覆盖数据安全部分） |

依据：[官方](https://support.google.com/googleplay/android-developer/answer/9844686?hl=zh-Hans)。

## 3. 上传 AAB：`scripts/play_upload.py`

```bash
pip install google-api-python-client google-auth
python3 scripts/play_upload.py --key ~/.shipmygame/keys/<包名>/play-api.json --package <包名> \
  --aab Builds/<产品名>-1.0.2-3.aab --track <轨道名> \
  --notes en-US=publish/google-play/notes/en-US.txt --notes zh-CN=publish/google-play/notes/zh-CN.txt \
  --dry-run                       # 先预演：只打印参数，不连 Google
```

- 预演没问题后去掉 `--dry-run`。默认 `--status draft` 只建草稿版本，而且**不送审**（commit 时带 `changesNotSentForReview=true`）。去「发布概览」核对后，按提审闸门送审。
- `--status completed --send-for-review`：只在用户说了「提交」之后用。
- 每次 commit 都带 `changesInReviewBehavior=ERROR_IF_IN_REVIEW`。不带的话，默认行为会取消正在审核的变更，再把全部变更重新送审 [官方](https://developers.google.com/android-publisher/api-ref/rest/v3/edits/commit)。所以已有改动在审核时会直接报错，等那次审核结束再提交。
- 版本说明每种语言 ≤500 字符，脚本会检查。

**已知限制和注意事项**（脚本没处理的，按下面做）：

1. **轨道名**：官方文档的表格里，内部测试是 `qa`、开放测试是 `beta`、正式版是 `production`，封闭测试用自定义名称（默认那条历史上叫 `alpha`）[官方](https://developers.google.com/android-publisher/tracks)。脚本默认的 `internal` 来自第三方工具的叫法，**是否和 `qa` 等价未确认**。实测游戏用的是默认封闭测试轨道。上传前先用网页（轨道页网址里的轨道 ID 和名称）或 API `edits.tracks.list` 确认实际的轨道名。
2. **草稿应用**（从没在任何轨道发布过，或首次发布被拒）：第三方工具文档里记录的报错是 `Only releases with status draft may be created on draft app`（官方文档没有明文写）。所以新应用第一次一律用 `draft`，在网页上完成第一次发布 `[实测：首次发布走的网页]`。
3. **`changesNotSentForReview` 的两种报错**（fastlane 源码 `supply/lib/supply/client.rb` 处理了这两条 API 报错文本）：
   - 报 `Please set the query parameter changesNotSentForReview to true`：应用上次被拒，需要带这个参数，再到网页手动「送审」。
   - 报 `The query parameter changesNotSentForReview must not be set`：这个应用的改动会自动送审，不允许带这个参数。这时**停下来**：不带参数重试就等于送审，必须先走提审闸门，用户同意后再用 `--send-for-review` 重跑。
   - 脚本的默认行为在第二种情况下会失败。这是预期的保护，不要自动重试。
4. **超时**：官方建议上传 AAB 时把 HTTP 超时调到 2 分钟以上 [官方](https://developers.google.com/android-publisher/api-ref/rest/v3/edits.bundles/upload)。上传几十 MB 超时的话，在网络稳定时重试，或者请用户拖拽。
5. **edit 会失效**：每个用户同时只能有一个打开的 edit；有人在网页上改了这个应用，所有打开的 edit 都会作废 [官方](https://developers.google.com/android-publisher/edits)。脚本运行期间不要在网页上改同一个应用。
6. **生效时间**：commit 成功后「可能要几个小时」才生效；在「发布概览」和轨道页确认。

## 4. 其他常用 API（同一个 edit 里做，最后 commit）

都用 `googleapiclient` 的 `service.edits()` 调用，参数名和 REST 参考一致 [官方](https://developers.google.com/android-publisher/api-ref/rest)：

| 目的 | 方法 | 要点 |
|---|---|---|
| 列出轨道和 release 状态 | `edits.tracks.list` | 只读，用完 `edits.delete` 掉这个 edit |
| 商品详情文字 | `edits.listings.update(language=…, body={language,title,shortDescription,fullDescription})` | 长度限制同网页 |
| 截图、图标、置顶大图 | `edits.images.deleteall` 然后 `edits.images.upload(imageType=phoneScreenshots/icon/featureGraphic…)` | 按顺序逐张上传。2026-11 的发现文档里多了一个可选参数 `aiGeneratedState`，用来声明图片是否 AI 生成，参考页还没写 |
| 封闭测试的群组 | `edits.testers.update(track=…, body={googleGroups:[…]})` | 不支持邮箱名单 |
| 新建封闭测试轨道 | `edits.tracks.create(body={track, type: CLOSED_TESTING, formFactor: DEFAULT})` | |
| 数据安全 | `applications.dataSafety(packageName=…, body={safetyLabels: <CSV 全文>})` | 不在 edit 里；CSV 从网页「数据安全」右上角导出后修改 |
| 审核状态 | `applications.tracks.releases.list(parent=applications/<包名>/tracks/<轨道>)` | 返回 `RELEASE_LIFECYCLE_STATE_IN_REVIEW / NOT_APPROVED / PUBLISHED…`，适合轮询 |
| 内部应用分享 | `internalappsharingartifacts.uploadbundle` | 不经审核，返回下载链接；Play 会用内部测试证书重新签名 |

配额：发布类 API 默认每分钟 3000 次，够用 [官方](https://developers.google.com/android-publisher/quotas)。

## 5. 现成工具

- **fastlane supply**（2.241.2，2026-10 仍在活跃发布）：
  - `fastlane supply --aab <x.aab> --track <轨道> --release_status draft`；
  - 有 `changes_not_sent_for_review` 和 `rescue_changes_not_sent_for_review` 选项，见第 3 节第 3 条；
  - `fastlane run validate_play_store_json_key json_key:<路径>` 可以测连通性 [官方](https://docs.fastlane.tools/actions/upload_to_play_store/)。
  - 需要 Ruby。
- **gradle-play-publisher**（4.1.1，2026-08）：Gradle 插件，Unity 要先导出 Gradle 工程才能用，不推荐。

## 6. 密钥放哪里

全部放 `~/.shipmygame/keys/<包名>/`，目录 700、文件 600，不进 git，也不贴进聊天（规则见 [ship-my-game](../../ship-my-game/SKILL.md)「密钥与隐私」）：

| 文件 | 内容 | 谁生成 |
|---|---|---|
| `upload.keystore`、`keystore.env` | 上传密钥和口令 | `scripts/make_upload_keystore.sh` `[实测]` |
| `play-api.json` | 服务账号 JSON 密钥 | 第 2 节 |

提醒用户把整个目录备份到密码管理器或加密盘。服务账号不用了，就在 Cloud 里删掉它的密钥，再在 Play「用户和权限」里移除它。两边是分开管理的，只在 Play 里移除不会删掉 Cloud 里的密钥（推断）。
