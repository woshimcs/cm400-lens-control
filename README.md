# CM400 镜头伸缩控制

Windows 上控制 Novatek CM400 USB 摄像头的伸缩机构。实测：写 `1` 镜头伸出，写 `2` 镜头收回。

标准 UVC 变焦（0–10）只能改一个数值，镜头不动。伸缩不在变焦上，而在厂商扩展单元的一个 1 字节开关上。

## 设备

| 项 | 值 |
| --- | --- |
| 名称 | `CM400` |
| USB | `0603:8612`（Novatek，UVC 摄像头模式） |
| 扩展单元 GUID | `{C987A729-59D3-4569-8467-FF0849FC19E8}` |
| 拓扑节点 | `4`（`KSNODETYPE_DEV_SPECIFIC`） |
| 控制选择子 | `4` |
| 伸出 | `1` |
| 收回 | `2` |

`0` 可以读到，但写进去不会保持。出图不需要额外驱动，Windows 自带 USB Video 即可。

## 用法

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download)。摄像头要出现在系统的视频设备里，名称包含 `CM400`。

```powershell
dotnet run --project Cm400Lens.csproj -- extend
dotnet run --project Cm400Lens.csproj -- retract
dotnet run --project Cm400Lens.csproj -- status
```

发布成单个程序：

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

## 控制方式

通过 DirectShow 设备枚举找到 `CM400`，再对滤镜调用 `IKsControl::KsProperty`。

属性头 32 字节：

| 偏移 | 内容 |
| --- | --- |
| 0 | 扩展单元 GUID |
| 16 | 选择子 `4` |
| 20 | `KSPROPERTY_TYPE_GET (1)` 或 `SET (2)`，并加上 `KSPROPERTY_TYPE_TOPOLOGY (0x10000000)` |
| 24 | 节点号 `4` |

数据是单独的 1 字节，不和属性头放在同一块缓冲区里。不带 `TOPOLOGY` 标志时，这个选择子会返回“找不到”。

同一次摸索里，选择子 5 只能读、写入会被拒绝，和伸缩无关。变焦属性 `KSPROPERTY_CAMERACONTROL_ZOOM` 接受 0 到 10，同样不带动机构。

## 许可

MIT
