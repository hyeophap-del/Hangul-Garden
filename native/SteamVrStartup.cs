using System;
using System.IO;
using System.Text;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Valve.VR;

namespace HangeulVR {
public sealed class StartupState {
    public readonly bool Enabled;public readonly string Message;
    public StartupState(bool enabled,string message){Enabled=enabled;Message=message;}
}
// Called only on the OpenVR worker thread. SteamVR owns the applied On/Off state.
public sealed class SteamVrStartup {
    [DataContract]sealed class RuntimePaths {[DataMember(Name="config")]public string[] Config {get;set;}}
    [DataContract]sealed class RuntimeAppConfig {[DataMember(Name="autolaunch")]public bool? AutoLaunch {get;set;}}
    // Some SteamVR builds return a stale false from GetApplicationAutoLaunch even
    // after SetApplicationAutoLaunch succeeds. Read SteamVR's own saved value;
    // all writes still go through the supported OpenVR API.
    internal static bool ReadEnabled(CVRApplications apps){
        string overridePath=Environment.GetEnvironmentVariable("VR_CONFIG_PATH");
        string[] paths=String.IsNullOrEmpty(overridePath)?new string[0]:new[]{overridePath};
        string registry=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"openvr","openvrpaths.vrpath");
        if(paths.Length==0&&File.Exists(registry))using(var f=File.OpenRead(registry)){var p=(RuntimePaths)new DataContractJsonSerializer(typeof(RuntimePaths)).ReadObject(f);paths=p.Config??paths;}
        foreach(string path in paths){
            string config=Path.Combine(path,"vrappconfig",AppKey+".vrappconfig");
            if(File.Exists(config))using(var f=new FileStream(config,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
                var c=(RuntimeAppConfig)new DataContractJsonSerializer(typeof(RuntimeAppConfig)).ReadObject(f);if(c.AutoLaunch.HasValue)return c.AutoLaunch.Value;
            }
        }
        return apps.GetApplicationAutoLaunch(AppKey);
    }
    public const string AppKey="local.hyeophap.hangeulvr";
    public StartupState State=new StartupState(false,"SteamVR 연결 후 시작 앱 목록에 등록합니다.");
    bool registered;double nextPoll;
    static string Json(string value){using(var s=new MemoryStream()){new DataContractJsonSerializer(typeof(string)).WriteObject(s,value);return Encoding.UTF8.GetString(s.ToArray());}}
    internal static string Manifest(string exe){
        exe=Path.GetFullPath(exe);AutoLaunch.RequireInstalledPath(exe);
        return "{\"source\":\"builtin\",\"applications\":[{\"app_key\":"+Json(AppKey)+",\"launch_type\":\"binary\",\"binary_path_windows\":"+Json(exe)+",\"working_directory\":"+Json(Path.GetDirectoryName(exe))+",\"arguments\":\"--steamvr-forward\",\"is_dashboard_overlay\":true,\"strings\":{\"en_us\":{\"name\":\"협합 · 한글뜰\"},\"ko_kr\":{\"name\":\"협합 · 한글뜰\"}}}]}";
    }
    static void Check(EVRApplicationError error){if(error!=EVRApplicationError.None)throw new InvalidOperationException(error.ToString());}
    internal static string PrepareLauncher(string exe){
        exe=Path.GetFullPath(exe);AutoLaunch.RequireInstalledPath(exe);
        string folder=Path.Combine(AppSettings.Root,"steamvr-launcher");Directory.CreateDirectory(folder);
        string launcher=Path.Combine(folder,"HangeulVR-"+Updates.Current.ToString(3)+".exe");
        // An exact copy of our executable acts only as a short-lived Unicode-aware
        // launcher. SteamVR never has to execute the user's Desktop/OneDrive path.
        byte[] source=File.ReadAllBytes(exe);
        if(!File.Exists(launcher)||!EqualBytes(source,File.ReadAllBytes(launcher))){
            string temp=launcher+".tmp";File.WriteAllBytes(temp,source);
            if(File.Exists(launcher))File.Replace(temp,launcher,null);else File.Move(temp,launcher);
        }
        var s=AppSettings.Load();s.ActiveExe=exe;s.Save();return launcher;
    }
    static bool EqualBytes(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    internal static void ForwardLaunch(){
        string target=AppSettings.Load().ActiveExe;
        if(String.IsNullOrEmpty(target)||!File.Exists(target)||AutoLaunch.IsWithin(target,Path.Combine(AppSettings.Root,"steamvr-launcher")))throw new InvalidOperationException("한글뜰 실행 위치를 찾지 못했습니다. 압축 해제한 폴더의 프로그램을 한 번 실행해 주세요.");
        AutoLaunch.Launch(target,"--steamvr-start");
    }
    internal static void Register(CVRApplications apps,string exe){
                bool wasInstalled=apps.IsApplicationInstalled(AppKey),wasEnabled=ReadEnabled(apps);
                string path=Path.Combine(AppSettings.Root,"hangeulvr.vrmanifest"),data=Manifest(PrepareLauncher(exe));
                Directory.CreateDirectory(AppSettings.Root);
                if(!File.Exists(path)||File.ReadAllText(path)!=data){
                    string temp=path+".tmp";File.WriteAllText(temp,data,new UTF8Encoding(false));
                    if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
                }
                Check(apps.AddApplicationManifest(path,false));
                if(!apps.IsApplicationInstalled(AppKey))throw new InvalidOperationException("SteamVR 앱 등록을 확인할 수 없습니다.");
                if(wasInstalled&&ReadEnabled(apps)!=wasEnabled)Check(apps.SetApplicationAutoLaunch(AppKey,wasEnabled));
    }
    public void Request(bool enabled){
        try{var s=AppSettings.Load();s.PendingSteamVrAutoStart=enabled;s.Save();nextPoll=0;State=new StartupState(enabled,"SteamVR에 자동 시작 설정 적용 대기 중…");}
        catch(Exception e){State=new StartupState(State.Enabled,"자동 시작 설정 저장 실패: "+e.Message);}
    }
    public void Disconnected(){registered=false;nextPoll=0;State=new StartupState(State.Enabled,"SteamVR 연결 후 시작 앱 목록에 등록합니다.");}
    public void Tick(bool connected,double now){
        if(now<nextPoll)return;nextPoll=now+3;
        try{
            var s=AppSettings.Load();
            if(!connected){State=new StartupState(s.PendingSteamVrAutoStart??State.Enabled,"SteamVR을 켜면 자동 시작 설정이 적용됩니다.");return;}
            var apps=OpenVR.Applications;if(apps==null)throw new InvalidOperationException("SteamVR Applications 연결 실패");
            if(!registered){
                Register(apps,Application.ExecutablePath);
                Check(apps.IdentifyApplication((uint)System.Diagnostics.Process.GetCurrentProcess().Id,AppKey));
                registered=true;
            }
            if(s.PendingSteamVrAutoStart.HasValue){
                bool desired=s.PendingSteamVrAutoStart.Value;
                if(ReadEnabled(apps)!=desired){Check(apps.SetApplicationAutoLaunch(AppKey,desired));State=new StartupState(desired,"SteamVR 자동 시작 설정 적용 확인 중…");return;}
                s.PendingSteamVrAutoStart=null;s.Save();
            }
            State=new StartupState(ReadEnabled(apps),"SteamVR → 시작/종료 → 시작 오버레이 앱에서도 변경할 수 있습니다.");
        }catch(Exception e){State=new StartupState(State.Enabled,"SteamVR 자동 시작 설정 실패: "+e.Message);}
    }
}
}
