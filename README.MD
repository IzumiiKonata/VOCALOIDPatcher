# VOCALOID Patcher
一个用于为 `VOCALOID 6` 编辑器提供本地化的补丁, 理论上支持所有版本的 V6 编辑器

### 翻译条目版本: `6.12.0.1`
### 已测试的编辑器版本: `6.1.0.2`, `6.6.0`, `6.11.0.1`, `6.12.0.1`

提交反馈/技术交流/单纯聊天 请加入 `422827160`  
我没有什么使用 V6 编辑器的经验, 所以可能会出现翻译不准确的情况。开个 issue 或者加群反馈给我, 然后下个版本我会修改翻译。

## 使用方法
前往 [Releases](https://github.com/IzumiiKonata/VOCALOIDPatcher/releases) 中下载最新发行版  
如果您的编辑器版本较老, 请下载 `.net 6.0` 的版本  
将压缩包中的所有文件 _(除了 README.txt)_ 复制并替换到 `C:\Program Files\VOCALOID6\Editor` 中 (如果编辑器没有安装在默认目录中可能会有问题)  
打开编辑器并打开一个工程, 如果菜单栏中出现 `VOCALOID Patcher` 选项则安装成功。  
![img](./res/screenshot.png)

## 调试
安装该补丁后, 在打开 `VOCALOID 6` 编辑器时长按 `左 Shift` 会打开控制台, `VOCALOIDPatcher` 会在其中显示一些 Debug 日志。

## 更新翻译
拉取项目, 运行 `ResourceTranslationComparer`, 控制台会输出一份比较报告:

- **[1] 没翻译的 (XAML 用到了)**: 界面真正用到、但还没翻译的 resx 条目, 优先翻; 直接把里面的 `<data>` 行复制到翻译文件即可
- **[2] 没翻译的 (XAML 没直接用)**: resx 里有、但 XAML 没直接绑定的条目, 可能是动态用的, 可以缓缓
- **[3] 待登记的写死字面量**: 写死在 XAML 里、不走资源系统的文本; 要在 `HardcodedPropertyMap.xml` 里登记 (字面量 → 自定义键), 再到翻译文件里给这个键补译文
- **[4] 多出来的翻译键 / [5] 找不到的硬编码项**: 源码里已经没有、可以删的条目 (仅供参考; 有些字面量是代码动态设的, 工具扫不到, 删之前先核对)

加 `--write-map` 参数可额外导出一份整理过的 `HardcodedPropertyMap.generated.xml` (保留现有条目 + 为新发现的字面量补占位条目, 供人工补键名/译文)。

## 开发

### 注意事项
你需要在默认目录中 `(C:\Program Files\VOCALOID6\Editor)` 安装一份最新的 VOCALOID6 编辑器  
编译完成后, 会在 `VOCALOIDPatcher\bin\Release\netX.0-windows\out` 中构建一个包含依赖的 `Microsoft.Xaml.Behaviors.dll`。

