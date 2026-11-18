using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HangeulVR {
[DataContract]public sealed class AppSettings {
    [DataMember]public bool AutoStart;
    [DataMember]public bool? PendingSteamVrAutoStart;
    internal void MigrateStartup(){if(AutoStart&&!PendingSteamVrAutoStart.HasValue)PendingSteamVrAutoStart=true;AutoStart=false;}
    [DataMember]public string Repository=Updates.DefaultRepository;
    [DataMember]public string ActiveExe="";
    [DataMember]public string EntryExe="";
    public static readonly string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HangeulVR");
    static string Config {get{return Path.Combine(Root,"app-settings.json");}}
    public static AppSettings Load(){
        if(!File.Exists(Config))return new AppSettings();
        using(var f=File.OpenRead(Config))return (AppSettings)new DataContractJsonSerializer(typeof(AppSettings)).ReadObject(f)??new AppSettings();
    }
    public void Save(){
        Directory.CreateDirectory(Root);string temp=Config+".tmp";
        using(var f=File.Create(temp))new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(f,this);
        if(File.Exists(Config))File.Replace(temp,Config,null);else File.Move(temp,Config);
    }
}
public static class AutoLaunch {
    const string RunKey="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    internal static bool IsWithin(string path,string directory){
        string full=Path.GetFullPath(path),root=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        return String.Equals(full,root,StringComparison.OrdinalIgnoreCase)||full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
    }
    internal static void RequireInstalledPath(string exe){
        string localTemp=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Temp");
        string windowsTemp=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Temp");
        if(IsWithin(exe,Path.GetTempPath())||IsWithin(exe,localTemp)||IsWithin(exe,windowsTemp))
            throw new InvalidDataException("압축파일 안이나 임시 폴더에서는 실행할 수 없습니다. ZIP 전체를 문서 등 보관할 폴더에 압축 해제한 후 실행해 주세요.");
    }
    public static void RemoveLegacyStartup(){
        using(var key=Registry.CurrentUser.OpenSubKey(RunKey,true))if(key!=null)key.DeleteValue("HangeulVR",false);
    }
    public static void Launch(string exe,string args){
        RequireInstalledPath(exe);
        Process.Start(new ProcessStartInfo{FileName=exe,Arguments=args,WorkingDirectory=Path.GetDirectoryName(exe),UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Normal});
    }
}
}
