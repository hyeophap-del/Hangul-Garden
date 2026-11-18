using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyVersion("1.1.9.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.1.9.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.1.9")]
[assembly: System.Reflection.AssemblyTitle("한글뜰")]
[assembly: System.Reflection.AssemblyCompany("협합")]
[assembly: System.Reflection.AssemblyCopyright("Copyright © 2026 협합. 무단 재배포 금지. 제3자 구성요소는 각 라이선스 적용.")]

namespace HangeulVR {
// 한글뜰 만든이: 협합. 문제·오류·피드백 문의: 디스코드 suin__.
public static class AppInfo {
    public static Icon LoadIcon(){
        using(var stream=typeof(AppInfo).Assembly.GetManifestResourceStream("HangeulVR.AppIcon"))
        using(var icon=new Icon(stream,32,32))return (Icon)icon.Clone();
    }
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.DllImport("shell32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    public static void SetTaskbarIdentity(){SetCurrentProcessExplicitAppUserModelID("Hyeophap.HangeulVR");}
    public const string Creator="만든이: 협합";
    public const string Feedback="문제·오류·피드백 문의: 디스코드 suin__ (언더바 두 개)";
    public const string Copyright="Copyright © 2026 협합. 무단 재배포 금지.";
    public const string ThirdParty="제3자 구성요소·이미지의 권리는 각 권리자에게 있습니다.";
}
public class ControlWindow : Form {
    readonly OverlayRunner runner;
    readonly Label info=new Label();
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    public ControlWindow(){
        Text="한글뜰 1.1.9 · 실시간 한글 채팅";ClientSize=new Size(570,520);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;DoubleBuffered=true;
        Icon=AppInfo.LoadIcon();
        Font=new Font("Malgun Gothic",10);BackColor=Color.FromArgb(20,27,39);ForeColor=Color.White;
        info.SetBounds(22,20,525,95);Controls.Add(info);
        Add("키보드 열기 / 위치 재설정",22,120,270,delegate{runner.Post(vr=>vr.Show());});
        Add("숨기기",306,120,240,delegate{runner.Post(vr=>vr.Hide());});
        Add("컨트롤러 바인딩 설정",22,172,270,delegate{runner.Post(vr=>vr.OpenBindings());});
        Add("다시 연결",306,172,240,delegate{runner.Post(vr=>vr.Reconnect());});
        var note=new Label{Text="VRChat: OSC → Enabled를 켜 주세요. 이 창은 최소화해도 됩니다.\r\n종료하려면 창을 닫으세요. 대시보드를 열면 키 입력이 잠시 멈춥니다.\r\n"+AppInfo.Creator+"\r\n"+AppInfo.Feedback+"\r\n"+AppInfo.Copyright+"\r\n"+AppInfo.ThirdParty,AutoSize=false};note.SetBounds(22,362,525,150);Controls.Add(note);
        var launcher=new LauncherPanel();launcher.Location=new Point(22,232);Controls.Add(launcher);Shown+=async delegate{await launcher.CheckNow(true);};
        int port=9000;var setting=Environment.GetEnvironmentVariable("OSC_PORT");
        if(!String.IsNullOrEmpty(setting)){if(!Int32.TryParse(setting,out port)||port<1||port>65535)throw new ArgumentException("OSC_PORT는 1~65535여야 합니다.");}
        runner=new OverlayRunner(port);launcher.SetAutoStart=enabled=>runner.Post(vr=>vr.Startup.Request(enabled));
        timer.Interval=200;timer.Tick+=delegate{if(info.Text!=runner.Info)info.Text=runner.Info;launcher.RefreshStartup(runner.StartupState);};timer.Start();
        FormClosed+=delegate{timer.Stop();timer.Dispose();runner.Dispose();};
    }
    void Add(string text,int x,int y,int w,Action action){var b=new Button{Text=text,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(42,57,75)};b.SetBounds(x,y,w,42);b.Click+=delegate{try{action();}catch(Exception e){MessageBox.Show(e.Message,"한글뜰");}};Controls.Add(b);}
}
#if DEVELOPMENT
public class PreviewWindow : Form {
    Keyboard k=new Keyboard();int hover=-1;
    readonly System.Windows.Forms.Timer liveTimer=new System.Windows.Forms.Timer();
    public PreviewWindow(){
        Icon=AppInfo.LoadIcon();
        Text="한글뜰 · 키보드 미리보기 (VR 연결 없음)";ClientSize=new Size(Keyboard.W,Keyboard.H);DoubleBuffered=true;
        k.Command=delegate(string c){if(c=="hide")Close();};
        MouseMove+=delegate(object s,MouseEventArgs e){hover=k.Hit(e.X*Keyboard.W/(float)ClientSize.Width,e.Y*Keyboard.H/(float)ClientSize.Height);Invalidate();};
        MouseDown+=delegate(object s,MouseEventArgs e){if(e.Button==MouseButtons.Left){k.Activate(k.Hit(e.X*Keyboard.W/(float)ClientSize.Width,e.Y*Keyboard.H/(float)ClientSize.Height),DateTime.UtcNow.Ticks/10000000.0);Invalidate();}};
        liveTimer.Interval=50;liveTimer.Tick+=delegate{if(k.UpdateLive(DateTime.UtcNow.Ticks/10000000.0))Invalidate();};liveTimer.Start();
        FormClosed+=delegate{liveTimer.Stop();liveTimer.Dispose();};
    }
    protected override void OnPaint(PaintEventArgs e){using(var b=k.Render(hover,-1,null,null))e.Graphics.DrawImage(b,ClientRectangle);}
}
#endif
static class Program {
    public static string RestartTarget;
    [STAThread]static int Main(string[] args){
        try{
#if DEVELOPMENT
            if(args.Length==2&&args[0]=="--launch-steamvr"){
                using(var dll=NativeLibrarySecurity.OpenVerified(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"openvr_api.dll"))){
                    var error=Valve.VR.EVRInitError.None;Valve.VR.OpenVR.Init(ref error,Valve.VR.EVRApplicationType.VRApplication_Utility);
                    if(error!=Valve.VR.EVRInitError.None)throw new InvalidOperationException(error.ToString());
                    try{File.WriteAllText(args[1],Valve.VR.OpenVR.Applications.LaunchDashboardOverlay(SteamVrStartup.AppKey).ToString());}finally{Valve.VR.OpenVR.Shutdown();}
                }return 0;
            }
            if(args.Length==3&&args[0]=="--register-steamvr"){
                using(var dll=NativeLibrarySecurity.OpenVerified(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"openvr_api.dll"))){
                    var error=Valve.VR.EVRInitError.None;Valve.VR.OpenVR.Init(ref error,Valve.VR.EVRApplicationType.VRApplication_Overlay);
                    if(error!=Valve.VR.EVRInitError.None)throw new InvalidOperationException(error.ToString());
                    try{
                        var apps=Valve.VR.OpenVR.Applications;SteamVrStartup.Register(apps,args[1]);
                        var propertyError=Valve.VR.EVRApplicationError.None;var name=new System.Text.StringBuilder(1024);
                        apps.GetApplicationPropertyString(SteamVrStartup.AppKey,Valve.VR.EVRApplicationProperty.Name_String,name,1024,ref propertyError);
                        bool overlay=apps.GetApplicationPropertyBool(SteamVrStartup.AppKey,Valve.VR.EVRApplicationProperty.IsDashboardOverlay_Bool,ref propertyError);
                        if(propertyError!=Valve.VR.EVRApplicationError.None||!overlay)throw new InvalidOperationException("Overlay registration not confirmed");
                        bool original=SteamVrStartup.ReadEnabled(apps);
                        try{
                            foreach(bool enabled in new[]{true,false}){
                                var toggleError=apps.SetApplicationAutoLaunch(SteamVrStartup.AppKey,enabled);System.Threading.Thread.Sleep(2000);
                                var ev=new Valve.VR.VREvent_t();while(Valve.VR.OpenVR.System.PollNextEvent(ref ev,(uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Valve.VR.VREvent_t)))){}
                                if(toggleError!=Valve.VR.EVRApplicationError.None||SteamVrStartup.ReadEnabled(apps)!=enabled)throw new InvalidOperationException("SteamVR toggle failed: desired="+enabled+", result="+toggleError+", actual="+SteamVrStartup.ReadEnabled(apps));
                                SteamVrStartup.Register(apps,args[1]);
                                if(SteamVrStartup.ReadEnabled(apps)!=enabled)throw new InvalidOperationException("Registration reset user toggle");
                            }
                        }finally{if(apps.SetApplicationAutoLaunch(SteamVrStartup.AppKey,original)!=Valve.VR.EVRApplicationError.None)throw new InvalidOperationException("Cannot restore SteamVR toggle");}
                        File.WriteAllText(args[2],"Registered: "+SteamVrStartup.AppKey+"\r\nOverlay: "+overlay+"\r\nON/OFF and preservation after registration: PASS\r\nAutoLaunch restored: "+SteamVrStartup.ReadEnabled(apps));
                    }finally{Valve.VR.OpenVR.Shutdown();}
                }return 0;
            }
            if(args.Length==2&&args[0]=="--render-update"){
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                using(var dialog=new UpdatePrompt(new ReleaseInfo{Version="1.2"})){dialog.Show();Application.DoEvents();using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(0,0,dialog.Width,dialog.Height));bitmap.Save(args[1]);}}return 0;
            }
            if(args.Length==4&&args[0]=="--verify-update"){
                string key;using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("HangeulVR.UpdatePublicKey"))using(var reader=new StreamReader(stream))key=reader.ReadToEnd();
                var release=Updates.VerifyManifest(File.ReadAllBytes(args[1]),File.ReadAllBytes(args[2]),key,args[3]);Console.WriteLine("Verified signed release "+release.Version);return 0;
            }
            if(args.Length==2&&args[0]=="--render-launcher"){
                using(var form=new Form()){form.ClientSize=new Size(570,300);form.BackColor=Color.FromArgb(20,27,39);form.Font=new Font("Malgun Gothic",10);var panel=new LauncherPanel();panel.Location=new Point(22,15);form.Controls.Add(panel);form.Show();Application.DoEvents();using(var bitmap=new Bitmap(570,300)){form.DrawToBitmap(bitmap,form.ClientRectangle);bitmap.Save(args[1]);}}return 0;
            }
            if(args.Length>0&&args[0]=="--self-test"){SelfTest.Run(args.Length>1?args[1]:"self-test.txt");return 0;}
            if(args.Length>0&&args[0]=="--render-benchmark"){SelfTest.RenderBenchmark(args.Length>1?args[1]:"render-benchmark.txt");return 0;}
            if(args.Length>0&&args[0]=="--gpu-test"){SelfTest.GpuProbe(args.Length>1?args[1]:"gpu-test.txt");return 0;}
            if(args.Length>1&&(args[0]=="--render"||args[0]=="--render-ctrl"||args[0]=="--render-settings")){var k=new Keyboard();if(args[0]=="--render-ctrl"){k.Ctrl=true;k.Layout();}if(args[0]=="--render-settings"){k.SettingsOpen=true;k.Layout();}k.Input.Keys="ㅇㅏㄴㄴㅕㅇㅎㅏㅅㅔㅇㅛ!";using(var b=k.Render(2,8,null,null))b.Save(args[1]);return 0;}
#endif
            try{AutoLaunch.RequireInstalledPath(Application.ExecutablePath);}
            catch(InvalidDataException e){MessageBox.Show(e.Message,"한글뜰 · 압축 해제 필요",MessageBoxButtons.OK,MessageBoxIcon.Information);return 1;}
            if(args.Length>0&&args[0]=="--steamvr-forward"){SteamVrStartup.ForwardLaunch();return 0;}
            AppInfo.SetTaskbarIdentity();
            if(args.Length>0&&args[0]=="--watch")return 0;
            if(Updates.ForwardToInstalled())return 0;
            using(var nativeLibrary=NativeLibrarySecurity.OpenVerified(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"openvr_api.dll"))){
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            bool created;using(var mutex=new Mutex(true,"Local\\HangeulVR.Keyboard",out created)){
                if(!created){if(args.Length==0)MessageBox.Show("한글뜰이 이미 실행 중입니다. 실행 중인 창을 확인해 주세요.","한글뜰");return 0;}
                var appSettings=AppSettings.Load();appSettings.MigrateStartup();AutoLaunch.RemoveLegacyStartup();Updates.RememberAndRepairEntry(appSettings);appSettings.ActiveExe=Application.ExecutablePath;appSettings.Save();

#if DEVELOPMENT
                Application.Run(args.Length>0&&args[0]=="--preview"?(Form)new PreviewWindow():new ControlWindow());
#else
                Application.Run(new ControlWindow());
#endif
            }}
            if(RestartTarget!=null){
                var settings=AppSettings.Load();string previous=settings.ActiveExe;
                try{
                    AutoLaunch.RequireInstalledPath(RestartTarget);
                    string oldPrefs=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"user-settings.json");
                    if(File.Exists(oldPrefs))File.Copy(oldPrefs,Path.Combine(Path.GetDirectoryName(RestartTarget),"user-settings.json"),true);
                    settings.ActiveExe=RestartTarget;settings.Save();
                    AutoLaunch.Launch(RestartTarget,"");
                }catch{
                    settings.ActiveExe=previous;settings.Save();
                    MessageBox.Show("새 버전을 실행하지 못했습니다. 기존 폴더의 프로그램을 다시 실행해 주세요.\r\n"+previous,"한글뜰 업데이트 실패");return 1;
                }
            }
            return 0;
        }catch(Exception e){
#if DEVELOPMENT
            if(args.Length==3&&args[0]=="--register-steamvr"){File.WriteAllText(args[2],"FAIL\r\n"+e);return 1;}
            if(args.Length>0&&(args[0]=="--self-test"||args[0]=="--gpu-test"||args[0]=="--render-benchmark"))File.WriteAllText(args.Length>1?args[1]:"self-test.txt","FAIL\r\n"+e);
            else
#endif
            MessageBox.Show(e.ToString(),"한글뜰 실행 오류");return 1;
        }
    }
}
}



