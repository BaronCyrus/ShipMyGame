// Paste into the page (javascript tool / DevTools console) after serve_cors.py is running.
// Puts a local file into a file input, including inputs inside shadow roots, then fires the events
// the console's framework listens to. Upload ONE field at a time and wait for its thumbnail or
// progress bar before the next: parallel uploads overwrote each other on mp.weixin.qq.com.
//
//   await __pub.list()                                  // every <input type=file> on the page, with accept/name
//   await __pub.put(0, "http://127.0.0.1:8765/shot1.png") // index from list(), or a CSS selector
//
// Note: a console that opens a native file picker from a button may create its input only after the click;
// click the button once, cancel the picker, then list() again.
(() => {
  const inputs = () => {
    const out = [];
    const walk = (root) => {
      root.querySelectorAll("input[type=file]").forEach((i) => out.push(i));
      root.querySelectorAll("*").forEach((el) => el.shadowRoot && walk(el.shadowRoot));
      root.querySelectorAll("iframe").forEach((f) => {
        try { f.contentDocument && walk(f.contentDocument); } catch (e) { /* cross-origin frame */ }
      });
    };
    walk(document);
    return out;
  };
  window.__pub = {
    list: () => inputs().map((i, n) => ({ n, accept: i.accept, name: i.name, multiple: i.multiple, visible: !!i.offsetParent })),
    put: async (which, url, fileName, type) => {
      const all = inputs();
      const input = typeof which === "number" ? all[which] : all.find((i) => i.matches(which));
      if (!input) throw new Error("file input not found: " + which);
      const res = await fetch(url);
      if (!res.ok) throw new Error("HTTP " + res.status + " for " + url);
      const blob = await res.blob();
      const name = fileName || decodeURIComponent(url.split("/").pop());
      const file = new File([blob], name, { type: type || blob.type, lastModified: Date.now() });
      const dt = new DataTransfer();
      dt.items.add(file);
      input.files = dt.files;
      input.dispatchEvent(new Event("input", { bubbles: true, composed: true }));
      input.dispatchEvent(new Event("change", { bubbles: true, composed: true }));
      return { name, size: file.size, type: file.type };
    },
  };
  return "ready: " + inputs().length + " file input(s)";
})();
