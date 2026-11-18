using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace HangeulVR {
[DataContract]public sealed class ReleaseInfo {
    [DataMember]public string Version;
    [DataMember]public string PackageUrl;
    [DataMember]public string Sha256;
    [DataMember]public long PackageBytes;
    [DataMember]public string EntryPoint;
    [DataMember]public string Notes;
}
public static class Updates {
    public const string DefaultRepository="Hyeophap/Hangul-Garden";
    public static Version Current {get{return Assembly.GetExecutingAssembly().GetName().Version;}}
    public const long MaxPackage=128L*1024*1024;
    internal static string NewerInstalled(string root,Version current,bool allowEqual=false){
        string best=null;Version highest=current;if(!Directory.Exists(root))return null;
        foreach(string folder in Directory.GetDirectories(root))foreach(string exe in Directory.GetFiles(folder,"HangeulVR-*.exe")){
            try{
                var version=ParseVersion(FileVersionInfo.GetVersionInfo(exe).FileVersion);
                if((version>highest||(allowEqual&&best==null&&version==highest))&&File.Exists(Path.Combine(folder,".installed"))&&File.Exists(Path.Combine(folder,"openvr_api.dll"))&&File.Exists(Path.Combine(folder,"actions.json"))){highest=version;best=exe;}
            }catch(IOException){}catch(InvalidDataException){}
        }return best;
    }
    public static bool ForwardToInstalled(){
        string root=Path.Combine(AppSettings.Root,"versions"),current=System.Windows.Forms.Application.ExecutablePath;
        bool original=!AutoLaunch.IsWithin(current,root);string exe=NewerInstalled(root,Current,original);
        if(exe==null)return false;
        if(original){var settings=AppSettings.Load();settings.EntryExe=current;settings.Save();}
        AutoLaunch.Launch(exe,"--installed-start");return true;
    }
    public static void RememberAndRepairEntry(AppSettings settings){
        string current=System.Windows.Forms.Application.ExecutablePath;
        if(!AutoLaunch.IsWithin(current,Path.Combine(AppSettings.Root,"versions"))){settings.EntryExe=current;return;}
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(current),".installed"),Current.ToString());
        string entry=settings.EntryExe;
        if(String.IsNullOrEmpty(entry)||!File.Exists(entry)){
            // 1.1.3 discards unknown settings fields and never records its source
            // path. Ask once rather than guessing and replacing unrelated files.
            using(var picker=new System.Windows.Forms.OpenFileDialog()){
                picker.Title="업데이트 유지 설정: 평소 실행하던 이전 한글뜰 EXE를 선택하세요";
                picker.Filter="한글뜰 실행 파일|HangeulVR-*.exe";
                picker.InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if(picker.ShowDialog()!=System.Windows.Forms.DialogResult.OK){
                    System.Windows.Forms.MessageBox.Show("이전 실행 파일 연결을 설정하지 않았습니다. 기존 아이콘을 열면 업데이트 안내가 반복될 수 있습니다.\r\n현재 버전 실행 위치:\r\n"+current,"한글뜰 업데이트 유지 설정");return;
                }
                entry=picker.FileName;
            }
        }
        if(String.Equals(entry,current,StringComparison.OrdinalIgnoreCase))return;
        string problem;
        if(!RepairEntry(current,entry,out problem)){
            System.Windows.Forms.MessageBox.Show("이전 실행 파일을 최신 버전에 연결하지 못했습니다.\r\n"+problem+"\r\n이전 프로그램을 닫고 현재 버전을 다시 실행해 주세요.","한글뜰 업데이트 유지 설정 실패");return;
        }
        settings.EntryExe=entry;settings.Save();
    }
    internal static bool RepairEntry(string current,string entry,out string problem){
        problem="";
        try{
            AutoLaunch.RequireInstalledPath(entry);
            var info=FileVersionInfo.GetVersionInfo(entry);
            if(!Regex.IsMatch(Path.GetFileName(entry),@"\AHangeulVR-[0-9]+(?:\.[0-9]+){1,3}\.exe\z")||info.CompanyName!="협합"||info.FileDescription!="한글뜰")throw new InvalidDataException("이전 한글뜰 실행 파일을 선택해 주세요.");
            if(ParseVersion(info.FileVersion)>Current)throw new InvalidDataException("더 새로운 실행 파일은 교체하지 않습니다.");
            if(!File.Exists(entry+".before-update.bak"))File.Copy(entry,entry+".before-update.bak");
        }catch(Exception e){problem=e.Message;return false;}
        // Refresh the original entry point. Its early version check redirects to
        // the installed version before loading the old folder's DLLs or bindings.
        for(int attempt=0;attempt<20;attempt++)try{
            string temp=entry+".update.tmp";File.Copy(current,temp,true);File.Replace(temp,entry,null);return true;
        }catch(IOException e){problem=e.Message;System.Threading.Thread.Sleep(100);}catch(UnauthorizedAccessException e){problem=e.Message;return false;}
        return false;
    }
    public static string ValidateRepository(string repository){
        string s=(repository??"").Trim();
        if(!Regex.IsMatch(s,@"\A[A-Za-z0-9][A-Za-z0-9-]{0,38}/[A-Za-z0-9_.-]{1,100}\z")||s.EndsWith("/.")||s.EndsWith("/.."))throw new ArgumentException("GitHub 저장소를 소유자/저장소 형식으로 입력하세요.");
        return s;
    }
    public static Version ParseVersion(string value){
        Version v;if(!System.Version.TryParse(value,out v))throw new InvalidDataException("올바르지 않은 버전입니다.");
        return new Version(v.Major,v.Minor,Math.Max(0,v.Build),Math.Max(0,v.Revision));
    }
    static string PublicKey(){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("HangeulVR.UpdatePublicKey"))using(var r=new StreamReader(s))return r.ReadToEnd();}
    internal static ReleaseInfo VerifyManifest(byte[] data,byte[] signature,string key,string repository){
        if(data.Length>65536||signature.Length>1024)throw new InvalidDataException("업데이트 정보가 너무 큽니다.");
        using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(key);if(!rsa.VerifyData(data,"SHA256",signature))throw new InvalidDataException("업데이트 서명을 확인할 수 없습니다.");}
        ReleaseInfo release;using(var s=new MemoryStream(data))release=(ReleaseInfo)new DataContractJsonSerializer(typeof(ReleaseInfo)).ReadObject(s);
        if(release==null)throw new InvalidDataException("업데이트 정보가 없습니다.");ParseVersion(release.Version);
        string repo=ValidateRepository(repository);Uri url;
        if(!Uri.TryCreate(release.PackageUrl,UriKind.Absolute,out url)||url.Scheme!="https"||url.Host!="github.com"||url.UserInfo!=""||!url.IsDefaultPort||!url.AbsolutePath.StartsWith("/"+repo+"/releases/download/",StringComparison.Ordinal))throw new InvalidDataException("저장소의 HTTPS Releases 파일만 받을 수 있습니다.");
        if(release.PackageBytes<=0||release.PackageBytes>MaxPackage||!Regex.IsMatch(release.Sha256??"",@"\A[0-9a-fA-F]{64}\z"))throw new InvalidDataException("올바르지 않은 파일 크기 또는 해시입니다.");
        if(!Regex.IsMatch(release.EntryPoint??"",@"\AHangeulVR-[0-9]+(?:\.[0-9]+){1,3}\.exe\z"))throw new InvalidDataException("올바르지 않은 실행 파일 이름입니다.");
        return release;
    }
    internal static bool AllowedDownload(Uri uri){
        return uri.Scheme=="https"&&uri.IsDefaultPort&&uri.UserInfo==""&&(uri.Host=="github.com"||uri.Host=="release-assets.githubusercontent.com"||uri.Host=="objects.githubusercontent.com"||uri.Host=="github-releases.githubusercontent.com");
    }
    static void Fetch(string address,Stream destination,long limit){
        Uri uri=new Uri(address);ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
        for(int redirects=0;redirects<6;redirects++){
            if(!AllowedDownload(uri))throw new InvalidDataException("허용되지 않은 다운로드 주소입니다.");
            var request=(HttpWebRequest)WebRequest.Create(uri);request.AllowAutoRedirect=false;request.Timeout=15000;request.ReadWriteTimeout=15000;request.UserAgent="HangeulVR/"+Current;request.AutomaticDecompression=DecompressionMethods.None;
            using(var response=(HttpWebResponse)request.GetResponse()){
                int code=(int)response.StatusCode;
                if(code>=300&&code<=399){uri=new Uri(uri,response.Headers["Location"]);continue;}
                if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>limit)throw new InvalidDataException("다운로드 응답 또는 파일 크기가 올바르지 않습니다.");
                using(var source=response.GetResponseStream())CopyLimited(source,destination,limit);return;
            }
        }
        throw new InvalidDataException("다운로드 리디렉션이 너무 많습니다.");
    }
    internal static long CopyLimited(Stream input,Stream output,long limit){
        byte[] buffer=new byte[65536];long count=0;int read;
        while((read=input.Read(buffer,0,buffer.Length))>0){count+=read;if(count>limit)throw new InvalidDataException("파일 크기 제한을 초과했습니다.");output.Write(buffer,0,read);}return count;
    }
    public static ReleaseInfo Check(string repository){
        string url="https://github.com/"+ValidateRepository(repository)+"/releases/latest/download/update.json";
        byte[] data,signature;using(var s=new MemoryStream()){Fetch(url,s,65536);data=s.ToArray();}using(var s=new MemoryStream()){Fetch(url+".sig",s,1024);signature=s.ToArray();}
        var release=VerifyManifest(data,signature,PublicKey(),repository);return ParseVersion(release.Version)>Current?release:null;
    }
    public static string Prepare(ReleaseInfo release){
        string cache=Path.Combine(AppSettings.Root,"downloads");Directory.CreateDirectory(cache);string zip=Path.Combine(cache,Guid.NewGuid().ToString("N")+".zip");
        try{using(var f=new FileStream(zip,FileMode.CreateNew,FileAccess.Write,FileShare.None))Fetch(release.PackageUrl,f,release.PackageBytes);return Stage(zip,release,Path.Combine(AppSettings.Root,"versions"));}
        finally{if(File.Exists(zip))File.Delete(zip);}
    }
    internal static string Stage(string zip,ReleaseInfo release,string versionsRoot){
        if(new FileInfo(zip).Length!=release.PackageBytes)throw new InvalidDataException("다운로드 크기가 일치하지 않습니다.");
        string digest;using(var f=File.OpenRead(zip))using(var sha=SHA256.Create())digest=BitConverter.ToString(sha.ComputeHash(f)).Replace("-","");
        if(!String.Equals(digest,release.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("다운로드 해시가 일치하지 않습니다.");
        var required=new HashSet<string>(StringComparer.OrdinalIgnoreCase){release.EntryPoint,"openvr_api.dll","actions.json","bindings_oculus_touch.json","bindings_knuckles.json","bindings_vive_controller.json","bindings_holographic_controller.json"};
        var allowed=new HashSet<string>(required,StringComparer.OrdinalIgnoreCase);allowed.UnionWith(new[]{"OpenVR-LICENSE.txt","사용안내.txt","THIRD-PARTY.txt","COPYRIGHT.txt","README.md"});
        string versionFolder=Path.Combine(Path.GetFullPath(versionsRoot),ParseVersion(release.Version)+"-"+Guid.NewGuid().ToString("N"));
        using(var archive=ZipFile.OpenRead(zip)){
            if(archive.Entries.Count>allowed.Count)throw new InvalidDataException("배포 파일 수가 올바르지 않습니다.");
            long total=0;var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in archive.Entries){
                if(!allowed.Contains(entry.FullName)||!seen.Add(entry.FullName)||entry.Length>MaxPackage||((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("허용되지 않은 배포 파일입니다: "+entry.FullName);
                total+=entry.Length;if(total>200L*1024*1024)throw new InvalidDataException("압축 해제 크기가 너무 큽니다.");
            }
            if(!required.IsSubsetOf(seen))throw new InvalidDataException("필수 배포 파일이 빠졌습니다.");
            Directory.CreateDirectory(versionFolder);
            foreach(var entry in archive.Entries)using(var input=entry.Open())using(var output=new FileStream(Path.Combine(versionFolder,entry.FullName),FileMode.CreateNew,FileAccess.Write,FileShare.None))CopyLimited(input,output,entry.Length);
        }
        string exe=Path.Combine(versionFolder,release.EntryPoint);
        using(var verified=NativeLibrarySecurity.OpenVerified(Path.Combine(versionFolder,"openvr_api.dll"))){}
        if(ParseVersion(FileVersionInfo.GetVersionInfo(exe).FileVersion)!=ParseVersion(release.Version))throw new InvalidDataException("실행 파일의 버전이 업데이트 정보와 다릅니다.");
        File.WriteAllText(Path.Combine(versionFolder,".installed"),release.Version);
        return exe;
    }
}
}
