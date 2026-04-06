using System;
using System.Runtime.InteropServices;

namespace TabletMidiPad;

internal static class WintabInterop
{
    public const int WT_PACKET = 0x7FF0;
    private const uint WTI_DEFCONTEXT = 3;
    private const uint WTI_DEVICES = 100;
    private const uint DVC_NPRESSURE = 15;

    public const uint CXO_SYSTEM = 0x0001;
    public const uint CXO_MESSAGES = 0x0004;

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
    public struct Orientation
    {
        public int Azimuth;
        public int Altitude;
        public int Twist;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Packet
    {
        public uint Context;
        public uint Status;
        public uint Time;
        public uint Changed;
        public uint SerialNumber;
        public uint Cursor;
        public uint Buttons;
        public int X;
        public int Y;
        public int Z;
        public uint NormalPressure;
        public uint TangentPressure;
        public Orientation Orientation;
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
        int size = Marshal.SizeOf<LogContext>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            int returned = (int)WTInfoA(WTI_DEFCONTEXT, 0, buffer);
            if (returned <= 0)
            {
                throw new InvalidOperationException("Wintab default context is unavailable.");
            }

            return Marshal.PtrToStructure<LogContext>(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static Axis GetPressureAxis()
    {
        int size = Marshal.SizeOf<Axis>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            int returned = (int)WTInfoA(WTI_DEVICES, DVC_NPRESSURE, buffer);
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
