# 测试1记菜器 v2.8

v2.8 修改模式开关逻辑：

```text
四人模式开关 = 关
双人模式开关 = 关
```

这种情况下，进入测试1不会唤起记菜器菜单窗口。

规则现在是：

```text
只开四人模式 => 显示四人 1/2/3/4 号位
只开双人模式 => 显示双人 1/2 号位
两个都开 => 自动只保留最后打开的模式
两个都关 => 不显示菜单
```

控制台仍然只有三部分：

```text
1. 四人模式
2. 双人模式
3. 通用功能
```

安装：

```powershell
cd C:\Users\11569\Downloads\OC2Test1RecipeCounter_v2_8_source\OC2Test1RecipeCounter_v2_8_source
powershell -ExecutionPolicy Bypass -File .\build.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Overcooked! 2" -Install
```

启动后确认：

```text
Loading [测试1记菜器 2.8.0]
测试1记菜器 v2.8.0 loaded.
```
