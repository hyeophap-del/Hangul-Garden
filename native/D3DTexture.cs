using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Valve.VR;

namespace HangeulVR {
// Own one fixed-size D3D11 texture for the lifetime of the overlay. No image reloads.
public sealed class D3DTexture : IDisposable {
    IntPtr device,context,texture;
    readonly int width,height;
    UpdateSubresource update;Flush flush;
    [StructLayout(LayoutKind.Sequential)]struct TextureDesc {
        public uint Width,Height,MipLevels,ArraySize,Format,SampleCount,SampleQuality,Usage,BindFlags,CPUAccessFlags,MiscFlags;
    }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int EnumAdapters(IntPtr self,uint index,out IntPtr adapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int CreateTexture2D(IntPtr self,ref TextureDesc desc,IntPtr initial,out IntPtr texture);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate void UpdateSubresource(IntPtr self,IntPtr destination,uint index,IntPtr box,IntPtr source,uint rowPitch,uint depthPitch);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate void Flush(IntPtr self);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("dxgi.dll",CallingConvention=CallingConvention.StdCall)]static extern int CreateDXGIFactory(ref Guid iid,out IntPtr factory);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("d3d11.dll",CallingConvention=CallingConvention.StdCall)]static extern int D3D11CreateDevice(IntPtr adapter,uint type,IntPtr software,uint flags,IntPtr levels,uint count,uint sdk,out IntPtr device,out uint level,out IntPtr context);
    static T Method<T>(IntPtr obj,int slot) where T:class {
        return Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj),slot*IntPtr.Size),typeof(T)) as T;
    }
    public D3DTexture(int w,int h,int adapterIndex){
        width=w;height=h;IntPtr factory=IntPtr.Zero,adapter=IntPtr.Zero;
        try {
            var iid=new Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369"); // IDXGIFactory
            Marshal.ThrowExceptionForHR(CreateDXGIFactory(ref iid,out factory));
            Marshal.ThrowExceptionForHR(Method<EnumAdapters>(factory,7)(factory,(uint)Math.Max(0,adapterIndex),out adapter));
            uint level;
            Marshal.ThrowExceptionForHR(D3D11CreateDevice(adapter,0,IntPtr.Zero,0x20,IntPtr.Zero,0,7,out device,out level,out context));
            var desc=new TextureDesc{Width=(uint)w,Height=(uint)h,MipLevels=1,ArraySize=1,Format=87,SampleCount=1,Usage=0,BindFlags=0x28,MiscFlags=2}; // BGRA8, shader resource + render target, shared
            Marshal.ThrowExceptionForHR(Method<CreateTexture2D>(device,5)(device,ref desc,IntPtr.Zero,out texture));
            update=Method<UpdateSubresource>(context,48);flush=Method<Flush>(context,111);
        } catch {Dispose();throw;}
        finally {if(adapter!=IntPtr.Zero)Marshal.Release(adapter);if(factory!=IntPtr.Zero)Marshal.Release(factory);}
    }
    public IntPtr Handle {get{return texture;}}
    public EVROverlayError Submit(CVROverlay overlay,ulong handle,Bitmap bitmap){
        if(bitmap.Width!=width||bitmap.Height!=height)throw new ArgumentException("Texture size changed.");
        var data=bitmap.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try {
            if(data.Stride<=0)throw new InvalidOperationException("Expected a top-down bitmap.");
            update(context,texture,0,IntPtr.Zero,data.Scan0,(uint)data.Stride,0);
            flush(context);
        } finally {bitmap.UnlockBits(data);}
        var native=new Texture_t{handle=texture,eType=ETextureType.DirectX,eColorSpace=EColorSpace.Gamma};
        return overlay.SetOverlayTexture(handle,ref native);
    }
    public void Dispose(){
        if(texture!=IntPtr.Zero){Marshal.Release(texture);texture=IntPtr.Zero;}
        if(context!=IntPtr.Zero){Marshal.Release(context);context=IntPtr.Zero;}
        if(device!=IntPtr.Zero){Marshal.Release(device);device=IntPtr.Zero;}
    }
}
}
