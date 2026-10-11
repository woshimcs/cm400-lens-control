using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

/// <summary>
/// CM400（Novatek USB 0603:8612）镜头伸缩。
/// 标准 UVC 变焦不会带动机构。实际控制在厂商扩展单元的 1 字节开关上：
/// 选择子 4，节点 4，1 = 伸出，2 = 收回。
/// </summary>
static class Program
{
    const byte Extend = 1;
    const byte Retract = 2;

    static int Main(string[] args)
    {
        string command = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "status";
        byte? target = command switch
        {
            "extend" or "out" or "伸出" => Extend,
            "retract" or "in" or "收回" => Retract,
            "status" or "状态" => null,
            _ => byte.MaxValue
        };
        if (target == byte.MaxValue)
        {
            Console.Error.WriteLine("用法 / Usage: cm400-lens <extend|retract|status>");
            return 2;
        }

        IKsControl control;
        try
        {
            control = CameraControl.Open("CM400");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("没有找到 CM400 / CM400 was not found: " + ex.Message);
            return 1;
        }

        if (target == null)
        {
            Console.WriteLine(Describe(CameraControl.Get(control)));
            return 0;
        }

        int hr = CameraControl.Set(control, target.Value);
        if (hr != 0)
        {
            Console.Error.WriteLine("写入失败 / Write failed " + Hex(hr));
            return 1;
        }

        byte? current = CameraControl.Get(control);
        Console.WriteLine(Describe(current));
        if (current != target.Value)
        {
            Console.Error.WriteLine("摄像头没有保持目标值 / Camera did not keep target " + target.Value);
            return 1;
        }
        return 0;
    }

    static string Describe(byte? value)
    {
        if (value == null) return "读不到开关 4 / Cannot read switch 4";
        string name = value.Value switch
        {
            Extend => "伸出 / extended",
            Retract => "收回 / retracted",
            _ => "未知 / unknown"
        };
        return "开关 4 / Switch 4 = " + value.Value + "（" + name + "）";
    }

    static string Hex(int hr) => "0x" + unchecked((uint)hr).ToString("X8");
}

static class CameraControl
{
    const uint GetFlags = 0x10000001; // KSPROPERTY_TYPE_GET | KSPROPERTY_TYPE_TOPOLOGY
    const uint SetFlags = 0x10000002; // KSPROPERTY_TYPE_SET | KSPROPERTY_TYPE_TOPOLOGY
    public static IKsControl Open(string namePart)
    {
        Guid clsid = new Guid("62BE5D10-60EB-11d0-BD3B-00A0C911CE86");
        Guid category = new Guid("860BB310-5D01-11d0-BD3B-00A0C911CE86");
        var enumerator = (ICreateDevEnum)Activator.CreateInstance(Type.GetTypeFromCLSID(clsid));
        IEnumMoniker monikers;
        int hr = enumerator.CreateClassEnumerator(ref category, out monikers, 0);
        if (hr != 0 || monikers == null) throw new InvalidOperationException(Hex(hr));

        IMoniker[] fetched = new IMoniker[1];
        while (monikers.Next(1, fetched, IntPtr.Zero) == 0)
        {
            Guid bagId = typeof(IPropertyBag).GUID;
            object bagObject;
            fetched[0].BindToStorage(null, null, ref bagId, out bagObject);
            object nameObject;
            ((IPropertyBag)bagObject).Read("FriendlyName", out nameObject, IntPtr.Zero);
            if (Convert.ToString(nameObject).IndexOf(namePart, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            Guid filterId = new Guid("56A86895-0AD4-11CE-B03A-0020AF0BA770");
            object filter;
            fetched[0].BindToObject(null, null, ref filterId, out filter);
            IntPtr unknown = Marshal.GetIUnknownForObject(filter);
            Guid controlId = typeof(IKsControl).GUID;
            IntPtr controlPointer;
            Marshal.QueryInterface(unknown, ref controlId, out controlPointer);
            return (IKsControl)Marshal.GetObjectForIUnknown(controlPointer);
        }
        throw new InvalidOperationException("设备不在视频采集列表里 / Device is not in the video capture list");
    }

    public static byte? Get(IKsControl control)
    {
        IntPtr property = Header(GetFlags);
        IntPtr data = Marshal.AllocHGlobal(8);
        try
        {
            Zero(data, 8);
            uint returned;
            int hr = control.KsProperty(property, 32, data, 8, out returned);
            if (hr != 0) return null;
            return Marshal.ReadByte(data);
        }
        finally
        {
            Marshal.FreeHGlobal(property);
            Marshal.FreeHGlobal(data);
        }
    }

    public static int Set(IKsControl control, byte value)
    {
        IntPtr property = Header(SetFlags);
        IntPtr data = Marshal.AllocHGlobal(8);
        try
        {
            Zero(data, 8);
            Marshal.WriteByte(data, value);
            uint returned;
            return control.KsProperty(property, 32, data, 1, out returned);
        }
        finally
        {
            Marshal.FreeHGlobal(property);
            Marshal.FreeHGlobal(data);
        }
    }

    static IntPtr Header(uint flags)
    {
        IntPtr property = Marshal.AllocHGlobal(32);
        Zero(property, 32);
        Marshal.StructureToPtr(new Guid("C987A729-59D3-4569-8467-FF0849FC19E8"), property, false);
        Marshal.WriteInt32(property, 16, 4);
        Marshal.WriteInt32(property, 20, (int)flags);
        Marshal.WriteInt32(property, 24, 4);
        return property;
    }

    static void Zero(IntPtr pointer, int length)
    {
        for (int i = 0; i < length; i++) Marshal.WriteByte(pointer, i, 0);
    }

    static string Hex(int hr) => "0x" + unchecked((uint)hr).ToString("X8");
}

[ComImport, Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ICreateDevEnum
{
    [PreserveSig]
    int CreateClassEnumerator(ref Guid deviceClass, out IEnumMoniker moniker, int flags);
}

[ComImport, Guid("55272A00-42CB-11CE-8135-00AA004BB851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPropertyBag
{
    [PreserveSig]
    int Read([MarshalAs(UnmanagedType.LPWStr)] string name, out object value, IntPtr errorLog);
    [PreserveSig]
    int Write([MarshalAs(UnmanagedType.LPWStr)] string name, ref object value);
}

[ComImport, Guid("28F54685-06FD-11D2-B27A-00A0C9223196"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IKsControl
{
    [PreserveSig]
    int KsProperty(IntPtr property, uint propertyLength, IntPtr propertyData, uint dataLength, out uint bytesReturned);
    [PreserveSig]
    int KsMethod(IntPtr method, uint methodLength, IntPtr methodData, uint dataLength, out uint bytesReturned);
    [PreserveSig]
    int KsEvent(IntPtr ksEvent, uint eventLength, IntPtr eventData, uint dataLength, out uint bytesReturned);
}
