# Windows 构建：在 Mac 上给 Steam 打包

## 1. 为什么 Mac 上只能用 Mono

- Unity 文档：IL2CPP 一般不支持交叉编译，要打哪个平台的 IL2CPP 包，就得用跑在那个平台上的 Editor；唯一的例外是 Linux [官方](https://docs.unity.com/en-us/engine/6000.3/manual/programming-environment/scripting-backends/il2cpp/il2cpp-introduction)。
- `[实测]`：Mac 版 Unity Hub 给 2022.3、6000.3、6000.6 提供的 Windows 模块都只有「Windows Build Support (Mono)」，没有 IL2CPP 版。
- Unity 云构建（Build Automation）的平台表里，Windows IL2CPP 只能用 Windows 构建机 [官方](https://docs.unity.com/en-us/build-automation/reference/supported-platforms-on-each-builder-os)。
- Steam 本身不要求 IL2CPP，用 Mono 打的 Windows 包可以正常发行。确实需要 IL2CPP 时，要在装了 Visual Studio C++ 工具和 Windows SDK 的 Windows 电脑上打 [官方](https://docs.unity.com/en-us/engine/6000.5/manual/platform-specific/windows/requirements-and-compatibility)，或者用云端的 Windows 构建机。

## 2. 装 Windows Build Support (Mono) `[实测：6000.6.5f1，Apple 芯片的 Mac]`

用 Unity CLI（`unity`）装，不用打开 Hub：

```bash
unity install-modules --editor-version <版本> --architecture arm64 --module windows-mono --yes
unity editors verify <版本> --architecture arm64
```

- 下载约 380 MB，装好约 1 GB，装在 Editor 目录的 `PlaybackEngines/WindowsStandaloneSupport` 下。
- `verify` 的输出里有 `windows-mono ok` 就是装好了。

## 3. 打包 `[实测：空工程]`

1. 把 `scripts/ShipSteamBuild.cs` 复制到游戏的 `Assets/Editor/`。
2. 确认 Build Settings（或 Build Profile）的场景列表里列着游戏的场景，并且勾上了。
3. 关掉这个工程的 Editor，然后构建：

   ```bash
   ../ship-my-game/scripts/unity_build.sh <工程目录> Win64 ShipSteamBuild.Win64 steam-win64 -shipVersion 1.0.0
   ```

   - 默认输出到工程里的 `Builds/Steam/Windows/<产品名>.exe`，用 `-shipOut <路径>` 改。
   - 默认用 Mono。在 Windows 电脑上要打 IL2CPP 时加 `-shipIl2cpp`；在 Mac 上加这个参数会直接报错。
4. 自检：
   - 日志摘要里有 `[ShipSteam] Succeeded`，后面写着包体大小和脚本后端；
   - `file Builds/Steam/Windows/<产品名>.exe` 显示 `PE32+ executable (GUI) x86-64`；
   - 同一目录下有 `<产品名>_Data`、`MonoBleedingEdge`、`UnityPlayer.dll`。

`[实测]`：在 6000.6.5f1 的空 3D 工程上，打出的 Mono 包约 94 MB。整个目录是一个整体，只拷 exe 是跑不起来的。

`[实测-失败 → 已修复]`：场景列表为空时，`unity build --target StandaloneWindows64` 用 Unity 默认的构建方式会因为场景路径为空而失败。`ShipSteamBuild` 遇到空列表会直接报错，提示先把场景加进去。

## 4. Steam 版要改的地方

- **去掉广告**：Steam 不接受以广告为商业模式的应用 [官方](https://partner.steamgames.com/doc/gettingstarted/onboarding)。用宏或者单独的 Steam 构建配置把广告 SDK 和广告入口去掉。
- **PC 操作**：键盘、鼠标、窗口大小和分辨率，按游戏自己的情况改，本仓库未实测。
- **Steamworks SDK**（成就、云存档、Steam 登录等）：本仓库未实测。
