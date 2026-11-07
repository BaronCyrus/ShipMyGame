# 把文件传进后台

截图、视频、宣传图、包体（AAB / zip）、备案文件都要传进网页后台。按顺序选第一个可用的方法。

## 1. 官方 API / 命令行

不经过浏览器，最稳。渠道 skill 里写了各自的方式（例如 Google Play Developer API、微信 miniprogram-ci、TapTap CLI），以及需要先在后台生成什么密钥。密钥按 [../SKILL.md](../SKILL.md)「密钥与隐私」保存。

## 2. 浏览器工具自带的上传

Claude in Chrome 有上传工具，单次最多 10 MB `[实测]`。截图、宣传图通常够用。

## 3. 本机中转（仅限用户自己的 Chrome）`[实测]`

适用：文件超过 10 MB、浏览器工具没有上传能力、或上传框藏在 shadow root 里找不到。在 mp.weixin.qq.com（备案截图）和 developer.taptap.cn（十几 MB 的游戏包）实测可用；在 Claude 桌面版内置浏览器里**不可用**（它拦截页面访问本机地址，见 [browser.md](browser.md)）。

1. 把要传的文件放进一个只含这些文件的目录。
2. 启动中转服务（只监听 127.0.0.1，只回答后台的域名，30 分钟后自动退出）：
   ```bash
   python3 <本 skill 目录>/scripts/serve_cors.py <文件目录> --port 8765
   ```
   后台域名不在默认名单里时，加 `--origin https://<后台域名>`。
3. 在后台页面执行 [`../scripts/page_upload.js`](../scripts/page_upload.js) 的内容，然后：
   ```js
   __pub.list()                                            // 列出页面上所有上传框（含 shadow root 里的）
   await __pub.put(0, "http://127.0.0.1:8765/截图1.png")    // 按序号或 CSS 选择器指定上传框
   ```
4. 读页面，确认缩略图或进度条出现、文件名和大小正确，再传下一个。
5. 用完停掉服务。

要点：

- **一次只传一个字段**，等它显示缩略图再传下一个；并行上传时后传的会覆盖先传的 `[实测]`。
- 有的上传框要先点一下「上传」按钮才会出现在页面上：点开，取消系统文件框，再 `__pub.list()`。如果按钮是用 `click()` 打开文件框的，按下一节先截住它，就不会弹系统文件框。
- 文件名尽量用 ASCII；有的后台用文件名做展示。

### 按钮用 click() 打开文件框时

有的按钮点下去，才对一个隐藏的文件框调用 `click()`，直接点会弹出系统文件框。实测：微信后台「运营素材管理」的「上传图片」。先在页面里截住这次调用 `[实测]`：

```js
window.__captured = [];
// 微前端（wujie）的脚本跑在同源 iframe 里，那些 iframe 的原型也要改
const wins = [window, ...[...document.querySelectorAll("iframe")]
  .map((f) => { try { return f.contentWindow; } catch (e) { return null; } }).filter(Boolean)];
for (const w of wins) {
  try {
    const proto = w.HTMLInputElement.prototype;
    if (!proto.__origClick) proto.__origClick = proto.click;
    proto.click = function () {
      if (this.type === "file") { window.__captured.push(this); return; }
      return proto.__origClick.call(this);
    };
  } catch (e) { /* 跨域 iframe */ }
}
```

然后按坐标真点那个按钮。`window.__captured[0]` 就是文件框，把文件放进去：

```js
const input = window.__captured[0];
const blob = await (await fetch("http://127.0.0.1:8765/cover.jpg")).blob();
const dt = new DataTransfer();
dt.items.add(new File([blob], "cover.jpg", { type: "image/jpeg" }));
input.files = dt.files;
input.dispatchEvent(new Event("input", { bubbles: true, composed: true }));
input.dispatchEvent(new Event("change", { bubbles: true, composed: true }));
```

读页面确认出现缩略图，再点后台自己的「提交」。改过的原型刷新页面后就恢复了。

## 4. 请用户拖进去

前面都不行时（例如大于 10 MB 的 AAB、独立浏览器里的任何文件）：

1. 把文件复制到桌面，在 Finder / 资源管理器里选中它（macOS：`open -R <文件>`）。
2. 告诉用户：拖到哪个页面的哪个框，拖完回来说一声。
3. 读页面确认上传完成（文件名、大小、版本号），再继续。

## 读后台里隐藏的值

有的值页面上不显示，只给「复制」按钮（例如 Play 应用签名密钥的 SHA-1）。先在页面里接管剪贴板写入，再点复制按钮 `[实测]`：

```js
window.__copied = [];
navigator.clipboard.writeText = async (t) => { window.__copied.push(t); };
// 点击复制按钮后读取 window.__copied
```
