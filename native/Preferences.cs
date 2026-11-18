using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace HangeulVR {
[DataContract]public sealed class Preferences {
    [DataMember]public List<string> Phrases=new List<string>{"안녕하세요!","감사합니다","잠시만요"};
    [DataMember]public float Width=1.1f;
    [DataMember]public float Opacity=1;
    [DataMember]public int Hotkey;
    [DataMember]public bool GripDistance;
    [DataMember]public float Distance=.85f;
    internal static float NormalizeDistance(float distance){
        if(float.IsNaN(distance)||float.IsInfinity(distance)||distance<=0)return .85f;
        return Math.Max(.2f,Math.Min(5f,distance));
    }
    public static Preferences Load(string path){
        if(!File.Exists(path))return new Preferences();
        using(var file=File.OpenRead(path)){
            var value=(Preferences)new DataContractJsonSerializer(typeof(Preferences)).ReadObject(file);
            if(value==null)throw new InvalidDataException("설정 파일이 비어 있습니다.");
            value.Phrases=(value.Phrases??new List<string>()).FindAll(s=>!String.IsNullOrWhiteSpace(s)&&s.Length<=144);
            if(value.Phrases.Count>30)value.Phrases=value.Phrases.GetRange(0,30);
            value.Width=Math.Max(.55f,Math.Min(1.8f,value.Width));value.Opacity=Math.Max(.25f,Math.Min(1,value.Opacity));
            if(float.IsNaN(value.Width))value.Width=1.1f;if(float.IsNaN(value.Opacity))value.Opacity=1;
            value.Distance=NormalizeDistance(value.Distance);
            if(value.Hotkey<0||value.Hotkey>2)value.Hotkey=0;return value;
        }
    }
    public void Save(string path){
        string temp=path+".tmp";
        using(var file=File.Create(temp))new DataContractJsonSerializer(typeof(Preferences)).WriteObject(file,this);
        if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
    }
}
}
