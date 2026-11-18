using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace HangeulVR {
public static class UpdateTests {
    static byte[] Serialize(ReleaseInfo info){using(var s=new MemoryStream()){new DataContractJsonSerializer(typeof(ReleaseInfo)).WriteObject(s,info);return s.ToArray();}}
    static bool Reject(Action action){try{action();return false;}catch(InvalidDataException){return true;}}
    static void HashPackage(string path,ReleaseInfo info){info.PackageBytes=new FileInfo(path).Length;using(var f=File.OpenRead(path))using(var sha=SHA256.Create())info.Sha256=BitConverter.ToString(sha.ComputeHash(f)).Replace("-","");}
    public static void Run(Action<bool,string> check){
        string legacyFolder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"entry-repair-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(legacyFolder);
        try{
            string entry=Path.Combine(legacyFolder,"HangeulVR-1.1.3.exe"),problem;
            string old=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","release","HangeulVR-1.1.3-Windows-x64","HangeulVR-1.1.3.exe"));
            File.Copy(old,entry);
            check(Updates.RepairEntry(System.Windows.Forms.Application.ExecutablePath,entry,out problem),"Real 1.1.3 entry is upgraded for forwarding");
            check(Updates.ParseVersion(System.Diagnostics.FileVersionInfo.GetVersionInfo(entry).FileVersion)==Updates.Current,"Old filename now executes current forwarding code");
            check(Updates.ParseVersion(System.Diagnostics.FileVersionInfo.GetVersionInfo(entry+".before-update.bak").FileVersion)==new Version(1,1,3,0),"Original 1.1.3 binary preserved for recovery");
            string unrelated=Path.Combine(legacyFolder,"unrelated.exe");File.Copy(old,unrelated);
            check(!Updates.RepairEntry(System.Windows.Forms.Application.ExecutablePath,unrelated,out problem),"Unrelated executable path is rejected");
        }finally{Directory.Delete(legacyFolder,true);}
        string installed=Path.Combine(Path.GetTempPath(),"hangeul-entry-test-"+Guid.NewGuid().ToString("N"));
        string install=Path.Combine(installed,"version");Directory.CreateDirectory(install);
        try{
            string entry=Path.Combine(install,"HangeulVR-test.exe");File.Copy(System.Windows.Forms.Application.ExecutablePath,entry);
            check(Updates.NewerInstalled(installed,new Version(0,0))==null,"Incomplete installation is not selected");
            File.WriteAllText(Path.Combine(install,"openvr_api.dll"),"test");File.WriteAllText(Path.Combine(install,"actions.json"),"{}");
            check(Updates.NewerInstalled(installed,new Version(0,0))==null,"Unfinished installation marker prevents launch");
            File.WriteAllText(Path.Combine(install,".installed"),"test");
            check(Updates.NewerInstalled(installed,new Version(0,0))==entry,"Old entry locates installed update");
            check(Updates.NewerInstalled(installed,Updates.Current)==null,"Installed version does not redirect to itself");
            check(Updates.NewerInstalled(installed,Updates.Current,true)==entry,"Refreshed original entry forwards to equal installed version");
            check(Updates.NewerInstalled(installed,new Version(99,0),true)==null,"Newer standalone never downgrades");
        }finally{Directory.Delete(installed,true);}
        int downloads=0;
        using(var prompt=new UpdatePrompt(new ReleaseInfo{Version="9.0"},r=>{downloads++;return System.Threading.Tasks.Task.FromResult("verified.exe");})){
            prompt.Show();System.Windows.Forms.Application.DoEvents();
            check(downloads==0,"Showing update prompt never starts download");
            foreach(System.Windows.Forms.Control c in prompt.Controls)if(c.Text=="업데이트하기")((System.Windows.Forms.Button)c).PerformClick();
            check(downloads==1&&prompt.Target=="verified.exe"&&prompt.DialogResult==System.Windows.Forms.DialogResult.OK,"Explicit update choice hands off prepared target");
        }
        var failed=new System.Threading.Tasks.TaskCompletionSource<string>();failed.SetException(new IOException("test download failure"));
        using(var prompt=new UpdatePrompt(new ReleaseInfo{Version="9.0"},r=>failed.Task)){
            prompt.Show();System.Windows.Forms.Application.DoEvents();
            foreach(System.Windows.Forms.Control c in prompt.Controls)if(c.Text=="업데이트하기")((System.Windows.Forms.Button)c).PerformClick();
            check(prompt.Target==null&&prompt.DialogResult!=System.Windows.Forms.DialogResult.OK,"Failed download cannot trigger restart");
            foreach(System.Windows.Forms.Control c in prompt.Controls)if(c is System.Windows.Forms.Button)check(c.Enabled,"Failure restores both choices");
        }
        check(AutoLaunch.IsWithin(@"C:\Temp\BNZ\app.exe",@"C:\Temp"),"Temporary extraction child recognized");
        check(AutoLaunch.IsWithin(@"c:\TEMP\x\..\app.exe",@"C:\Temp"),"Temporary path normalized case insensitively");
        check(!AutoLaunch.IsWithin(@"C:\Temporary\app.exe",@"C:\Temp"),"Sibling with shared prefix allowed");
        check(Reject(()=>AutoLaunch.RequireInstalledPath(Path.Combine(Path.GetTempPath(),"BNZ","app.exe"))),"Temporary executable blocked before registration and launch");
        var legacy=new AppSettings{AutoStart=true};legacy.MigrateStartup();
        check(!legacy.AutoStart&&legacy.PendingSteamVrAutoStart==true,"Legacy opt-in transfers once and stops old watcher");
        legacy.PendingSteamVrAutoStart=null;legacy.MigrateStartup();check(legacy.PendingSteamVrAutoStart==null,"Migration does not overwrite SteamVR choices on later starts");
        var off=new AppSettings{AutoStart=true,PendingSteamVrAutoStart=false};off.MigrateStartup();check(off.PendingSteamVrAutoStart==false,"Explicit pending OFF wins over legacy ON");
        string manifest=SteamVrStartup.Manifest(@"E:\한글 폴더\HangeulVR-1.1.4.exe");
        using(var reader=JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(manifest),new System.Xml.XmlDictionaryReaderQuotas())){
            var xml=new System.Xml.XmlDocument();xml.Load(reader);
            check(xml.SelectSingleNode("/root/applications/item/binary_path_windows").InnerText==@"E:\한글 폴더\HangeulVR-1.1.4.exe","SteamVR manifest Unicode and spaces round-trip");
            check(xml.SelectSingleNode("/root/applications/item/is_dashboard_overlay").InnerText=="true","App listed as startup overlay");
            check(xml.SelectSingleNode("/root/applications/item/arguments").InnerText=="--steamvr-forward","SteamVR uses Unicode-aware launcher without watcher");
            check(xml.SelectSingleNode("/root/applications/item/app_key").InnerText==SteamVrStartup.AppKey,"Stable app key across versions");
        }
        check(Reject(()=>SteamVrStartup.Manifest(Path.Combine(Path.GetTempPath(),"app.exe"))),"Temporary path cannot be registered in SteamVR");
        check(Updates.ParseVersion("1.1")==Updates.ParseVersion("1.1.0.0"),"Version components normalized");
        check(!Updates.AllowedDownload(new Uri("http://github.com/a/b"))&&!Updates.AllowedDownload(new Uri("https://github.com.evil.example/a"))&&!Updates.AllowedDownload(new Uri("https://github.com:444/a")),"Unsafe redirects rejected");
        var info=new ReleaseInfo{Version="1.1",EntryPoint="HangeulVR-1.1.exe",PackageUrl="https://github.com/example/project/releases/download/v1.1/package.zip",PackageBytes=10,Sha256=new string('a',64),Notes="테스트"};
        using(var rsa=new RSACryptoServiceProvider(2048)){
            rsa.PersistKeyInCsp=false;byte[] data=Serialize(info),sig=rsa.SignData(data,"SHA256");string key=rsa.ToXmlString(false);
            check(Updates.VerifyManifest(data,sig,key,"example/project").Version=="1.1","Valid signed manifest accepted");
            data[data.Length-2]^=1;check(Reject(()=>Updates.VerifyManifest(data,sig,key,"example/project")),"Manifest tampering rejected");data=Serialize(info);
            check(Reject(()=>Updates.VerifyManifest(data,sig,key,"other/project")),"Other repository package rejected");
            using(var wrong=new RSACryptoServiceProvider(2048)){wrong.PersistKeyInCsp=false;check(Reject(()=>Updates.VerifyManifest(data,sig,wrong.ToXmlString(false),"example/project")),"Unknown publisher signature rejected");}
            info.EntryPoint="../evil.exe";data=Serialize(info);sig=rsa.SignData(data,"SHA256");check(Reject(()=>Updates.VerifyManifest(data,sig,key,"example/project")),"Path in executable name rejected");info.EntryPoint="HangeulVR-1.1.exe";
        }
        using(var input=new MemoryStream(new byte[5]))using(var output=new MemoryStream())check(Reject(()=>Updates.CopyLimited(input,output,4)),"Oversized network data rejected");
        info.Version=Updates.Current.ToString(3);
        string root=Path.Combine(Path.GetTempPath(),"hangeul-update-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try{
            string zip=Path.Combine(root,"package.zip");
            using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create)){
                archive.CreateEntryFromFile(System.Reflection.Assembly.GetExecutingAssembly().Location,info.EntryPoint);
                archive.CreateEntryFromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"openvr_api.dll"),"openvr_api.dll");
                foreach(string name in new[]{"actions.json","bindings_oculus_touch.json","bindings_knuckles.json","bindings_vive_controller.json","bindings_holographic_controller.json"})using(var w=new StreamWriter(archive.CreateEntry(name).Open()))w.Write("{}");
            }
            HashPackage(zip,info);string target=Updates.Stage(zip,info,Path.Combine(root,"versions"));check(File.Exists(target),"Valid package stages in separate version folder");
            info.Sha256=new string('0',64);check(Reject(()=>Updates.Stage(zip,info,Path.Combine(root,"bad-hash"))),"Corrupt package rejected");
            using(var archive=ZipFile.Open(zip,ZipArchiveMode.Update))using(var w=new StreamWriter(archive.CreateEntry("../escape.txt").Open()))w.Write("escape");
            HashPackage(zip,info);check(Reject(()=>Updates.Stage(zip,info,Path.Combine(root,"bad-path")))&&!File.Exists(Path.Combine(root,"escape.txt")),"ZIP traversal rejected before extraction");
        }finally{Directory.Delete(root,true);}
    }
}
}
