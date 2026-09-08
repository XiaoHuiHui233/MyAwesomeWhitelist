# Changelog

所有对本插件有影响的变更按版本记录于此。发布流程从本文件为每个版本提取发布说明——
发布的版本必须有且仅有一个 `## <版本号>` 小节，标题必须与
`thunderstore/manifest.json` 的 `version_number` 完全一致（见 `docs/RELEASING.md`）。

## 0.1.0

- 强制踢出：主机端服务端单方面断开（销毁该客户端全部对象 + 广播移除），并在 Steam P2P 层拒绝其重连；客户端"防踢"补丁无法拦截。
- 白名单：可开关；仅白名单玩家可加入；永久（写入文件）+ 临时（仅当前房间）。
- 黑名单：可开关；黑名单玩家在握手前即被拒绝（表现为"连接失败"）；房间内黑名单玩家每秒自动强制踢出。
- 仅主机生效：客户端或单人模式自动停用，踢人不误伤。
- 游戏内 IMGUI 管理窗口：玩家 / 白名单 / 黑名单 / 好友 / 设置 五个 tab；默认 `Ctrl+Shift+W` 开关（可在设置 tab 或 `.cfg` 修改），macOS 上 `Ctrl` 匹配 `Cmd`。
- 多语言界面：`BepInEx/config/MyAwesomeWhitelist/lang/*.txt`，升级后缺失翻译自动补齐。
- 白名单 / 黑名单名单文件：`whitelist.json`、`blacklist.json`，永久条目跨重启保留。