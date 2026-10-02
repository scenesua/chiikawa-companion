using System.Runtime.InteropServices;
using Avalonia;

namespace Momonga.Mac;

// Listen-only event tap: counts event types without inspecting keys or text.
public sealed class MacActivity : IDisposable
{
    private const string CoreGraphics="/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundation="/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private delegate IntPtr Callback(IntPtr proxy,uint type,IntPtr evt,IntPtr unused);
    private readonly Callback callback;
    private IntPtr tap,source;
    private int keys,clicks;
    public bool Enabled=>tap!=IntPtr.Zero;
    public MacActivity(bool enable=true)
    {
        callback=(_,type,evt,_)=>{ if(type==10) Interlocked.Increment(ref keys); else if(type is 1 or 3 or 25) Interlocked.Increment(ref clicks); else if(type is 0xfffffffe or 0xffffffff) CGEventTapEnable(tap,true); return evt; };
        if(enable && OperatingSystem.IsMacOS() && CGPreflightListenEventAccess()) Start();
    }
    public bool RequestPermission()
    {
        if(!OperatingSystem.IsMacOS()) return false;
        if(!CGRequestListenEventAccess()) return false;
        Start(); return Enabled;
    }
    private void Start()
    {
        if(Enabled) return;
        tap=CGEventTapCreate(1,0,1,(1UL<<10)|(1UL<<1)|(1UL<<3)|(1UL<<25),callback,IntPtr.Zero);
        if(tap==IntPtr.Zero) return;
        source=CFMachPortCreateRunLoopSource(IntPtr.Zero,tap,0);
        using var lib=new NativeLibraryHandle(CoreFoundation);
        var modes=Marshal.ReadIntPtr(NativeLibrary.GetExport(lib.Handle,"kCFRunLoopCommonModes"));
        CFRunLoopAddSource(CFRunLoopGetMain(),source,modes); CGEventTapEnable(tap,true);
    }
    public (int Keys,int Clicks) Drain()=> (Interlocked.Exchange(ref keys,0),Interlocked.Exchange(ref clicks,0));
    public static Point? Pointer()
    {
        if(!OperatingSystem.IsMacOS()) return null;
        var evt=CGEventCreate(IntPtr.Zero); if(evt==IntPtr.Zero) return null;
        var p=CGEventGetLocation(evt); CFRelease(evt); return new Point(p.X,p.Y);
    }
    public void Dispose()
    {
        if(source!=IntPtr.Zero) { CFRunLoopSourceInvalidate(source); CFRelease(source); source=IntPtr.Zero; }
        if(tap!=IntPtr.Zero) { CGEventTapEnable(tap,false); CFMachPortInvalidate(tap); CFRelease(tap); tap=IntPtr.Zero; }
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public double X,Y; }
    [DllImport(CoreGraphics)] private static extern IntPtr CGEventTapCreate(uint tap,uint place,uint options,ulong mask,Callback callback,IntPtr user);
    [DllImport(CoreGraphics)] private static extern void CGEventTapEnable(IntPtr tap,[MarshalAs(UnmanagedType.I1)]bool enabled);
    [DllImport(CoreGraphics)] [return:MarshalAs(UnmanagedType.I1)] private static extern bool CGPreflightListenEventAccess();
    [DllImport(CoreGraphics)] [return:MarshalAs(UnmanagedType.I1)] private static extern bool CGRequestListenEventAccess();
    [DllImport(CoreGraphics)] private static extern IntPtr CGEventCreate(IntPtr source);
    [DllImport(CoreGraphics)] private static extern NativePoint CGEventGetLocation(IntPtr evt);
    [DllImport(CoreFoundation)] private static extern IntPtr CFRunLoopGetMain();
    [DllImport(CoreFoundation)] private static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator,IntPtr port,nint order);
    [DllImport(CoreFoundation)] private static extern void CFRunLoopAddSource(IntPtr loop,IntPtr source,IntPtr mode);
    [DllImport(CoreFoundation)] private static extern void CFRunLoopSourceInvalidate(IntPtr source);
    [DllImport(CoreFoundation)] private static extern void CFMachPortInvalidate(IntPtr port);
    [DllImport(CoreFoundation)] private static extern void CFRelease(IntPtr value);
    private sealed class NativeLibraryHandle(string path):IDisposable { public IntPtr Handle {get;}=NativeLibrary.Load(path); public void Dispose()=>NativeLibrary.Free(Handle); }
}
