# 工程改造与构建：签名 AAB

命令里的 `scripts/…` 以本 skill 目录为起点，`../ship-my-game/…` 是总入口的共享脚本。

## 1. 工程设置

`scripts/ShipAndroidBuild.cs` 会在构建时自动设置下表里标「脚本」的项。其余项由你改工程，或者确认用户已经改好。

| 项 | 取值 | 谁来设 | 依据 |
|---|---|---|---|
| Unity 版本 | 目标 API 36 需要 6000.0.46f1+ 或 2022.3.62f1+（6000.2、6000.3 都支持 35 和 36）；16 KB 页需要 6000.0.38f1+ 或 2022.3.56f1+。本仓库实测用 Unity 6000.6.5f1 | 用户 | [官方 6000.0](https://docs.unity3d.com/6000.0/Documentation/Manual/android-requirements-and-compatibility.html)、[官方 2022.3](https://docs.unity3d.com/2022.3/Documentation/Manual/android-requirements-and-compatibility.html)、[Unity 公告](https://discussions.unity.com/t/android-unity-engine-support-for-16-kb-memory-page-sizes-android-15/1589588)；`[实测]` |
| 平台模块 | Android Build Support（含 OpenJDK、Android SDK & NDK） | 用户（几个 GB，安装前征得同意） | — |
| 包名 | `PlayerSettings` 的 applicationIdentifier。**第一次上传后就不能再改**，也不能删除或复用 | 你 | [官方](https://support.google.com/googleplay/android-developer/answer/9859152) |
| 脚本后端 | IL2CPP | 脚本 | [官方 64 位](https://developer.android.com/google/play/requirements/64-bit)；`[实测]` |
| 架构 | ARM64 + ARMv7 | 脚本 | `[实测]`：版本表显示「ABI 2」 |
| targetSdk | **36**。下限是 2026-08-31 起的 Play 要求，申请延期的截止日 2026-11-01 已过 | 脚本（见下方注意） | [官方](https://support.google.com/googleplay/android-developer/answer/11926878)；`[实测]` 用 36 没有任何警告 |
| minSdk | 不低于 24（AdMob 插件 11.6、PGS 插件 2.2+ 的要求）；实测用 26，上传后显示「API 级别：26 及更高级别」 | 你 | [AdMob 插件](https://github.com/googleads/googleads-mobile-unity/releases)、[PGS 插件](https://github.com/playgameservices/play-games-plugin-for-unity/releases)；`[实测]` |
| 输出格式 | AAB（新应用必须用 AAB） | 脚本（`Aab` 入口） | [官方](https://support.google.com/googleplay/android-developer/answer/9859348) |
| 托管代码裁剪 | release 用 Medium | 脚本 | `[实测]` |
| 调试符号 | release 把符号表打进 AAB，消除 Play 的「尚未上传调试符号文件」提醒 | 脚本 | `[实测]` |
| AudioListener | 每个会启动的场景里有且只有一个 | 你（脚本只警告） | `[实测]`：两款游戏都因为缺它无声上线过 |
| 启动画面 | 可以用 `PlayerSettings.SplashScreen.show = false` 关掉（是否可关取决于用户的 Unity 许可） | 你 | `[实测]` |
| 自适应图标 | `PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive)`，再 `SetTextures(背景, 前景)`。Unity 6.6 里 `Round`、`Legacy` 两种图标类型已过时（报 CS0619），删掉 | 你 | `[实测]` |
| 照片、视频、通讯录等敏感权限 | 合并后的清单里不要有 `READ_MEDIA_IMAGES`、`READ_MEDIA_VIDEO` 等权限，除非是核心功能（需要在后台声明） | 你 | [官方](https://support.google.com/googleplay/android-developer/answer/14115180) |

**注意**：`ShipAndroidBuild.cs` 只在 targetSdk 已设成某个具体值、并且低于 36 时才把它改成 36。Player Settings 里是「Automatic (highest installed)」（值为 0）时不改，实际 target 取本机已装的最高 SDK 平台。所以每次构建后都用第 4 节的 bundletool 命令确认 `targetSdkVersion` 是 36。

## 2. 上传密钥（一次，每个游戏一把）

```bash
bash scripts/make_upload_keystore.sh <包名>        # 可选第二个参数 "CN=<工作室名>"，不要写法定姓名
```

- 生成位置：`~/.shipmygame/keys/<包名>/upload.keystore` 和 `keystore.env`（随机 32 位口令，权限 600）。已存在时拒绝覆盖。
- 会自动找 Unity Android 模块自带的 keytool，因为 macOS 的 `/usr/bin/keytool` 是空壳。
- 输出里有上传密钥的 SHA-1。PGS 建凭据时要用，不要写进进度档案和聊天里，需要时再读。
- `[实测]`：脚本已测；同样做法的上传密钥在两款游戏上签过 AAB。
- 生成后立刻提醒用户把 `~/.shipmygame/keys/<包名>/` 备份到密码管理器或加密盘。丢了只能向 Google 申请重置上传密钥 [官方](https://support.google.com/googleplay/android-developer/answer/9842756)。

Play 应用签名：新应用默认由 Google 生成并保管应用签名密钥，创建应用时会接受 Play 应用签名服务条款。新应用还会默认启用「量子就绪型混合签名」，PGS 等服务要登记它的指纹，见 [capabilities.md](capabilities.md) 第 1.3 节 [官方](https://support.google.com/googleplay/android-developer/answer/9842756)。

## 3. 构建命令

把 `scripts/ShipAndroidBuild.cs` 复制到工程的 `Assets/Editor/`，然后（请用户先关掉打开这个工程的 Unity 编辑器）：

```bash
# 发布用 AAB：签名用 ~/.shipmygame/keys/<包名>/keystore.env
../ship-my-game/scripts/unity_build.sh <工程目录> Android ShipAndroidBuild.Aab release-1.0.1 \
  -shipVersion 1.0.1 -shipVersionCode 2

# 调试 APK：装到模拟器或手机上冒烟
../ship-my-game/scripts/unity_build.sh <工程目录> Android ShipAndroidBuild.Apk dev

# 签名的 release APK：本地真机测试用
../ship-my-game/scripts/unity_build.sh <工程目录> Android ShipAndroidBuild.ReleaseApk release-apk -shipVersion 1.0.1 -shipVersionCode 2
```

- 可选参数：`-shipOut <输出文件>`；`-shipKeys <keystore.env 路径>`（默认 `~/.shipmygame/keys/<包名>/keystore.env`）。
- 默认输出：`Builds/<产品名>-<版本>-<版本代码>.aab`。
- 日志里有 `[Ship] result=Succeeded out=… size=… version=… targetSdk=…`。
- `[Ship] WARNING: … AudioListener` 表示某个场景的 AudioListener 不是正好 1 个，必须处理，见 [troubleshooting.md](troubleshooting.md)。
- `[实测]`：本脚本在空工程上构建出过调试 APK；同样配置的构建脚本在两款游戏上产出过签名 AAB。耗时（Apple Silicon）：首次 release AAB 约 6.6 分钟，增量约 3.2 分钟，调试 APK 2～3 分钟。

## 4. 构建后自检（你做，全自动）`[实测]`

工具都在 Unity 的 Android 模块里，下面用 `<A>` 代表 `<编辑器目录>/PlaybackEngines/AndroidPlayer`：
- bundletool：`<A>/Tools/bundletool-all-<版本>.jar`
- java：`<A>/OpenJDK/bin/java`
- zipalign：`<A>/SDK/build-tools/<版本>/zipalign`

```bash
# 1. 清单：包名、versionCode/versionName、minSdk/targetSdk、权限、AdMob App ID（测试版里应是测试 ID）
"<A>/OpenJDK/bin/java" -jar "<A>/Tools/bundletool-all-<版本>.jar" dump manifest --bundle <x>.aab

# 2. 符号表已打进 AAB（应看到 BUNDLE-METADATA/com.android.tools.build.debugsymbols/...）
unzip -l <x>.aab | grep BUNDLE-METADATA

# 3. 转出通用 APK，装到模拟器冒烟（重点看 IL2CPP 裁剪有没有误删代码、广告能否加载）
"<A>/OpenJDK/bin/java" -jar "<A>/Tools/bundletool-all-<版本>.jar" build-apks --mode=universal \
  --bundle <x>.aab --output <x>.apks --ks <keystore> --ks-key-alias upload --ks-pass file:<口令文件>
```

`--ks-pass` 用 `file:` 读口令，避免口令出现在命令行和日志里。口令就是 `keystore.env` 里 `KEYSTORE_PASS` 的值，临时写进一个权限 600 的文件，用完删掉。

**16 KB 页大小检查** [官方](https://developer.android.com/guide/practices/page-sizes)：

```bash
"<A>/OpenJDK/bin/java" -jar "<A>/Tools/bundletool-all-<版本>.jar" dump config --bundle <x>.aab | grep -i alignment   # 应含 PAGE_ALIGNMENT_16K
unzip -o <x>.apks -d apks_out && "<A>/SDK/build-tools/<版本>/zipalign" -c -P 16 -v 4 apks_out/universal.apk          # 最后一行应是 Verification successful
```

- 还可以用 `llvm-objdump -p <lib>.so | grep LOAD` 逐个看 `.so`：`align` 不能小于 `2**14`。
- 不合规的通常是旧的第三方原生插件，或 1.8.21 以下的 Burst（Unity 公告）。Play 管理中心的「App bundle 资源管理器」（「测试和发布 → 最新版本和 app bundle → 所有应用版本」里）也会显示合规情况。
- 官方几处日期说法不一致，一律按「上传前必须合规」处理：
  - 2025-05 的官方博客写的是 2025-11-01 起，以 Android 15+ 为目标的新应用和更新必须支持 16 KB [官方](https://android-developers.googleblog.com/2025/05/prepare-play-apps-for-devices-with-16kb-page-size.html)；
  - page-sizes 页（2026-10-16 更新）写的是 2027-02-01 起不支持 16 KB 的更新不能发布；
  - Play 帮助中心「技术质量要求」把它列为现行要求：含原生代码的应用必须支持 [官方](https://support.google.com/googleplay/android-developer/answer/17492799)。

完成标准：
- `dump manifest` 里 targetSdkVersion = 36，versionCode 比上一次上传的大，AdMob App ID 符合当前阶段（测试期用测试 ID）。
- 16 KB 两项检查都通过。
- 调试 APK 在模拟器或真机上能启动；声音由用户在真机外放确认（模拟器验证不了声音，见 troubleshooting）。

## 5. 插件相关的构建步骤

- **AdMob App ID**：在 `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset` 里改 `adMobAndroidAppId` 一行，也可以用菜单 **Assets > Google Mobile Ads > Settings** 改。
  - 测试期用 Google 的示例 App ID，见 [capabilities.md](capabilities.md) 第 2.3 节；正式版再换成自己的。
  - 漏填会一启动就崩溃，报 `Missing application ID` [官方](https://developers.google.com/admob/android/quick-start)。
  - `[实测]` 做法：构建脚本里用反射调 `GoogleMobileAds.Editor.GoogleMobileAdsSettings.LoadInstance()`，再用 `SerializedObject` 改这个字段。
- **PGS 资源**：把「获取资源」里的 XML 存成 `publish/google-play/games-ids.xml`，复制 `scripts/ShipPgsSetup.cs` 到 `Assets/Editor/`，然后运行：
  ```bash
  ../ship-my-game/scripts/unity_build.sh <工程目录> Android ShipPgsSetup.Run pgs
  ```
  - 会生成 `Assets/Scripts/GPGSIds.cs`（每个成就、排行榜的 ID 常量）、带 APP_ID 的清单库和 `PlayGamesSettings.asset`。
  - 每次加了成就或排行榜都要重跑。
  - `[实测]`：同一套调用在两款游戏上用过。本脚本已编译验证，没装插件时也能编译。
  - 游戏代码已经引用了 `GPGSIds`、但它还没生成时，工程编译不过。先放一个只含占位常量的 `Assets/Scripts/GPGSIds.cs`，运行 ShipPgsSetup 后会被覆盖 `[实测]`。
- **插件导入**：PGS 插件的 `.unitypackage` 自带 EDM4U 1.2.182。工程里已经有更新的 EDM4U 时，导入时跳过它和包里嵌套的旧 unitypackage `[实测]`。AdMob 插件 11.6.0 起可以改用 Unity 官方的 `com.unity.external-dependency-manager` [官方](https://github.com/googleads/googleads-mobile-unity/releases)。同一个工程只保留一套 EDM，详见 capabilities.md。

## 6. 版本号

- 每上传一个 AAB，versionCode 就加 1；versionName 按语义递增，例如 1、2、3 对应 1.0.0、1.0.1、1.0.2 `[实测]`。
- versionCode 上限 2,100,000,000 [官方](https://support.google.com/googleplay/android-developer/answer/9859152)。
- 发布版本名称会自动填成「versionCode (versionName)」，例如「2 (1.0.1)」，上限 50 字符 `[实测]`。
- 每次上传的版本号都记进进度档案。

## 7. 体积

- 实测 AAB 59～88 MB；把符号打进去会让 AAB 变大，但不影响用户下载。预览页「版本分发」显示新安装约 40 MB `[实测]`。
- 官方上限按压缩后的下载大小算：base 模块 500 MB；超过 200 MB 时，用移动数据安装的用户会看到提示；更大的资源用 Play Asset Delivery [官方](https://support.google.com/googleplay/android-developer/answer/9859372)。
- AAB 远超浏览器工具单次上传的 10 MB 上限：第一次上传请用户拖拽，以后可以走 API，见 [automation.md](automation.md)。

## 8. 截图和录屏

用专门的「截图构建」和 adb 截图、录屏，做法见 [store-assets.md](../../ship-my-game/references/store-assets.md)。Google Play 的规格见 [listing.md](listing.md)。
