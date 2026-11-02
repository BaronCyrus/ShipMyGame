// Boots a TapTap mini-game export in node as far as it can go without WebGL, to catch startup failures before an
// upload and a 20-minute 包体准入测试. It loads the converted framework with stubbed mini-game globals, gives it the
// functions the bundled unity-sdk defines as TJWASMSDK, links the real wasm through Module.instantiateWasm and
// writes the data package into FS the way the cached UnityPlugin does: a moment later, then it removes the
// "wasm-preloadAssets" run dependency. A good build prints "main() ran" after the data package (main itself stops
// in InitWebGLPlayer, which needs a real WebGL context). ShipMyGame: used before every Unity 6000.6 upload from 1.0.5 on;
// it reproduced the link error of 1.0.4 and caught the SystemInfo and canvas-selector failures before 1.0.6 reached a
// device. It cannot see first-frame problems (no game loop runs here), so the 包体准入测试 still decides.
//   node tap_boot_test.js <dst from publish/taptap/build.json>/minigame
// Exit codes: 0 main() ran, 1 wasm link failed, 2 framework threw, 3 timeout, 4 receiveInstance threw,
// 5 main() ran before the data package was in FS.
const fs = require('fs'), path = require('path'), zlib = require('zlib');

const dir = process.argv[2];
if (!dir) { console.error('usage: node tap_boot_test.js <Builds/taptap/minigame>'); process.exit(64); }
const only = (sub, suffix) => {
  const files = fs.readdirSync(path.join(dir, sub)).filter((f) => f.endsWith(suffix));
  if (files.length !== 1) throw new Error(`expected one *${suffix} in ${sub}, found ${files.length}`);
  return path.join(dir, sub, files[0]);
};
const wasm = zlib.brotliDecompressSync(fs.readFileSync(only('wasmcode', '.wasm.br')));
const data = zlib.brotliDecompressSync(fs.readFileSync(only('data-package', '.bin.br')));
let code = fs.readFileSync(path.join(dir, 'webgl.wasm.framework.unityweb.js'), 'utf8');
const mainCall = 'try{var ret=entryFunction(argc,argv);';
if (code.includes(mainCall)) {
  code = code.replace(mainCall, 'try{console.log("main() starts");var ret=entryFunction(argc,argv);');
  code = code.replace('catch(e){return handleException(e)}',
    'catch(e){console.log("main() threw: "+String(e&&(e.stack||e.message)||e).split("\\n").slice(0,3).join(" | "));' +
    'if(globalThis.tapTrace&&globalThis.tapTrace.length)console.log("last imports:\\n  "+globalThis.tapTrace.join("\\n  "));return handleException(e)}');
}

// TJWASMSDK holds exactly the _JS_* functions the unity-sdk declares, so a missing one fails the link as on a
// device; its other members (canvasContext, ...) are stubs
const sdkFunctions = {};
(function walk(d) {
  for (const f of fs.readdirSync(d)) {
    const p = path.join(d, f);
    if (fs.statSync(p).isDirectory()) walk(p);
    else if (p.endsWith('.js')) {
      for (const m of fs.readFileSync(p, 'utf8').matchAll(/(?<![\w$.])(_[A-Za-z]\w*)\s*:\s*function/g)) sdkFunctions[m[1]] = () => 0;
    }
  }
})(path.join(dir, 'unity-sdk'));

// anything not modelled is a stub: callable, constructible, every property another stub, 0 as a primitive
const stub = new Proxy(function () {}, {
  get: (t, k) => (k === Symbol.toPrimitive ? () => 0 : typeof k === 'symbol' || k === 'then' ? undefined : stub),
  apply: () => stub, construct: () => stub, set: () => true,
});
const obj = (base) => new Proxy(base, {
  get: (t, k) => (k in t ? t[k] : typeof k === 'symbol' ? undefined : stub),
  set: (t, k, v) => ((t[k] = v), true),
});
const TJWASMSDK = new Proxy(sdkFunctions, {
  get: (t, k) => (k in t ? t[k] : typeof k === 'symbol' || String(k).startsWith('_') ? undefined : stub),
});
// the main canvas as weapp-adapter leaves it: no id, a getBoundingClientRect, no real WebGL behind getContext
const canvas = obj({
  id: undefined, width: 720, height: 1600, addEventListener() {}, removeEventListener() {},
  getBoundingClientRect: () => ({ top: 0, left: 0, width: 360, height: 800 }),
});
const window = obj({ TJWASMSDK, canvas, CSS: undefined, addEventListener() {}, removeEventListener() {}, navigator: { userAgent: 'TapTap Android' } });
// unityNamespace comes from the package's own unity-namespace.js (TJAssetBundles, PathInFileOS and the loader).
// GameGlobal is the global object in a mini-game, so its properties are also read as bare globals.
const gg = { unityNamespace: undefined, manager: obj({}) };
const GameGlobal = obj(gg);
const tj = obj({
  env: { USER_DATA_PATH: 'tjfile://usr' },
  getSystemInfoSync: () => ({ platform: 'android', system: 'Android 13', pixelRatio: 2, screenWidth: 360, screenHeight: 800, windowWidth: 360, windowHeight: 800 }),
});
const mod = { exports: {} };
const named = {
  module: mod, exports: mod.exports, window, GameGlobal, tj, navigator: window.navigator, ENVIRONMENT_IS_PTHREAD: undefined,
  // weapp-adapter's querySelector knows only these names for the canvas
  document: obj({ addEventListener() {}, removeEventListener() {}, querySelector: (q) => (q === 'canvas' || q === '#' + canvas.id ? canvas : null) }),
};
const scope = new Proxy(named, {
  has: (t, k) => typeof k === 'string',
  get: (t, k) => (k === Symbol.unscopables ? undefined : k in t ? t[k] : k in gg ? gg[k] : k in globalThis ? globalThis[k] : stub),
  set: (t, k, v) => ((t[k] = v), true),
});
const run = (source, exportsObj) => new Function('scope', 'exportsObj', 'with (scope) { (function (exports) {' + source + '\n}).call(undefined, exportsObj); }')(scope, exportsObj);
run(fs.readFileSync(path.join(dir, 'unity-namespace.js'), 'utf8'), {});
run(code, mod.exports);
const ns = GameGlobal.unityNamespace;

// the plugin's side of the contract: exactly the fields of the Module literal in cachedPlugin/UnityPlugin/index.js
// (UnityManager.prepareModule), with its callbacks reduced to what the test needs
const Module = {
  IsTjHostGame: true, IsWxGame: true, graphicsAPI: ['WebGL 2.0', 'WebGL 1.0'],
  monitorRunDependencies() {}, wasmInstantiated() {}, onRuntimeInitialized() {},
  preRun: [], postRun: [], Jobs: {}, canvas, buildDownloadProgress: {},
  resolveBuildUrl: (p) => p, streamingAssetsUrl: () => 'StreamingAssets', pthreadMainPrefixURL: 'Build/',
  webglContextAttributes: { premultipliedAlpha: 1, preserveDrawingBuffer: 1 }, arguments: [],
  thisProgram: undefined, quit: undefined, read: undefined, readAsync: undefined, readBinary: undefined,
  setWindowTitle: undefined, wasmBinary: undefined, noExitRuntime: undefined, INITIAL_MEMORY: undefined,
  print: (m) => console.log('[unity]', String(m).slice(0, 200)),
  printErr: (m) => console.log('[unity err]', String(m).slice(0, 200)),
  instantiateWasm(imports, receiveInstance) {
    if (process.env.TAP_TRACE) {
      // TAP_TRACE=1 prints the last JS imports the engine called before main() threw
      for (const k of Object.keys(imports.env)) {
        const f = imports.env[k];
        if (typeof f !== 'function') continue;
        imports.env[k] = function () {
          trace.push(k + '(' + Array.from(arguments).join(',').slice(0, 60) + ')');
          if (trace.length > 40) trace.shift();
          const ret = f.apply(this, arguments);
          trace[trace.length - 1] += ' -> ' + String(typeof ret === 'function' ? 'fn' : ret).slice(0, 40);
          return ret;
        };
      }
    }
    WebAssembly.instantiate(wasm, imports).then((r) => {
      console.log('wasm linked');
      // on devices the data package subpackage lands after the wasm one, so the plugin processes it later
      setTimeout(writeDataPackage, 500);
      try { receiveInstance(r.instance, r.module); } catch (e) { console.log('receiveInstance threw:', e && e.stack ? e.stack.split('\n').slice(0, 3).join(' | ') : e); process.exit(4); }
    }, (e) => { console.log('wasm link failed:', e.message); process.exit(1); });
    return {};
  },
  unityFileSystemInit() {
    ns.FS.mkdir('/idbfs');
    ns.FS.mount(ns.IDBFS, {}, '/idbfs');
    console.log('/idbfs mounted through unityNamespace.FS and IDBFS');
  },
  tuanjieFileSystemInit() { Module.unityFileSystemInit(); },
  calledMainCb() {
    if (!dataWritten) { console.log('main() ran BEFORE the data package was in FS'); process.exit(5); }
    console.log('main() ran after the data package: Module.calledMainCb fired');
    process.exit(0);
  },
  onAbort(what) { console.log('abort:', String(what).slice(0, 300)); },
};
process.on('unhandledRejection', (e) => console.log('unhandled rejection:', e && e.stack ? e.stack.split('\n').slice(0, 3).join(' | ') : e));
let dataWritten = false;
const trace = [];
globalThis.tapTrace = trace;
// the plugin's prepareModule sets UnityLoader before starting Unity; SystemInfo has these fields and no others
ns.UnityLoader = { SystemInfo: { width: 360, height: 800, gpu: 'unknown', browser: 'tj', browserVersion: '0.0', language: 'zh-CN', hasWebGL: 2 }, UnityCache: { XMLHttpRequest: stub } };
// the plugin starts Unity through unityNamespace.UnityModule, which the framework sets as its last statement
const started = ns.UnityModule(Module);

// UnityWebData1.0: header end, then (offset, size, name length, name) per file
function writeDataPackage() {
  const u8 = new Uint8Array(data.buffer, data.byteOffset, data.byteLength);
  const dv = new DataView(data.buffer, data.byteOffset, data.byteLength);
  let o = 'UnityWebData1.0\0'.length;
  const end = dv.getUint32(o, true);
  o += 4;
  let n = 0;
  while (o < end) {
    const off = dv.getUint32(o, true), size = dv.getUint32(o + 4, true), len = dv.getUint32(o + 8, true);
    o += 12;
    const name = Buffer.from(u8.subarray(o, o + len)).toString('latin1');
    o += len;
    for (let a = 0, b = name.indexOf('/', a) + 1; b > 0; a = b, b = name.indexOf('/', a) + 1) ns.FS.createPath(name.substring(0, a), name.substring(a, b - 1), true, true);
    ns.FS.createDataFile(name, null, u8.slice(off, off + size), true, true, true);
    n++;
  }
  dataWritten = true;
  console.log(`data package: ${n} files written through unityNamespace.FS`);
  Module.removeRunDependency('wasm-preloadAssets');
}
Promise.resolve(started).catch((e) => { console.log('framework threw:', e && e.stack ? e.stack.split('\n').slice(0, 4).join(' | ') : e); process.exit(2); });
setTimeout(() => { console.log('timeout: main() never ran'); process.exit(3); }, 60000);
