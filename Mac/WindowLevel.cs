using Avalonia.Controls;
using System.Runtime.InteropServices;

namespace Momonga.Mac;

internal static class WindowLevel
{
    public static void IgnoreMouse(Window window)
    {
        if(!OperatingSystem.IsMacOS())return;
        var handle=window.TryGetPlatformHandle();
        if(handle?.HandleDescriptor=="NSWindow")SendLong(handle.Handle,sel_registerName("setIgnoresMouseEvents:"),1);
    }
    public static void Maintain(Window window)
    {
        if(!window.IsVisible)return;
        window.Topmost=true;
        if(!OperatingSystem.IsMacOS())return;
        var handle=window.TryGetPlatformHandle();
        if(handle?.HandleDescriptor!="NSWindow")return;
        SendLong(handle.Handle,sel_registerName("setLevel:"),25);
        SendLong(handle.Handle,sel_registerName("setCollectionBehavior:"),1|16|256);
        Send(handle.Handle,sel_registerName("orderFrontRegardless"));
    }
    [DllImport("/usr/lib/libobjc.A.dylib")] private static extern IntPtr sel_registerName(string name);
    [DllImport("/usr/lib/libobjc.A.dylib",EntryPoint="objc_msgSend")] private static extern void SendLong(IntPtr target,IntPtr selector,nint value);
    [DllImport("/usr/lib/libobjc.A.dylib",EntryPoint="objc_msgSend")] private static extern void Send(IntPtr target,IntPtr selector);
}
