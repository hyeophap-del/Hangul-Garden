using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using Valve.VR;

namespace HangeulVR {
public class Hangul {
    public const string L = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ", V = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ", T = " ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";
    static readonly Dictionary<string,char> vowels = Pairs("ㅗㅏㅘ ㅗㅐㅙ ㅗㅣㅚ ㅜㅓㅝ ㅜㅔㅞ ㅜㅣㅟ ㅡㅣㅢ");
    static readonly Dictionary<string,char> finals = Pairs("ㄱㅅㄳ ㄴㅈㄵ ㄴㅎㄶ ㄹㄱㄺ ㄹㅁㄻ ㄹㅂㄼ ㄹㅅㄽ ㄹㅌㄾ ㄹㅍㄿ ㄹㅎㅀ ㅂㅅㅄ");
    public string Keys = "";
    public string Text { get { return Compose(Keys); } }
    static Dictionary<string,char> Pairs(string s) { var d=new Dictionary<string,char>(); foreach(var p in s.Split(' '))d[p.Substring(0,2)]=p[2];return d; }
    static string Syllable(char l,char v,char t) { return l!='\0' && v!='\0' ? ((char)(0xac00+(L.IndexOf(l)*21+V.IndexOf(v))*28+T.IndexOf(t))).ToString() : l!='\0'?l.ToString():v!='\0'?v.ToString():""; }
    public static string Compose(string keys) {
        var result=new StringBuilder();char l='\0',v='\0',t=' ';
        Action flush=delegate { result.Append(Syllable(l,v,t));l=v='\0';t=' '; };
        foreach(char k in keys) {
            char merged;
            if(V.IndexOf(k)>=0) {
                if(l!='\0' && v!='\0' && t!=' ') {
                    char next=t; char remain=' ';
                    foreach(var p in finals)if(p.Value==t){remain=p.Key[0];next=p.Key[1];break;}
                    t=remain;flush();l=next;v=k;
                } else if(v!='\0') { if(vowels.TryGetValue(""+v+k,out merged))v=merged;else {flush();v=k;} }
                else v=k;
            } else if(L.IndexOf(k)>=0) {
                if(l!='\0' && v!='\0' && t==' ' && T.IndexOf(k)>0)t=k;
                else if(l!='\0' && v!='\0' && t!=' ' && finals.TryGetValue(""+t+k,out merged))t=merged;
                else {flush();l=k;}
            } else {flush();result.Append(k);}
        }
        return result.ToString()+Syllable(l,v,t);
    }
    public void Delete() { if(Keys.Length>0)Keys=Keys.Substring(0,Keys.Length-1); }
}

public static class Osc {
    static byte[] Str(string value){var b=Encoding.UTF8.GetBytes(value);var p=new byte[((b.Length+1+3)/4)*4];Array.Copy(b,p,b.Length);return p;}
    public static byte[] Packet(string text,bool sound) {
        if(String.IsNullOrWhiteSpace(text)||text.Length>144||text.Split('\n').Length>9)throw new ArgumentException("1~144자, 최대 9줄까지 보낼 수 있어요.");
        foreach(char c in text)if(Char.IsControl(c)&&c!='\n')throw new ArgumentException("지원하지 않는 제어 문자입니다.");
        var bytes=new List<byte>();bytes.AddRange(Str("/chatbox/input"));bytes.AddRange(Str(sound?",sTT":",sTF"));bytes.AddRange(Str(text));return bytes.ToArray();
    }
    public static void Send(string text,bool sound,int port) { var b=Packet(text,sound);using(var udp=new UdpClient())udp.Send(b,b.Length,new IPEndPoint(IPAddress.Loopback,port)); }
}

public static class Geometry {
    // Snapshot the complete headset basis, including pitch/roll. Center on its gaze ray.
    public static HmdMatrix34_t InFront(HmdMatrix34_t head,float distance) {
        var panel=head;
        panel.m3=head.m3-distance*head.m2;
        panel.m7=head.m7-distance*head.m6;
        panel.m11=head.m11-distance*head.m10;
        return panel;
    }
    public static bool Hit(HmdMatrix34_t panel,HmdMatrix34_t hand,float width,float height,out float u,out float v) {
        return HitPlane(panel,hand,width,height,out u,out v)&&u>=0&&u<=1&&v>=0&&v<=1;
    }
    public static bool HitPlane(HmdMatrix34_t panel,HmdMatrix34_t hand,float width,float height,out float u,out float v) {
        u=v=0;float dx=-hand.m2,dy=-hand.m6,dz=-hand.m10;
        float denom=dx*panel.m2+dy*panel.m6+dz*panel.m10;
        if(denom>=-0.0001f)return false;
        float t=((panel.m3-hand.m3)*panel.m2+(panel.m7-hand.m7)*panel.m6+(panel.m11-hand.m11)*panel.m10)/denom;
        if(t<0||t>5)return false;
        float px=hand.m3+dx*t-panel.m3,py=hand.m7+dy*t-panel.m7,pz=hand.m11+dz*t-panel.m11;
        u=0.5f+(px*panel.m0+py*panel.m4+pz*panel.m8)/width;
        v=0.5f-(px*panel.m1+py*panel.m5+pz*panel.m9)/height;
        return true;
    }
    static float[] Elements(HmdMatrix34_t m){return new[]{m.m0,m.m1,m.m2,m.m3,m.m4,m.m5,m.m6,m.m7,m.m8,m.m9,m.m10,m.m11};}
    static HmdMatrix34_t Matrix(float[] a){return new HmdMatrix34_t{m0=a[0],m1=a[1],m2=a[2],m3=a[3],m4=a[4],m5=a[5],m6=a[6],m7=a[7],m8=a[8],m9=a[9],m10=a[10],m11=a[11]};}
    public static HmdMatrix34_t Multiply(HmdMatrix34_t first,HmdMatrix34_t second){
        var a=Elements(first);var b=Elements(second);var c=new float[12];
        for(int r=0;r<3;r++)for(int col=0;col<4;col++){for(int k=0;k<3;k++)c[r*4+col]+=a[r*4+k]*b[k*4+col];if(col==3)c[r*4+col]+=a[r*4+3];}return Matrix(c);
    }
    public static HmdMatrix34_t InverseRigid(HmdMatrix34_t value){
        var a=Elements(value);var b=new float[12];for(int r=0;r<3;r++){for(int c=0;c<3;c++)b[r*4+c]=a[c*4+r];for(int c=0;c<3;c++)b[r*4+3]-=b[r*4+c]*a[c*4+3];}return Matrix(b);
    }
}
public class HoldToggle {
    double start=-1;bool fired;
    public bool Update(bool held,double now) {
        if(!held){start=-1;fired=false;return false;}
        if(start<0)start=now;
        if(!fired && now-start>=0.7){fired=true;return true;}return false;
    }
}
}
