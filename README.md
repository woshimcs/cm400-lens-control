# CM400 镜头伸缩控制 / CM400 Lens Extend and Retract

在 Windows 上控制 Novatek CM400 USB 摄像头的伸缩机构。实测：写 `1` 镜头伸出，写 `2` 镜头收回。

Control the telescoping mechanism of a Novatek CM400 USB camera on Windows. Verified on the device: writing `1` extends the lens, writing `2` retracts it.

标准 UVC 变焦（0–10）只能改一个数值，镜头不动。伸缩不在变焦上，而在厂商扩展单元的一个 1 字节开关上。

Standard UVC zoom (0–10) only stores a number. The barrel does not move. The mechanism is a 1-byte switch on the vendor extension unit, not the zoom control.

## 设备 / Device

| 项 / Item | 值 / Value |
| --- | --- |
| 名称 / Name | `CM400` |
| USB | `0603:8612`（Novatek，UVC 摄像头模式 / UVC webcam mode） |
| 扩展单元 GUID / Extension unit GUID | `{C987A729-59D3-4569-8467-FF0849FC19E8}` |
| 拓扑节点 / Topology node | `4`（`KSNODETYPE_DEV_SPECIFIC`） |
| 控制选择子 / Control selector | `4` |
| 伸出 / Extend | `1` |
| 收回 / Retract | `2` |

`0` 可以读到，但写进去不会保持。出图不需要额外驱动，Windows 自带 USB Video 即可。

`0` can be read, but a write does not stick. Preview uses the in-box Windows USB Video driver. No extra camera driver is required.

## 用法 / Usage

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download)。摄像头要出现在系统的视频设备里，名称包含 `CM400`。

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download). The camera must show up as a video capture device whose name contains `CM400`.

```powershell
dotnet run --project Cm400Lens.csproj -- extend
dotnet run --project Cm400Lens.csproj -- retract
dotnet run --project Cm400Lens.csproj -- status
```

发布成单个程序 / Publish a single program:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

## 控制方式 / How the control works

通过 DirectShow 设备枚举找到 `CM400`，再对滤镜调用 `IKsControl::KsProperty`。

Find `CM400` with DirectShow device enumeration, then call `IKsControl::KsProperty` on the capture filter.

属性头 32 字节 / 32-byte property header:

| 偏移 / Offset | 内容 / Content |
| --- | --- |
| 0 | 扩展单元 GUID / Extension unit GUID |
| 16 | 选择子 `4` / Selector `4` |
| 20 | `KSPROPERTY_TYPE_GET (1)` 或 `SET (2)`，并加上 `KSPROPERTY_TYPE_TOPOLOGY (0x10000000)` / `GET (1)` or `SET (2)`, OR'ed with `KSPROPERTY_TYPE_TOPOLOGY (0x10000000)` |
| 24 | 节点号 `4` / Node id `4` |

数据是单独的 1 字节，不和属性头放在同一块缓冲区里。不带 `TOPOLOGY` 标志时，这个选择子会返回“找不到”。

The payload is a separate 1-byte buffer. It is not packed into the property header. Without the `TOPOLOGY` flag, this selector returns “not found”.

同一次摸索里，选择子 5 只能读、写入会被拒绝，和伸缩无关。变焦属性 `KSPROPERTY_CAMERACONTROL_ZOOM` 接受 0 到 10，同样不带动机构。

Selector 5 can be read and rejects writes. It does not move the lens. `KSPROPERTY_CAMERACONTROL_ZOOM` accepts 0 through 10 and does not move the mechanism either.

## 许可 / License

MIT
