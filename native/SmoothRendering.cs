using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;

namespace HangeulVR {
public sealed class KeyboardRenderer : IDisposable {
    Bitmap background;
    readonly Bitmap frame=new Bitmap(Keyboard.W,Keyboard.H,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
    readonly byte[] pixels=new byte[Keyboard.W*Keyboard.H*4];
    int revision=-1,left=-2,right=-2,caret=-1,selectionStart=-1,selectionLength=-1;string text,status;
    public int Rebuilds {get;private set;}
    // Borrowed bitmap: valid until the next Draw or Dispose. Caller must not dispose it.
    public Bitmap Draw(Keyboard keyboard,int l,int r,PointF? lp,PointF? rp){
        string current=keyboard.Input.Text;
        if(background==null||revision!=keyboard.LayoutRevision||text!=current||status!=keyboard.Status||left!=l||right!=r||caret!=keyboard.Input.Caret||selectionStart!=keyboard.Input.SelectionStart||selectionLength!=keyboard.Input.SelectionLength){
            if(background!=null)background.Dispose();
            background=keyboard.Render(l,r,null,null);revision=keyboard.LayoutRevision;text=current;status=keyboard.Status;left=l;right=r;caret=keyboard.Input.Caret;selectionStart=keyboard.Input.SelectionStart;selectionLength=keyboard.Input.SelectionLength;Rebuilds++;
        }
        // Copy straight-alpha pixels exactly; DrawImage can round translucent panel edges.
        var bounds=new Rectangle(0,0,Keyboard.W,Keyboard.H);
        var source=background.LockBits(bounds,System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try{
            var target=frame.LockBits(bounds,System.Drawing.Imaging.ImageLockMode.WriteOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try{
                for(int y=0;y<Keyboard.H;y++){
                    Marshal.Copy(IntPtr.Add(source.Scan0,y*source.Stride),pixels,y*Keyboard.W*4,Keyboard.W*4);
                    Marshal.Copy(pixels,y*Keyboard.W*4,IntPtr.Add(target.Scan0,y*target.Stride),Keyboard.W*4);
                }
            }finally{frame.UnlockBits(target);}
        }finally{background.UnlockBits(source);}
        using(var g=Graphics.FromImage(frame)){
            g.CompositingMode=CompositingMode.SourceOver;g.SmoothingMode=SmoothingMode.AntiAlias;
            if(lp.HasValue)Keyboard.Dot(g,lp.Value,Color.Aquamarine);
            if(rp.HasValue)Keyboard.Dot(g,rp.Value,Color.FromArgb(255,205,133));
        }
        return frame;
    }
    public void Dispose(){if(background!=null){background.Dispose();background=null;}frame.Dispose();}
}
public sealed class FrameWaiter : IDisposable {
    IntPtr timer;
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateWaitableTimerExW(IntPtr attributes,string name,uint flags,uint access);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool SetWaitableTimer(IntPtr timer,ref long due,int period,IntPtr routine,IntPtr arg,bool resume);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll")]static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll")][return:MarshalAs(UnmanagedType.Bool)]static extern bool CloseHandle(IntPtr handle);
    public FrameWaiter(){timer=CreateWaitableTimerExW(IntPtr.Zero,null,2,0x100002);if(timer==IntPtr.Zero)timer=CreateWaitableTimerExW(IntPtr.Zero,null,0,0x100002);}
    public void Wait(double seconds){
        if(seconds<=0)return;
        long due=-Math.Max(1,(long)(seconds*10000000));
        if(timer!=IntPtr.Zero&&SetWaitableTimer(timer,ref due,0,IntPtr.Zero,IntPtr.Zero,false))WaitForSingleObject(timer,1000);
        else Thread.Sleep(Math.Max(1,(int)(seconds*1000)));
    }
    public void Dispose(){if(timer!=IntPtr.Zero){CloseHandle(timer);timer=IntPtr.Zero;}}
}
// All OpenVR, D3D and keyboard state stays on this one worker; the UI only queues commands.
public sealed class OverlayRunner : IDisposable {
    readonly ConcurrentQueue<Action<Overlay>> commands=new ConcurrentQueue<Action<Overlay>>();
    readonly Thread worker;volatile bool stop;
    public volatile string Info="SteamVR 연결 대기";
    public volatile StartupState StartupState=new StartupState(false,"SteamVR 연결 대기 중…");
    public OverlayRunner(int port){worker=new Thread(()=>Run(port)){IsBackground=true,Name="HangeulVR render"};worker.SetApartmentState(ApartmentState.STA);worker.Start();}
    public void Post(Action<Overlay> command){if(!stop)commands.Enqueue(command);}
    void Run(int port){
        try {
            using(var vr=new Overlay())using(var waiter=new FrameWaiter()){
                vr.Keyboard.Port=port;vr.Keyboard.LoadSettings(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"user-settings.json"));var clock=Stopwatch.StartNew();double deadline=clock.Elapsed.TotalSeconds;
                while(!stop){
                    Action<Overlay> action;
                    while(commands.TryDequeue(out action)){try{action(vr);}catch(Exception e){vr.Info="조작 오류: "+e.Message;}}
                    vr.Tick();Info=vr.Info;vr.Startup.Tick(vr.Connected,clock.Elapsed.TotalSeconds);StartupState=vr.Startup.State;
                    double period=vr.Connected?1.0/Math.Min(288,Math.Max(180,vr.TargetHz*2)):.05;
                    deadline+=period;double now=clock.Elapsed.TotalSeconds;
                    if(deadline<now)deadline=now; // skip missed slots; never burst to catch up
                    waiter.Wait(deadline-now);
                }
            }
        } catch(Exception e){Info="렌더링 중단: "+e.Message;}
    }
    public void Dispose(){stop=true;worker.Join();}
}
}
