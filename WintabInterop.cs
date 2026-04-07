using System;
using System.Runtime.InteropServices;

namespace TabletMidiPad;

internal static class WintabInterop
{
    public const int WT_DEFBASE = 0x7FF0;
    public const int WT_PACKET = 0;
    public const int WT_CTXOPEN = 1;
    public const int WT_CTXCLOSE = 2;
    public const int WT_CTXUPDATE = 3;
    public const int WT_CTXOVERLAP = 4;
    public const int WT_PROXIMITY = 5;
    public const int WT_INFOCHANGE = 6;
    public const int WT_PACKETEXT = 8;
    private const uint WTI_INTERFACE = 1;
    private const uint WTI_DEFCONTEXT = 3;
    private const uint WTI_DEFSYSCTX = 4;
    private const uint WTI_DDCTXS = 400;
    private const uint WTI_DEVICES = 100;
    private const uint WTI_EXTENSIONS = 300;
    private const uint IFC_NDEVICES = 4;
    private const uint DVC_HARDWARE = 2;
    private const uint DVC_X = 12;
    private const uint DVC_Y = 13;
    private const uint DVC_NPRESSURE = 15;

    private const uint EXT_TAG = 2;
    private const uint EXT_MASK = 3;

    public const uint WTX_EXPKEYS2 = 8;
    public const uint WTX_TOUCHRING = 7;
    public const uint WTX_TOUCHSTRIP = 6;

    public const ushort TABLET_PROPERTY_CONTROLCOUNT = 0;
    public const ushort TABLET_PROPERTY_FUNCCOUNT = 1;
    public const ushort TABLET_PROPERTY_AVAILABLE = 2;
    public const ushort TABLET_PROPERTY_OVERRIDE = 5;
    public const ushort TABLET_PROPERTY_OVERRIDE_NAME = 6;
    public const ushort TABLET_PROPERTY_LOCATION = 11;
    public const uint HWC_INTEGRATED = 0x0001;

    public const uint CXO_SYSTEM = 0x0001;
    public const uint CXO_MESSAGES = 0x0004;

    public const uint CXL_INSIZE = 0x0001;
    public const uint CXL_INASPECT = 0x0002;
    public const uint CXL_SENSITIVITY = 0x0004;
    public const uint CXL_MARGIN = 0x0008;
    public const uint CXL_SYSOUT = 0x0010;

    public const uint PK_CONTEXT = 0x0001;
    public const uint PK_STATUS = 0x0002;
    public const uint PK_TIME = 0x0004;
    public const uint PK_CHANGED = 0x0008;
    public const uint PK_SERIAL_NUMBER = 0x0010;
    public const uint PK_CURSOR = 0x0020;
    public const uint PK_BUTTONS = 0x0040;
    public const uint PK_X = 0x0080;
    public const uint PK_Y = 0x0100;
    public const uint PK_Z = 0x0200;
    public const uint PK_NORMAL_PRESSURE = 0x0400;
    public const uint PK_TANGENT_PRESSURE = 0x0800;
    public const uint PK_ORIENTATION = 0x1000;
    public const uint PK_ALL = 0x1FFF;

    [StructLayout(LayoutKind.Sequential)]
    public struct Fix32
    {
        public uint Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Axis
    {
        public int Min;
        public int Max;
        public uint Units;
        public Fix32 Resolution;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct LogContext
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)]
        public string Name;
        public uint Options;
        public uint Status;
        public uint Locks;
        public uint MsgBase;
        public uint Device;
        public uint PktRate;
        public uint PktData;
        public uint PktMode;
        public uint MoveMask;
        public uint BtnDnMask;
        public uint BtnUpMask;
        public int InOrgX;
        public int InOrgY;
        public int InOrgZ;
        public int InExtX;
        public int InExtY;
        public int InExtZ;
        public int OutOrgX;
        public int OutOrgY;
        public int OutOrgZ;
        public int OutExtX;
        public int OutExtY;
        public int OutExtZ;
        public Fix32 SensX;
        public Fix32 SensY;
        public Fix32 SensZ;
        [MarshalAs(UnmanagedType.Bool)]
        public bool SysMode;
        public int SysOrgX;
        public int SysOrgY;
        public int SysExtX;
        public int SysExtY;
        public Fix32 SysSensX;
        public Fix32 SysSensY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Packet
    {
        public uint Buttons;
        public int X;
        public int Y;
        public uint NormalPressure;
    }

    public static bool IsAvailable()
    {
        try
        {
            return WTInfoA(0, 0, IntPtr.Zero) > 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    public static LogContext GetDefaultContext()
    {
        return GetLogContext(WTI_DEFCONTEXT);
    }

    public static LogContext GetDefaultDigitizingContext()
    {
        return GetLogContext(WTI_DEFCONTEXT);
    }

    public static LogContext GetDefaultSystemContext()
    {
        return GetLogContext(WTI_DEFSYSCTX);
    }

    public static LogContext GetDigitizerContext(int deviceIndex)
    {
        return GetLogContext(unchecked(WTI_DDCTXS + (uint)deviceIndex));
    }

    public static int GetAttachedDeviceCount()
    {
        return GetUint(WTI_INTERFACE, IFC_NDEVICES);
    }

    public static bool IsIntegratedTablet(int deviceIndex)
    {
        uint hardware = unchecked((uint)GetUint(GetDeviceCategory(deviceIndex), DVC_HARDWARE));
        return (hardware & HWC_INTEGRATED) != 0;
    }

    public static Axis GetPressureAxis()
    {
        return GetAxis(0, DVC_NPRESSURE);
    }

    public static Axis GetXAxis()
    {
        return GetAxis(0, DVC_X);
    }

    public static Axis GetYAxis()
    {
        return GetAxis(0, DVC_Y);
    }

    public static Axis GetXAxis(int deviceIndex)
    {
        return GetAxis(deviceIndex, DVC_X);
    }

    public static Axis GetYAxis(int deviceIndex)
    {
        return GetAxis(deviceIndex, DVC_Y);
    }

    public static Axis GetPressureAxis(int deviceIndex)
    {
        return GetAxis(deviceIndex, DVC_NPRESSURE);
    }

    private static LogContext GetLogContext(uint category)
    {
        int size = Marshal.SizeOf<LogContext>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            int returned = (int)WTInfoA(category, 0, buffer);
            if (returned <= 0)
            {
                throw new InvalidOperationException($"Wintab context category {category} is unavailable.");
            }

            return Marshal.PtrToStructure<LogContext>(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static Axis GetAxis(int deviceIndex, uint index)
    {
        int size = Marshal.SizeOf<Axis>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            int returned = (int)WTInfoA(GetDeviceCategory(deviceIndex), index, buffer);
            if (returned <= 0)
            {
                return default;
            }

            return Marshal.PtrToStructure<Axis>(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static uint GetDeviceCategory(int deviceIndex)
    {
        int category = unchecked((int)WTI_DEVICES + deviceIndex);
        return unchecked((uint)category);
    }

    private static int GetUint(uint category, uint index)
    {
        int size = sizeof(uint);
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            int returned = (int)WTInfoA(category, index, buffer);
            if (returned <= 0)
            {
                return 0;
            }

            return Marshal.ReadInt32(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("Wintab32.dll", CharSet = CharSet.Auto)]
    public static extern uint WTInfoA(uint category, uint index, IntPtr output);

    [DllImport("Wintab32.dll", CharSet = CharSet.Auto)]
    public static extern uint WTOpenA(IntPtr hwnd, ref LogContext logContext, bool enable);

    [DllImport("Wintab32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WTClose(uint context);

    [DllImport("Wintab32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WTEnable(uint context, bool enable);

    [DllImport("Wintab32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WTOverlap(uint context, bool toTop);

    [DllImport("Wintab32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WTPacket(uint context, uint packetSerialNumber, out Packet packet);

    [DllImport("Wintab32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WTPacket(uint context, uint packetSerialNumber, IntPtr buffer);

    [DllImport("Wintab32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WTExtGet(uint context, uint extensionTag, IntPtr data);

    [DllImport("Wintab32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WTExtSet(uint context, uint extensionTag, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    public struct ExtensionBase
    {
        public IntPtr Context;
        public uint Status;
        public uint Time;
        public uint SerialNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ExpKeyData
    {
        public byte Tablet;
        public byte Control;
        public byte Location;
        public byte Reserved;
        public uint State;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SliderData
    {
        public byte Tablet;
        public byte Control;
        public byte Mode;
        public byte Reserved;
        public uint Position;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PacketExt
    {
        public ExtensionBase Base;
        public ExpKeyData ExpKey;
        public SliderData TouchStrip;
        public SliderData TouchRing;
    }

    private const int ExtensionPropertyMaxDataBytes = 32;

    [StructLayout(LayoutKind.Sequential)]
    public struct ExtensionPropertyBase
    {
        public byte Version;
        public byte TabletIndex;
        public byte ControlIndex;
        public byte FunctionIndex;
        public ushort PropertyId;
        public ushort Reserved;
        public uint DataSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ExtensionProperty
    {
        public ExtensionPropertyBase Header;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = ExtensionPropertyMaxDataBytes)]
        public byte[] Data;
    }

    public static uint FindExtensionIndex(uint extensionTag)
    {
        int size = sizeof(uint);
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            for (uint i = 0; ; i++)
            {
                int returned = (int)WTInfoA(WTI_EXTENSIONS + i, EXT_TAG, buffer);
                if (returned <= 0)
                    break;
                uint tag = unchecked((uint)Marshal.ReadInt32(buffer));
                if (tag == extensionTag)
                    return i;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
        return 0xFFFFFFFF;
    }

    public static uint GetExtensionMask(uint extensionTag)
    {
        uint extIndex = FindExtensionIndex(extensionTag);
        if (extIndex == 0xFFFFFFFF)
            return 0;

        int size = sizeof(uint);
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            int returned = (int)WTInfoA(WTI_EXTENSIONS + extIndex, EXT_MASK, buffer);
            if (returned > 0)
                return unchecked((uint)Marshal.ReadInt32(buffer));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
        return 0;
    }

    public static bool GetExtensionProperty(uint context, uint extensionTag, byte tabletIndex,
        byte controlIndex, byte functionIndex, ushort propertyId, out uint result)
    {
        result = 0;
        var prop = new ExtensionProperty
        {
            Header = new ExtensionPropertyBase
            {
                Version = 0,
                TabletIndex = tabletIndex,
                ControlIndex = controlIndex,
                FunctionIndex = functionIndex,
                PropertyId = propertyId,
                Reserved = 0,
                DataSize = sizeof(uint)
            },
            Data = new byte[ExtensionPropertyMaxDataBytes]
        };

        int size = Marshal.SizeOf<ExtensionProperty>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(prop, buffer, false);
            if (!WTExtGet(context, extensionTag, buffer))
                return false;

            var ret = Marshal.PtrToStructure<ExtensionProperty>(buffer);
            result = BitConverter.ToUInt32(ret.Data, 0);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static bool SetExtensionProperty(uint context, uint extensionTag, byte tabletIndex,
        byte controlIndex, byte functionIndex, ushort propertyId, uint value)
    {
        var prop = new ExtensionProperty
        {
            Header = new ExtensionPropertyBase
            {
                Version = 0,
                TabletIndex = tabletIndex,
                ControlIndex = controlIndex,
                FunctionIndex = functionIndex,
                PropertyId = propertyId,
                Reserved = 0,
                DataSize = sizeof(uint)
            },
            Data = new byte[ExtensionPropertyMaxDataBytes]
        };

        byte[] valueBytes = BitConverter.GetBytes(value);
        Buffer.BlockCopy(valueBytes, 0, prop.Data, 0, 4);

        int size = Marshal.SizeOf<ExtensionProperty>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(prop, buffer, false);
            return WTExtSet(context, extensionTag, buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static bool SetExtensionPropertyString(uint context, uint extensionTag, byte tabletIndex,
        byte controlIndex, byte functionIndex, ushort propertyId, string value)
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value);
        var prop = new ExtensionProperty
        {
            Header = new ExtensionPropertyBase
            {
                Version = 0,
                TabletIndex = tabletIndex,
                ControlIndex = controlIndex,
                FunctionIndex = functionIndex,
                PropertyId = propertyId,
                Reserved = 0,
                DataSize = (uint)utf8.Length
            },
            Data = new byte[ExtensionPropertyMaxDataBytes]
        };

        Buffer.BlockCopy(utf8, 0, prop.Data, 0, Math.Min(utf8.Length, ExtensionPropertyMaxDataBytes));

        int size = Marshal.SizeOf<ExtensionProperty>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(prop, buffer, false);
            return WTExtSet(context, extensionTag, buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static bool GetPacketExt(uint context, uint packetId, out PacketExt packetExt)
    {
        packetExt = default;
        int size = Marshal.SizeOf<PacketExt>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!WTPacket(context, packetId, buffer))
                return false;
            packetExt = Marshal.PtrToStructure<PacketExt>(buffer);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
