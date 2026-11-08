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

1. 模拟器分辨率直接设成成片尺寸（例如 1080×1920），用 `-gpu host` 启动。游戏跑满 60 帧时，录出来也接近 60fps。
2. 用截图构建从启动参数直接打开要录的画面（例如空关卡），成片开头就没有加载和转场。
3. 把操作写成脚本，推到设备上一次跑完，节奏不受 adb 往返影响：
   ```bash
   adb push moves.sh /data/local/tmp/moves.sh   # 每行一条 input swipe 或 input tap，中间用 sleep 0.3 这类小数秒隔开
   adb shell screenrecord --bit-rate 20000000 --time-limit 120 /sdcard/demo.mp4 &
   adb shell sh /data/local/tmp/moves.sh
   adb shell pkill -INT screenrecord
   adb pull /sdcard/demo.mp4 publish/<渠道>/video/raw.mp4
   ```
   - 坐标从启动后的截图里算（例如找棋盘的网格线），不要写死：换主题、换关卡后布局会变。
   - 操作要像真人：按玩家思考的先后顺序走（解谜游戏按推理能确定的顺序），每步之间停 0.2～0.7 秒不等。拖动速度由 `input swipe` 的时长决定；点单个格子可以用起点和终点相同的 swipe。
4. 模拟器录不到声音。平台要声音时，后期把游戏自己的配乐和音效合进去。音效放在画面变化的那一帧，逐帧比较目标区域的亮度就能找到；各声音的音量比例照游戏里的设置。
5. 用 ffmpeg 剪辑和编码：
   - 先统一帧率（`-vf fps=60`），再剪掉头尾。在弹窗、广告出现之前结束；想让最后一帧多停一会儿，用 `tpad=stop_mode=clone:stop_duration=1.2`。
   - 编码参数：`-c:v libx264 -profile:v high -crf 17 -pix_fmt yuv420p -c:a aac -b:a 192k -ar 44100 -movflags +faststart`。
   - 平台要求有声轨、而你没有声音时，加一条静音音轨：`-f lavfi -i anullsrc`。
   - 用 `ffprobe` 核对时长、分辨率、帧率和体积，再交给用户看。
6. 平台要求封面和视频同一比例时，用录屏里的一帧做底图（`ffmpeg -ss <秒> -i raw.mp4 -frames:v 1 cover.png`），再按渠道规则加游戏名或 logo。

以上 `[实测]`：微信小游戏推广视频「实机画面」三套，见 [ship-wechat-minigame 的 listing.md](../../ship-wechat-minigame/references/listing.md#运营素材)。

## 平台要求「在平台里截图」

微信小游戏备案要求截图来自微信里运行的小游戏，并能看到当前小游戏名。这类截图只能由用户在手机上截：你列好清单（每张截什么画面、对应表单哪个字段），用户截完发给你，你按字段重命名、检查格式和大小。

## 图标、宣传图

- 像素风、几何风这类可以用代码画；角色、场景类建议用图像生成模型出图后再加工。
- 用了 AI 生成的图片，在要求声明的后台如实声明（Google Play 的商店图片有「AI 生成」标注项）。
- 名字、商标：不要用别人的商标词做游戏名或关键词。
