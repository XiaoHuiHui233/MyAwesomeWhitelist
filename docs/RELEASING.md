# Releasing

MyAwesomeWhitelist 的发布由 GitHub Actions
([`.github/workflows/release.yml`](../.github/workflows/release.yml)) 在每次 push 到
`main` 时自动完成:构建插件、发 GitHub Release、上传包到 Thunderstore。

## 一次性配置

CI 需要游戏 / BepInEx 的引用 DLL 才能编译,但这些是受版权保护的文件,不能进入公开
git 历史。本插件把它们放在仓库里的 **`game-refs` GitHub Release** 中(与 HSRTimer 的
私有 draft release 思路一致;此处用公开 release 亦可,因为没有私有 runner 要求)。

1. 运行 `scripts/make-refs.sh`,把生成的 `dist/refs.zip` 上传到本仓库的 `game-refs`
   release:

   ```sh
   gh release create game-refs dist/refs.zip \
     --title "Game refs" --notes "Human: Fall Flat Managed DLLs (build refs)"
   ```

   游戏每次更新后重新上传:

   ```sh
   gh release upload game-refs dist/refs.zip --clobber
   ```

2. 添加仓库 secret `THUNDERSTORE_TOKEN`(来自 thunderstore.io 设置页)。这是唯一需要的
   secret——CI 用的是 GitHub ubuntu 托管 runner,BepInEx core 按
   `thunderstore/manifest.json` 的依赖版本从 Thunderstore 直接下载,游戏 DLL 从
   `game-refs` release 下载,不需要自托管 runner。

## 发布流程

1. 手动 bump 版本号。版本号存在于**三处**,必须保持完全一致(CI 会校验,不一致则
   失败并退出):
   - `src/MyAwesomeWhitelist/PluginInfo.cs` → `PLUGIN_VERSION`
   - `src/MyAwesomeWhitelist/MyAwesomeWhitelist.csproj` → `<Version>`
   - `thunderstore/manifest.json` → `version_number`

   ⚠️ 版本号当前锁定在 `0.1.0`,除非明确要求否则不要 bump(且不要用 `0.0.0` 或 `dev`
   这类占位版本——CI 会直接拒绝)。

2. 在 `CHANGELOG.md` 中为这个版本添加一个 `## <版本号>` 小节,写明变更。发布说明就是从
   这里提取的;**没有对应条目时 CI 会失败**,所以要么每次发布都写,要么先加条目。

3. 提交并 push 到 `main`。工作流随后:
   - 读取 `thunderstore/manifest.json` 的版本,并校验它和 csproj / PluginInfo 一致、不是占位版本;
   - 从 `game-refs` release 下载游戏 DLL,从 Thunderstore 下载该版本的 BepInEx core;
   - `scripts/pack.sh` 构建并打包成 `dist/MyAwesomeWhitelist-<version>.zip`(DLL + manifest + icon + README);
   - 从 `CHANGELOG.md` 提取该版本的发布说明;
   - 创建 GitHub Release `v<version>`(用 CHANGELOG 内容作为说明);
   - 用 `tcli` 把 zip 上传到 Thunderstore(namespace `XiaoHuiHui233`,社区
     `human-fall-flat`)。

4. **同版本重复 push(如 rebase + force-push)**:此时 `v<version>` tag 已存在,工作流
   **只构建、不发布**——因为 Thunderstore 拒绝重复的版本号,无法覆盖。要再次发布就
   bump 版本。

## 手动触发

工作流也支持 `workflow_dispatch`,可以在 Actions 面板直接点 Run,不必触碰 `main` 来
测试整个发布链路。