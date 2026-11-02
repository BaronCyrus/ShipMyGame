# 体检 Unity 工程

目的：在碰后台之前，找出会让构建失败、审核被拒或上线后出事故的问题。逐项检查，结果写进进度档案的「差距清单」。

## 基本信息

| 查什么 | 在哪看 |
|---|---|
| Unity 版本 | `ProjectSettings/ProjectVersion.txt` 的 `m_EditorVersion` |
| 已装的平台模块 | Unity Hub 的编辑器目录下 `PlaybackEngines/`：`AndroidPlayer`（Google Play、TapTap APK）、`WebGLSupport`（微信 / TapTap 小游戏）。缺的模块要装：几个 GB，装到空间充足的磁盘，安装前征得用户同意。 |
| 渲染管线 | `Packages/manifest.json` 里有没有 `com.unity.render-pipelines.universal` |
| 输入系统 | `ProjectSettings/ProjectSettings.asset` 的 `activeInputHandler`（0 旧版，1 新版 Input System，2 两者） |
| 产品名、包名、版本 | `ProjectSettings/ProjectSettings.asset` 的 `productName`、`applicationIdentifier`、`bundleVersion`、`AndroidBundleVersionCode` |
| 场景列表 | `ProjectSettings/EditorBuildSettings.asset` |
| 第三方 SDK | `Packages/manifest.json`、`Assets/Plugins/` |
| 版本控制 | 工程是否在 git 里、有没有未提交的改动。没有 git 时，建议用户先初始化再改，方便回退。 |

## 无头构建

后面所有构建都用命令行跑，不需要用户打开编辑器：

```bash
<本 skill 目录>/scripts/unity_build.sh <工程路径> <构建目标> <类名.方法名> [日志名]
```

脚本会自动找对应版本的编辑器，构建目标用 `Android` 或 `WebGL`。编辑器正开着同一个工程时，命令行构建会失败：请用户先关掉编辑器，或改用编辑器里的菜单。

## 每个渠道都要查的事故点

- **没有声音**：场景里没有 `AudioListener`（用代码生成相机时最容易漏）。两款实测游戏都因此无声上线过 `[实测]`。在每个会启动的场景里确认有且只有一个。
- **中文显示成方块**：WebGL 没有系统字体回退，必须打包中文字体子集（见微信 / TapTap skill）。安卓靠系统字体能显示，但字形不统一。
- **存档丢失**：小游戏里 `Application.persistentDataPath` 不一定持久，关闭小游戏也不一定触发 `OnApplicationPause`；改用平台存储，并在关键操作后立即存。
- **多线程**：WebGL 没有线程，`Task.Run`、`Thread` 会卡死或报错，要改成分帧计算。
- **外链**：小游戏里 `Application.OpenURL` 无效；隐私政策、评分按钮在小游戏版里要换成游戏内页面或去掉。
- **测试广告**：测试阶段的包必须用广告平台的测试广告位，正式上线再换真实 ID，避免无效流量导致封号 `[实测]`。
- **安全区**：刘海屏、小游戏右上角的胶囊按钮会挡住 UI，渠道 skill 里有各自的适配方法。

## 素材与文案

各渠道要的素材不同，规格以渠道 skill 为准。通用做法见 [store-assets.md](store-assets.md)。
