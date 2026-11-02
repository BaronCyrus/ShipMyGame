#!/usr/bin/env node
// ShipMyGame — preview or upload a WeChat mini-game with miniprogram-ci, without opening 微信开发者工具.
// NOT TESTED (未实测): written from the official miniprogram-ci docs
// (https://developers.weixin.qq.com/miniprogram/dev/devtools/ci.html) and the type definitions shipped in
// miniprogram-ci 2.1.31; it has never been run against a real account.
// An upload only reaches 版本管理 → 开发版本; 体验版、提交审核、发布 stay in the MP web console.
//
// setup (once, outside the game project; Node >= 16.1):
//   npm i --prefix ~/.shipmygame/tools miniprogram-ci@2.1.31        (or set WX_CI_PREFIX to another prefix)
// usage:
//   node wx_ci.js preview <minigame dir> --appid <AppID> --key <private key> [--qr out.png] [--desc text] [--robot 1]
//   node wx_ci.js upload  <minigame dir> --appid <AppID> --key <private key> --version 1.0.0 [--desc text] [--robot 1]
//   optional: --setting '{"es6":true,"minify":true}'  (default {"useProjectConfig":true}: reuse the exported
//             project.config.json; useProjectConfig is in the package's types but not in the docs page)
// The key is the 「小程序代码上传密钥」 from 开发管理 → 开发设置 → 小程序代码上传. Keep it in
// ~/.shipmygame/keys/<game>/ with mode 600. With the IP 白名单 on, this machine's public IP must be listed.
'use strict';
const os = require('os');
const path = require('path');

function loadCi() {
  const prefix = process.env.WX_CI_PREFIX || path.join(os.homedir(), '.shipmygame', 'tools');
  try {
    return require(require.resolve('miniprogram-ci', { paths: [prefix, process.cwd(), __dirname] }));
  } catch (e) {
    console.error(`miniprogram-ci not found. Install it: npm i --prefix ${prefix} miniprogram-ci@2.1.31`);
    process.exit(2);
  }
}

function parseArgs(argv) {
  const [cmd, dir, ...rest] = argv;
  const opts = { cmd, dir };
  for (let i = 0; i < rest.length; i += 2) {
    const k = rest[i];
    const v = rest[i + 1];
    if (!k || !k.startsWith('--') || v === undefined) throw new Error(`bad argument near "${k}"`);
    opts[k.slice(2)] = v;
  }
  return opts;
}

async function main() {
  const o = parseArgs(process.argv.slice(2));
  if (!['preview', 'upload'].includes(o.cmd) || !o.dir || !o.appid || !o.key) {
    console.error('usage: wx_ci.js <preview|upload> <minigame dir> --appid <AppID> --key <private key> [--version x.y.z] [--qr out.png] [--desc text] [--robot 1] [--setting json]');
    process.exit(2);
  }
  const robot = Number(o.robot || 1);
  if (!(robot >= 1 && robot <= 30)) throw new Error('--robot must be 1..30');
  const ci = loadCi();
  const project = new ci.Project({
    appid: o.appid,
    type: 'miniGame',                 // mini-game, not the default miniProgram
    projectPath: path.resolve(o.dir), // the folder that holds project.config.json and game.json
    privateKeyPath: path.resolve(o.key),
    ignores: ['node_modules/**/*'],
  });
  const common = {
    project,
    desc: o.desc || '',
    robot,
    setting: o.setting ? JSON.parse(o.setting) : { useProjectConfig: true },
    onProgressUpdate: () => {},
  };
  let res;
  if (o.cmd === 'preview') {
    const qr = path.resolve(o.qr || 'wx-preview.png');
    res = await ci.preview({ ...common, qrcodeFormat: 'image', qrcodeOutputDest: qr });
    console.log(`preview QR code: ${qr}`);
  } else {
    if (!o.version) throw new Error('--version is required for upload');
    res = await ci.upload({ ...common, version: o.version });
    console.log(`uploaded ${o.version} (robot ${robot}) -> 版本管理 → 开发版本`);
  }
  // __FULL__ = whole package, __APP__ = main package (limit 4MB), others = subpackages
  for (const p of (res && res.subPackageInfo) || []) console.log(`${p.name}\t${p.size}`);
}

main().catch((e) => {
  console.error(e && e.message ? e.message : e);
  process.exit(1);
});
