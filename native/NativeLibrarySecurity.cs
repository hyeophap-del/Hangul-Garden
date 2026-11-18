using System;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

// Do not search the working directory or PATH for unmanaged dependencies.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.System32)]

namespace HangeulVR {
public static class NativeLibrarySecurity {
    // Official, signed Valve OpenVR DLL shipped with the pinned SDK in vendor/README.md.
    public const string OpenVrSha256="BAB8AC6EF64E68A9CA53315B0014D131088584B2EFDFA6DB511D67EC03CFCB4A";
    public static FileStream OpenVerified(string path){
        // Keep the verified file read-locked for the process lifetime to prevent replacement.
        var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        try {
            string actual;
            using(var sha=SHA256.Create())actual=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","");
            if(!String.Equals(actual,OpenVrSha256,StringComparison.Ordinal))
                throw new InvalidDataException("OpenVR DLL이 배포본과 다릅니다. 실행을 중단합니다. 공식 배포본을 다시 받아 주세요.");
            file.Position=0;return file;
        } catch {file.Dispose();throw;}
    }
}
}
