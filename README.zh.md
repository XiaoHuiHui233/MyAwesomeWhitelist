# MyAwesomeWhitelist

Human: Fall Flat 主机端(房主)黑白名单管理 mod。

[English](README.md) | **中文**

## 功能

- **强制踢出** — 在主机端执行服务端单方面断开(销毁该客户端全部对象 + 广播移除),并在 Steam P2P 层拒绝其重连。客户端"防踢"补丁(如忽略 Kick 消息的外挂端)无法拦截。
- **白名单** — 可开关;仅白名单玩家可加入;永久(写入文件)+ 临时(仅当前房间,重建房间即失效)。
- **黑名单** — 可开关;黑名单玩家连入时直接拒绝通信(P2P 层关闭会话,不发送握手);永久 + 临时。
- **仅主机生效** — 你是房主时才拦截/踢人;客户端或单人模式自动停用。
- **游戏内 IMGUI 管理窗口** — 默认 `Ctrl+Shift+W` 开关(可在设置 tab 或 `.cfg` 中修改),macOS 上 `Ctrl` 匹配 `Cmd`。

## 安装

1. 安装 [BepInEx 5](https://thunderstore.io/c/human-fall-flat/p/BepInEx/BepInExPack/) (5.4.21 或更高)
2. 将本 mod 的 zip 拖入 [r2modman](https://thunderstore.io/c/human-fall-flat/) 或手动解压 `plugins/MyAwesomeWhitelist.dll` 到 `BepInEx/plugins/`

## 使用

1. 创建多人游戏房间(你是主机)
2. 按 `Ctrl+Shift+W` 打开窗口(游戏内会自动进入 Esc 菜单状态以释放鼠标;标题栏可拖动窗口)
3. **玩家列表** tab:开启白名单后可一键「执行白名单踢出」;黑名单玩家**每秒自动强制踢出**,无需手动操作
4. **白名单 / 黑名单** tab:列表框展示条目,「永久 / 临时」按钮切换;可删除或手动输入 SteamID64 添加
5. **好友** tab:按名称排序,支持前缀搜索,一键加白/拉黑
6. **设置** tab:修改开窗热键、下拉切换界面语言

名单文件(永久)持久化于:
`BepInEx/config/MyAwesomeWhitelist/whitelist.json`、`blacklist.json`

配置: `BepInEx/config/MyAwesomeWhitelist.cfg`

界面语言文件: `BepInEx/config/MyAwesomeWhitelist/lang/*.txt`(升级后缺失的翻译会自动补齐到文件末尾)

## 说明

- 临时名单在**重新创建房间**时自动清空;永久名单跨重启保留。
- 黑名单拒绝发生在握手之前:对方会表现为"连接失败",而不是"被踢出";已在房间内的黑名单玩家每秒自动强制踢出。
- **踢出 ≠ 永久封禁**:原版被踢过的玩家在你重启游戏前永远进不来;本 mod 中普通踢出、白名单拒绝都是一次性的(玩家可重进),只有黑名单会保持 Steam P2P 封禁;关闭黑名单开关、删除黑名单条目或重建房间会自动解除封禁。

## 构建

```bash
dotnet build -c Release          # 需要本地游戏安装(见 Directory.Build.props)
scripts/pack.sh                  # 生成 dist/MyAwesomeWhitelist-<ver>.zip (Thunderstore)
```

发布由 CI 自动处理,详见 [docs/RELEASING.md](docs/RELEASING.md)。