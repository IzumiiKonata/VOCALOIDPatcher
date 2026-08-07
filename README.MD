# VOCALOID Patcher

[<img src="./res/lang_en.svg" alt="Read this in English" height="28">](./README_EN.MD)
[<img src="./res/lang_ja.svg" alt="日本語で読む" height="28">](./README_JA.MD)

VOCALOID Patcher 是一个直接加载进 `VOCALOID 6 Editor` 的功能补丁。它为编辑器补充多语言界面、钢琴窗功能、批处理工具和项目格式转换。

- 翻译条目版本：`6.13.1.1`
- 已测试版本：`6.1.0.2 (.NET 6)`、`6.6.0`、`6.11.0.1`、`6.12.0.1`、`6.13.0.1`、`6.13.1.1 (最新版)`
- 反馈与交流：QQ群 `422827160`

我平时并不重度使用 VOCALOID 6，翻译或格式转换难免会碰到没有覆盖到的地方。发现问题时请附上编辑器版本、源文件格式和复现步骤 (如果需要)，可以的话再提供一份能复现问题的精简工程。

## 安装

1. 从 [Releases](https://github.com/IzumiiKonata/VOCALOIDPatcher/releases) 下载最新版。
2. 较老的 VOCALOID 6 (6.3.0 及之前的版本) 请使用 `.NET 6` 构建；新版编辑器使用 `.NET 8` 构建 (没有 `_NET6` 后缀的版本)。
3. 将压缩包中的文件（`README.txt` 除外）复制到 `C:\Program Files\VOCALOID6\Editor` 并覆盖原文件。
4. 启动编辑器并打开任意工程。菜单栏出现 `VOCALOID Patcher` 即表示加载成功。

如果编辑器不在默认目录，请将文件复制到实际的 Editor 目录。

![VOCALOID Patcher Menu](./res/screenshot.png)

## 功能

### 界面翻译

编辑器资源和部分硬编码文本都可以翻译。译文保存在外置 XML 文件中。

### 钢琴窗

- 在当前钢琴窗中显示其它轨道的音符，并可跳过静音轨道。
- 在音符上显示 `C4`、`F#3` 这样的音高名。
- 圆角音符、歌词垂直居中和始终显示波形。
- 在右下角显示当前声库封面；每个声库都可以单独设置立绘、大小和透明度。
- 立绘支持静态图片、`GIF` 和 `MP4` 等视频。
- 为触控板补充钢琴窗横向滚动。

![Show notes from other tracks](./res/show_other_notes.png)
![Show note pitch](./res/show_note_pitch.png)
![Rounded notes](./res/rounded_notes.png)
![Centered lyrics](./res/centered_notes_b.png)
![Always show waveform](./res/always_show_wave.png)

### 批处理工具

- 批量替换选中音符的歌词。
- 按当前网格量化音符时值。
- 按调式生成上、下三度至八度的和声，可写入现有轨道或新轨道。
- 钢琴窗右键菜单提供八分音符、十六分音符的 Swing 和 Shuffle 预设。

### 项目格式转换

除了编辑器原生支持的格式，补丁还可以直接导入和导出：

`USTX`、`UST`、`SVP`、`S5P`、`VSQ`、`VSQX`、`VPR`、`CCS`、`TSSln`、`ACEP`、`VVProj`、`PPSF`、`DV`、`TLPX`、`TLP`、`SVIP`、`DS`、`AISP`、`Y77`、`VOG`、`MusicXML`、`OpenSVIP JSON` 和 `UFData`。

也可以将歌词导出为 `LRC`、`SRT` 或 `ASS`。

这部分代码移植自 [`LibreSVIP`](https://github.com/SoulMelody/LibreSVIP)

导入含有中文字符的工程时，也可以自动将中文字符转换为拼音。

不同编辑器的数据模型并不完全相同，转换后仍建议检查音高曲线、音素、换气和伴奏位置之类的东西。PPSF 目前支持新版 ZIP/JSON 工程，不支持旧版二进制 PPSF。

### 其它

- 按设定间隔自动保存当前工程。
- 在编辑器中显示实时频谱；使用 ASIO 时频谱可能无法正常工作。

## 调试

启动 VOCALOID 6 时按住左 `Shift`，补丁会额外打开一个控制台并输出调试日志。反馈启动失败、转换异常或兼容性问题时，请一并附上相关日志。

## 开发

需要在默认目录 `C:\Program Files\VOCALOID6\Editor` 安装一份较新的 VOCALOID 6。项目会同时构建 `.NET 6` 和 `.NET 8` 两个目标：

```powershell
dotnet build VOCALOIDPatcher\VOCALOIDPatcher.csproj -c Release
```

Release 构建会将依赖合并到：

```text
VOCALOIDPatcher\bin\Release\net8.0-windows\out\Microsoft.Xaml.Behaviors.dll
```

开发时可以用 `mklink` 把该 DLL 链接到编辑器目录，省去每次构建后手动复制。

### 更新翻译

运行 `ResourceTranslationComparer` 可以检查：

- 新增但尚未翻译的资源条目；
- 编辑器没有实际使用的资源；
- 需要加入 `HardcodedPropertyMap.xml` 的硬编码文本；
- 多余或已经失效的翻译键。

使用 `--write-map` 会另外生成整理后的 `HardcodedPropertyMap.generated.xml`。
