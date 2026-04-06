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
    private const uint WTI_INTERFACE = 1;
    private const uint WTI_DEFCONTEXT = 3;
    private const uint WTI_DEFSYSCTX = 4;
    private const uint WTI_DDCTXS = 400;
    private const uint WTI_DEVICES = 100;
    private const uint IFC_NDEVICES = 4;
    private const uint DVC_HARDWARE = 2;
    private const uint DVC_X = 12;
    private const uint DVC_Y = 13;
    private const uint DVC_NPRESSURE = 15;
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
}
