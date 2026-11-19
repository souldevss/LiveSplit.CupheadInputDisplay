using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace CupheadInputDisplay {
    // Premultiplied 32-bit DIB, presented with ULW_ALPHA; no color key is used.
    public sealed class LayeredSurface : IDisposable {
        IntPtr dc,handle,previous;public Bitmap Bitmap {get;private set;}
        [StructLayout(LayoutKind.Sequential)]struct Point {public int X,Y;public Point(int x,int y){X=x;Y=y;}}
        [StructLayout(LayoutKind.Sequential)]struct Size {public int Width,Height;public Size(int width,int height){Width=width;Height=height;}}
        [StructLayout(LayoutKind.Sequential)]struct Header {public uint Size;public int Width,Height;public ushort Planes,BitCount;public uint Compression,SizeImage;public int XPels,YPels;public uint Used,Important;}
        [StructLayout(LayoutKind.Sequential)]struct Info {public Header Header;public uint Colors;}
        [StructLayout(LayoutKind.Sequential,Pack=1)]struct Blend {public byte Operation,Flags,Alpha,Format;}
        [DllImport("gdi32.dll",SetLastError=true)]static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll",SetLastError=true)]static extern IntPtr CreateDIBSection(IntPtr dc,ref Info info,uint usage,out IntPtr bits,IntPtr section,uint offset);
        [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
        [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll",SetLastError=true)]static extern bool UpdateLayeredWindow(IntPtr window,IntPtr destinationDC,ref Point destination,ref Size size,IntPtr sourceDC,ref Point source,uint key,ref Blend blend,uint flags);
        public void Present(IntPtr window,Rectangle bounds,Component owner){
            if(Bitmap==null||Bitmap.Width!=bounds.Width||Bitmap.Height!=bounds.Height){Dispose();dc=CreateCompatibleDC(IntPtr.Zero);if(dc==IntPtr.Zero)throw new Win32Exception();var info=new Info{Header=new Header{Size=(uint)Marshal.SizeOf(typeof(Header)),Width=bounds.Width,Height=-bounds.Height,Planes=1,BitCount=32}};IntPtr bits;handle=CreateDIBSection(dc,ref info,0,out bits,IntPtr.Zero,0);if(handle==IntPtr.Zero)throw new Win32Exception();previous=SelectObject(dc,handle);Bitmap=new Bitmap(bounds.Width,bounds.Height,bounds.Width*4,PixelFormat.Format32bppPArgb,bits);}
            using(var graphics=Graphics.FromImage(Bitmap)){graphics.Clear(Color.Transparent);owner.Render(graphics,Bitmap.Width,Bitmap.Height,owner.Bounds(),owner.Pressed,false);graphics.Flush();}
            var target=new Point(bounds.X,bounds.Y);var size=new Size(bounds.Width,bounds.Height);var source=new Point(0,0);var blend=new Blend{Alpha=255,Format=1};if(!UpdateLayeredWindow(window,IntPtr.Zero,ref target,ref size,dc,ref source,0,ref blend,2))throw new Win32Exception();
        }
        public void Dispose(){if(Bitmap!=null){Bitmap.Dispose();Bitmap=null;}if(dc!=IntPtr.Zero&&previous!=IntPtr.Zero)SelectObject(dc,previous);if(handle!=IntPtr.Zero)DeleteObject(handle);if(dc!=IntPtr.Zero)DeleteDC(dc);dc=handle=previous=IntPtr.Zero;}
    }
}
