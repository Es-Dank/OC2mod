# 玩家位置分配器 v1.0

这个 Mod 只做“进入关卡前生效”的玩家位置分配，不做局内实时换人。

修改设置后，需要：

```text
退出当前关卡
在 Configuration Manager 里改位置
重新进入关卡
```

## 默认映射

```text
1号位 = 蓝色
2号位 = 红色
3号位 = 绿色
4号位 = 黄色
```

## 示例

如果想让 4号黄色玩家去 2号位，2号红色玩家去 4号位：

```text
1号位 = 蓝色
2号位 = 黄色
3号位 = 绿色
4号位 = 红色
```

然后退出当前关卡，重新进入关卡。

## 控制台配置

```text
玩家位置分配器
- 启用
- 1号位：蓝色 / 红色 / 绿色 / 黄色
- 2号位：蓝色 / 红色 / 绿色 / 黄色
- 3号位：蓝色 / 红色 / 绿色 / 黄色
- 4号位：蓝色 / 红色 / 绿色 / 黄色
- 调试日志
```

四个位置不要选择重复颜色。如果重复，插件不会应用本次换位。

## 和旧主机换位 Mod 的关系

这个版本会 patch `CampaignKitchenLoaderManager.AssignChefEntities`，并声明在旧 `dev.gua.overcooked.hostcolor` 之后执行。

但为了减少冲突，测试时建议先把旧的：

```text
OC2HostColor-主机换位.dll
```

从 `BepInEx/plugins` 里移走或改后缀禁用。

## 安装

```powershell
cd C:\Users\11569\Downloads\OC2PlayerSlotMapper_v1_0_source\OC2PlayerSlotMapper_v1_0_source
powershell -ExecutionPolicy Bypass -File .\build.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Overcooked! 2" -Install
```

启动游戏后，日志应显示：

```text
Loading [玩家位置分配器 1.0.0]
玩家位置分配器 v1.0.0 loaded.
```

## GitHub archive

本目录里的 `OC2PlayerSlotMapper_v1_0_source.zip.b64` 是源码 ZIP 的 Base64 文本。

还原命令：

```powershell
certutil -decode OC2PlayerSlotMapper_v1_0_source.zip.b64 OC2PlayerSlotMapper_v1_0_source.zip
```
