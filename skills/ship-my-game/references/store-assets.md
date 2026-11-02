# 商店素材的做法

规格（尺寸、数量、时长）以渠道 skill 为准。这里是三个渠道都用得上的做法，全部 `[实测]`。

## 截图：专用的「截图构建」

在游戏里加一个只在截图构建里生效的编译符号（例如 `SHOTS`），让它能从启动参数直接打开某个画面、某套主题、某个关卡，并隐藏广告和调试信息。这样截图可重复、无广告、无真实账号信息。

安卓模拟器上逐张截取：

```bash
adb shell am force-stop <包名>
adb shell am start -n <包名>/com.unity3d.player.UnityPlayerGameActivity --es shot home --es lang zh-Hans
sleep 7
adb exec-out screencap -p > publish/<渠道>/raw/home.png
```

Unity 6 的默认 Activity 是 `com.unity3d.player.UnityPlayerGameActivity`，旧版本是 `UnityPlayerActivity`；以 `adb shell cmd package resolve-activity --brief <包名>` 的输出为准。游戏里用 `AndroidJavaObject` 读 Intent 的 extras。

- 模拟器分辨率设成目标比例（例如 1080×1920），截出来就是商店要的尺寸。
- 只有一个模拟器给这个工程用时，固定它的端口，`adb -s emulator-<端口>` 只操作它，不碰其他会话的设备。
- 无头模拟器（`-no-window`）可以截图、录屏，但**声音无法验证**（模拟器音频输出会失败）。声音请用户在真机上确认。

## 截图：拼成商店图

原始截图加标题文字、设备框、背景色，用 Python + Pillow 生成，脚本放在 `publish/<渠道>/`，改文案后一条命令全部重出。各渠道对图片上能写什么有规定（例如 TapTap 宣传图上只能有游戏名），以渠道 skill 为准。

## 录屏：实机演示视频

安卓模拟器 `screenrecord` + `input` 自动操作：

```bash
adb shell screenrecord --bit-rate 8000000 --time-limit 60 /sdcard/demo.mp4 &
adb shell input swipe <x1> <y1> <x2> <y2> <毫秒>    # 或 input tap <x> <y>
adb shell pkill -INT screenrecord
adb pull /sdcard/demo.mp4 publish/<渠道>/video/
```

上传前转成常见编码：`ffmpeg -i demo.mp4 -r 30 -c:v libx264 -pix_fmt yuv420p -c:a aac -shortest out.mp4`（没有声轨的平台要求有声轨时，加一条静音音轨：`-f lavfi -i anullsrc`）。

## 平台要求「在平台里截图」

微信小游戏备案要求截图来自微信里运行的小游戏，并能看到当前小游戏名。这类截图只能由用户在手机上截：你列好清单（每张截什么画面、对应表单哪个字段），用户截完发给你，你按字段重命名、检查格式和大小。

## 图标、宣传图

- 像素风、几何风这类可以用代码画；角色、场景类建议用图像生成模型出图后再加工。
- 用了 AI 生成的图片，在要求声明的后台如实声明（Google Play 的商店图片有「AI 生成」标注项）。
- 名字、商标：不要用别人的商标词做游戏名或关键词。
