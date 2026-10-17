# 活侠传拓展

《活侠传》（Legend of Mortal）的 BepInEx 6 插件。

在原版「活侠传助手」的基础上修复了新版游戏的兼容问题，并加入了一些新功能。

**[▶ B 站功能演示视频](https://www.bilibili.com/video/BV1LZJh6YEUy/)**（3 分钟看完全部功能和安装方法）

## 下载安装

**[⬇ 下载 LOMAssistant.zip](https://github.com/kevinnie2003/LOMAssistant/releases/latest/download/LOMAssistant.zip)**（已包含 BepInEx，解压即用）

1. Steam 中右键《活侠传》→ 管理 → 浏览本地文件，打开游戏目录（里面有 `Mortal.exe`）。
2. 把压缩包里的所有内容解压到这个目录，提示同名文件时选择覆盖。
3. 正常启动游戏即可。

之前装过旧版 BepInEx 或原版助手的，同样直接覆盖即可。**请先删除 `BepInEx\plugins` 里的旧版骰子大师、多功能 Mod 等同类 Mod**，它们和本 Mod 改的是同一批游戏代码，会导致 Tab 窗口空白等问题。检测到冲突时，游戏启动几秒后会自动弹出 F8 面板，用红字提示是哪个 Mod。卸载：删除游戏目录下的 `winhttp.dll`。

## 来源声明

本项目不是从零开发的，功能思路和大部分代码来自以下作者，在此感谢：

- **[活侠传助手](https://mod.3dmgame.com/mod/256420)**：作者 爱死（3DM：wowowo4567）。本项目的基础，源码由其发布的插件反编译而来后再修改。
  - 该 Mod 又整合自三个原 Mod：
    - [骰子大师](https://mod.3dmgame.com/mod/210536)：Gmhaxx
    - [多功能 Mod](https://mod.3dmgame.com/mod/210629)：Jim97
    - [任意位置存档](https://mod.3dmgame.com/mod/210679)：NothingNullNull
- 修炼、锻造、炼丹相关功能参考了风灵月影《活侠传》修改器的功能思路（未使用其代码）。
- 存档备注在存档栏下方单独显示的做法，移植自 [LOM_SaveSlotExpander](https://gitlab.com/ame-chan/lom_saveslotexpander)（ame-chan，MIT 许可）及其[汉化版](https://github.com/shiluodeqiou/LOM_SaveSlotExpander-zh)（shiluodeqiou）。
- 下载包中附带的 [BepInEx](https://github.com/BepInEx/BepInEx)、[UnityDoorstop](https://github.com/NeighTools/UnityDoorstop)、[Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) 为各自作者的开源项目，按其原许可证分发。

如果原作者不希望本项目公开，请提 issue 联系，会及时处理。

## 功能

### 原版助手已有

- Tab：信息窗口（选项、开关、事件、任务）
- 对话选项结果直接显示
- 骰子手动选择：点击线路直接选中，跳过逆天判定
- 讲经堂解锁显示
- 决斗中按 ESC，菜单里有“直接获胜”
- F9：游戏内置的测试/编辑面板

### 本项目新增 / 修改

- **适配新版游戏**：修复新版游戏中 `CharacterIntroPanel.Show` 新增重载导致的补丁报错。
- **繁体转简体**：原版助手的繁转简函数是空的，信息窗口、选项结果、事件名、旗标等游戏数据一直显示繁体；现在用 Windows 自带的转换接口转成简体。
- **空过**：唐门地图界面按 ESC，菜单里有“空过”，消耗 1 次行动、不触发任何事件。主线任务等待中时不可空过。
- **团战直接获胜**：团战中按 ESC 暂停，菜单里有“直接获胜”。
- **F8 面板**（勾选即时生效，自动保存到配置）：
  - 无限修炼次数（武学点补满）、快速修炼
  - 无限锻造、炼丹次数（锻造点、炼丹点补满）、快速锻造、炼丹
  - 锻造、炼丹不消耗银两
  - 决斗中无敌、决斗中造成伤害为 0
  - 游戏倍速：1x / 2x / 3x / 5x（在游戏原本速度上再加速，暂停、剧情快进照常；多周目跳过剧情很方便）
  - 设定天命点（骰子“逆天”消耗的点数，进入存档后显示）
  - 弹珠小游戏中：直接设定总分、得分倍率
  - 存档/读档界面中：给所选存档写备注
- **存档栏位**：手动存档栏位从 20 个增加到 100 个（配置 `SaveSlotCount`）。
- **存档备注**：在存档/读档界面按 F8，可给所选存档写备注，备注以「【备注】」显示在存档栏下方。备注保存为存档旁的 `Save_XXX.dat.note`，随 Steam 云存档同步。从 LOM_SaveSlotExpander 换过来的，首次启动会自动导入它的 `SaveSlotComments.ini` 里的备注（已有备注的栏位跳过，不改动原文件）。存档文件两边通用，不用迁移；不要同时安装两个 Mod。
- **自动存档历史**：养成、剧情、战斗三类自动存档各额外保留 5 个旧档（配置 `AutoSaveHistory`）。读档界面每张自动存档卡片右侧的 ▲/▼ 可以切换到较新/更早的旧档，再点卡片读取。
- **四师兄售卖全部物品**（默认关闭，配置 `EnableShopAllItems = true` 打开）：来自 Jim97 多功能 Mod，活侠传助手整合时去掉了。商店里加入所有没拥有的书籍、杂物、特殊物品；不卖《证类本草》（提前买会导致六月后山事件出 bug）。加进去的商品不写进存档，关掉后商店恢复原样。
- **增强存档（随时存档）默认关闭**：原版的这个功能在新版游戏中会导致读档回到上个事件、出现奇怪背景音。仍可在配置中用 `EnableSaveSystem` 打开。

## 设置

常用开关都在游戏内 F8 面板里。其余设置在首次启动后生成的 `BepInEx\config\LOMAssistant.cfg` 中，改完重启游戏生效。

## 从源码构建

普通玩家直接用上面的下载包即可，这一节只给想改代码的人。

需要 .NET 8 SDK 和已安装 BepInEx 6（Unity.Mono，x64）的游戏目录（编译时直接引用游戏和 BepInEx 的程序集，本仓库不包含任何游戏文件）。

```bash
GAME="D:/Steam/steamapps/common/LegendOfMortal" DOTNET=/path/to/dotnet ./build.sh
```

脚本直接调用 Roslyn（csc）编译，并把 `LOMAssistant.dll` 复制进游戏的 `BepInEx\plugins\LOMAssistant\`。`Newtonsoft.Json.dll` 可以从下载包中获取。

## 目录

| 目录 | 内容 |
|---|---|
| `LOMAssistant/` | 插件入口、日志、骰子与信息窗口等（原版） |
| `LOMAssistant.Config/` | 配置项 |
| `LOMAssistant.Features.*` | 原版各功能 |
| `LOMAssistant.Features.Extras/` | 本项目新增的功能 |
| `build.sh` | 构建脚本 |
