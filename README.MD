# VOCALOID Patcher
一个用于为 `VOCALOID 6` 编辑器提供额外功能的补丁, 理论上支持所有版本的 V6 编辑器

### 翻译条目版本: `6.12.0.1`
### 已测试的编辑器版本: `6.1.0.2 (.net 6.0)`, `6.6.0`, `6.11.0.1`, `6.12.0.1`

提交反馈/技术交流/单纯聊天 请加入 `422827160`  
我没有什么使用 V6 编辑器的经验, 所以可能会出现翻译不准确的情况。开个 issue 或者加群反馈给我, 然后下个版本我会修改翻译。

## 安装
前往 [Releases](https://github.com/IzumiiKonata/VOCALOIDPatcher/releases) 中下载最新发行版  
如果您的编辑器版本较老, 请下载 `.net 6.0` 的版本  
将压缩包中的所有文件 _(除了 README.txt)_ 复制并替换到 `C:\Program Files\VOCALOID6\Editor` 中 (如果编辑器没有安装在默认目录中可能会有问题)  
打开编辑器并打开一个工程, 如果菜单栏中出现 `VOCALOID Patcher` 选项则安装成功。  
![img](./res/screenshot.png)

## 功能

### 界面汉化 / 多语言
- 翻译编辑器的资源条目与硬编码字符串, 翻译文件为外置 xml, 可自行修改。

### 钢琴窗增强
- **显示其他轨道的音符**: 在当前轨道的钢琴窗中叠加显示其它轨道的音符, 可选择跳过静音轨道。
- **显示音符音高**: 在每个音符上叠加显示音高名 (如 `C4`, `F#3`)。
- **显示声库封面**: 在钢琴窗右下角显示当前乐段所用声库的封面, 可调整大小与不透明度。

### 任务
- **歌词替换**: 将选中音符的歌词批量替换为指定音节。
- **量化时值**: 按网格量化音符时值。
- **生成和声**: 按调式根音生成 上/下 三、四、五、六、八度等多个声部, 可写入现有或新建轨道。
- **Swing 节奏**: 钢琴窗右键菜单内置 8 分 / 16 分的 Swing 与 Shuffle 预设。

### 多格式导入 / 导出
- 在原生 `Vpr/Vsqx/MIDI` 之外, 额外支持与以下工程格式互转:
  `UST` (UTAU)、`USTX` (OpenUTAU)、`CCS` (CeVIO)、`SVP` (Synthesizer V)、`S5P`、`MusicXML`、`DV` (DeepVocal)、`VSQ`、`PPSF`、`Tssln` (VoiSona)、`ufdata`。
- 导入/导出时会按目标格式自动转换音高曲线、音素等数据。

### 其它
- **定时保存**: 按设定间隔自动保存当前工程。

## 调试
安装该补丁后, 在打开 `VOCALOID 6` 编辑器时长按 `左 Shift` 会打开控制台, `VOCALOIDPatcher` 会在其中显示一些 Debug 日志。

## 更新翻译
运行 `ResourceTranslationComparer`, 控制台会输出翻译比对结果:

- **[1] 未翻译条目**: 新增的 resx 翻译条目
- **[2] 未翻译且编辑器未使用条目**: resx 条目, 但是没有 UI 元素引用的条目
- **[3] 硬编码字符串**: 添加到 `HardcodedPropertyMap.xml` 中, (字面量 → 自定义键)
- **[4] 多余键 / [5] 未使用的硬编码字符串**

使用参数 `--write-map` 可额外导出一份整理过的 `HardcodedPropertyMap.generated.xml`

## 开发

### 注意事项
你需要在默认目录中 `(C:\Program Files\VOCALOID6\Editor)` 安装一份最新的 VOCALOID6 编辑器  
编译完成后, 会在 `VOCALOIDPatcher\bin\Release\netX.0-windows\out` 中构建一个包含依赖的 `Microsoft.Xaml.Behaviors.dll`。

