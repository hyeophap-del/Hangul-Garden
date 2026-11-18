using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Valve.VR;

namespace HangeulVR {
public static class SelfTest {
    public static void GpuProbe(string output){
        ulong handle=0;CVROverlay overlay=null;
        try {
            var error=EVRInitError.None;var system=OpenVR.Init(ref error,EVRApplicationType.VRApplication_Overlay);
            if(error!=EVRInitError.None||system==null)throw new Exception("OpenVR: "+error);
            overlay=OpenVR.Overlay;
            var result=overlay.CreateOverlay("local.hangeul.gpu.probe."+System.Diagnostics.Process.GetCurrentProcess().Id,"Hangeul hidden GPU test",ref handle);
            if(result!=EVROverlayError.None)throw new Exception("Create: "+result);
            int adapter=0;system.GetDXGIOutputInfo(ref adapter);
            using(var gpu=new D3DTexture(Keyboard.W,Keyboard.H,adapter)){
                IntPtr stable=gpu.Handle;var k=new Keyboard();
                for(int frame=0;frame<300;frame++){
                    k.Input.Keys="GPU 연속 전송 검증 "+frame;
                    using(var b=k.Render(frame%k.Keys.Count,-1,new PointF(frame*3%Keyboard.W,350),null))result=gpu.Submit(overlay,handle,b);
                    if(result!=EVROverlayError.None)throw new Exception("Frame "+frame+": "+result);
                    if(stable!=gpu.Handle)throw new Exception("Texture identity changed");
                    System.Threading.Thread.Sleep(16);
                }
                uint w=0,h=0;result=overlay.GetOverlayTextureSize(handle,ref w,ref h);
                if(result!=EVROverlayError.None||w!=Keyboard.W||h!=Keyboard.H)throw new Exception("Unexpected texture size: "+w+"x"+h+" "+result);
                File.WriteAllText(output,"PASS: 300 consecutive D3D11 texture submissions to running SteamVR, no errors.\r\nStable texture identity. Size: "+w+"x"+h+". Adapter: "+adapter+".\r\nHidden test overlay only; no messages sent, no headset visibility/perceptual test.\r\n");
            }
        } finally {if(overlay!=null&&handle!=0)overlay.DestroyOverlay(handle);OpenVR.Shutdown();}
    }
    static int checks;
    static void InteractionTests(){
        string distancePath=Path.Combine(Path.GetTempPath(),"hangeul-distance-test-"+Guid.NewGuid()+".json");
        try{
            File.WriteAllText(distancePath,"{\"Width\":1.1,\"Opacity\":1}");
            Check(Preferences.Load(distancePath).Distance==.85f,"Legacy preferences retain default summon distance");
            var head=new HmdMatrix34_t{m0=1,m5=1,m10=1,m3=2,m7=1.6f,m11=3};
            var panelAtDistance=Geometry.InFront(head,1.7f);
            var preferences=new Preferences{Distance=Overlay.PanelDistance(head,panelAtDistance)};preferences.Save(distancePath);
            var reopened=new Keyboard();reopened.LoadSettings(distancePath);reopened.CloseReset();
            Check(Math.Abs(reopened.Settings.Distance-1.7f)<.0001f,"Distance survives save, app reload and keyboard close");
            var turnedHead=new HmdMatrix34_t{m2=1,m5=1,m8=-1,m3=-2,m7=2,m11=4};
            var summoned=Geometry.InFront(turnedHead,reopened.Settings.Distance);
            Check(Math.Abs(summoned.m3+3.7f)<.0001f&&summoned.m11==4,"Reopen uses saved distance in current gaze direction");
            Check(!reopened.Settings.GripDistance,"Remembering distance does not enable joystick movement");
            Check(Preferences.NormalizeDistance(float.NaN)==.85f&&Preferences.NormalizeDistance(float.PositiveInfinity)==.85f,"Invalid distances fall back safely");
            Check(Preferences.NormalizeDistance(-1)==.85f&&Preferences.NormalizeDistance(.01f)==.2f&&Preferences.NormalizeDistance(100)==5,"Distance limits validated");
        }finally{if(File.Exists(distancePath))File.Delete(distancePath);}
        Check(!new Preferences().GripDistance,"Grip distance defaults OFF");
        Check(Overlay.AdjustDepth(-1,.1f,.02)==-1,"Stick deadzone prevents drift");
        Check(Overlay.AdjustDepth(-1,1,.02)<-1&&Overlay.AdjustDepth(-1,-1,.02)>-1,"Stick up pushes away and down pulls closer");
        Check(Overlay.AdjustDepth(-3,1,.05)==-3&&Overlay.AdjustDepth(-.2f,-1,.05)==-.2f,"Distance has near and far limits");
        var draft=new Keyboard();draft.Input.InsertText("가나다");draft.ClickDraft(144,80);Check(draft.Input.Caret==1,"Controller click between Korean glyphs places caret");
        draft.Input.InsertText("X");Check(draft.Input.Text=="가X나다","Typing inserts at clicked position");
        draft.Input.Keys="A😀B";draft.Input.PlaceCaret(2,false);Check(draft.Input.Caret==1,"Caret cannot split a surrogate pair");
        draft.Input.Keys="가\n나다";draft.Input.PlaceCaret(0,false);draft.ClickDraft(117,112);Check(draft.Input.Caret==2,"Click follows explicit line break");
        draft.Input.Keys=new string('가',40);draft.Input.PlaceCaret(40,false);draft.ClickDraft(117,112);Check(draft.Input.Caret>0&&draft.Input.Caret<40,"Click follows visual wrapping");
        draft.Input.Keys="";draft.ClickDraft(900,80);Check(draft.Input.Caret==0,"Empty chat click stays at zero");
        string dll=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"openvr_api.dll");
        using(var verified=NativeLibrarySecurity.OpenVerified(dll)){
            Check(verified.Length>0,"Pinned OpenVR DLL accepted");
            bool refused=false;try{using(var writer=new FileStream(dll,FileMode.Open,FileAccess.Write,FileShare.ReadWrite)){} }catch(IOException){refused=true;}
            Check(refused,"Verified DLL cannot be opened for modification");
            // Resolves the signed SDK through the restricted search path without starting VR.
            OpenVR.IsRuntimeInstalled();Check(true,"Restricted OpenVR native load works");
        }
        string invalidDll=Path.Combine(Path.GetTempPath(),"hangeul-invalid-dll-"+Guid.NewGuid()+".bin");
        try{
            File.WriteAllText(invalidDll,"not the approved OpenVR binary");bool refused=false;
            try{using(var verified=NativeLibrarySecurity.OpenVerified(invalidDll)){} }catch(InvalidDataException){refused=true;}
            Check(refused,"Unexpected native library rejected before loading");
        }finally{if(File.Exists(invalidDll))File.Delete(invalidDll);}
        Check(Overlay.GripScale(1,.4f,.6f)==1.5f,"Two-hand spread enlarges panel");
        Check(Overlay.GripScale(1,.4f,.2f)==.55f,"Two-hand pinch respects minimum");
        Check(Overlay.GripScale(1,.4f,2)==1.8f,"Two-hand spread respects maximum");
        var labels=new Keyboard();labels.ActivateAction("ctrl",0);
        Check(labels.Keys.Find(x=>x.Action=="shortcut:c").Label=="복사하기"&&labels.Keys.Find(x=>x.Action=="shortcut:v").Label=="붙여넣기","Ctrl displays function names");
        labels.ActivateAction("shift",0);Check(labels.Keys.Find(x=>x.Action=="shortcut:z").Label=="다시 실행","Ctrl Shift Z label matches action");
        var e=new EditBuffer();foreach(var d in new[]{"left","right","up","down","home","end"})e.Move(d,false,false);Check(e.Caret==0,"Empty cursor moves safe");
        e.InsertKeys("ㅎㅏㄴㄱㅡㄹ");Check(e.Text=="한글"&&e.Caret==2,"Composer caret follows composed syllables");
        e.Move("left",false,false);e.InsertKeys("ㄱㅏ");Check(e.Text=="한가글"&&e.Caret==2,"Middle Hangul composition");
        e.Delete();Check(e.Text=="한ㄱ글","Middle composition backspace");e.Delete();Check(e.Text=="한글"&&e.Caret==1,"Middle composition fully erased");
        e.Move("left",true,false);Check(e.SelectedText=="한","Shift left selection");e.InsertText("새");Check(e.Text=="새글","Selection replacement");
        e.SelectAll();e.InsertText("가\n나다\n라");e.Move("home",false,true);e.Move("down",false,false);Check(e.Caret==2,"Arrow down logical line");e.Move("right",false,false);e.Move("up",false,false);Check(e.Caret==1,"Arrow up preserves column");
        e.Keys="A😀B";e.Move("end",false,true);e.Move("left",false,false);Check(e.Caret==3,"Before trailing B");e.Delete();Check(e.Text=="AB"&&e.Caret==1,"Delete whole emoji");e.Move("home",false,true);e.DeleteForward();Check(e.Text=="B","Forward delete");
        string clip="";var k=new Keyboard();k.ReadClipboard=()=>clip;k.WriteClipboard=s=>clip=s;
        k.Input.Keys="한글";k.ActivateAction("ctrl",0);k.ActivateAction("shortcut:a",0);Check(k.Input.SelectedText=="한글"&&!k.Ctrl,"Ctrl A selects all");
        k.ActivateAction("shortcut:c",0);Check(clip=="한글","Copy selected text");k.ActivateAction("shortcut:x",0);Check(k.Input.Text=="","Cut deletes selection");k.ActivateAction("shortcut:v",0);Check(k.Input.Text=="한글","Paste clipboard");
        k.ActivateAction("shortcut:z",0);Check(k.Input.Text=="","Undo paste");k.ActivateAction("shortcut:y",0);Check(k.Input.Text=="한글","Redo paste");
        clip=new String('가',145);k.ActivateAction("shortcut:v",0);Check(k.Input.Text=="한글","Oversized paste rejected atomically");
        k.Input.SelectAll();k.WriteClipboard=s=>{throw new Exception("clipboard busy");};k.Ctrl=true;k.ActivateAction("shortcut:x",0);Check(k.Input.Text=="한글"&&!k.Ctrl,"Failed cut preserves selection and releases Ctrl");
        k.Shift=k.Caps=k.Ctrl=true;k.Mode=1;k.CloseReset();Check(k.Input.Text==""&&!k.Shift&&!k.Caps&&!k.Ctrl&&k.Input.SelectionLength==0&&k.Undo.Count==0&&k.Redo.Count==0&&k.Mode==0,"Close resets editing session");
        int initial=k.Settings.Phrases.Count;k.Input.Keys="새 단축어";k.ActivateAction("phrase-add",0);Check(k.Settings.Phrases.Count==initial+1,"Add phrase");
        k.ActivateAction("phrase-add",0);Check(k.Settings.Phrases.Count==initial+1,"No duplicate phrase");k.ActivateAction("phrase-delete",0);k.ActivateAction("phrase-index:"+initial,0);Check(k.Settings.Phrases.Count==initial&&k.Input.Text=="새 단축어","Delete phrase without typing");
        k.SetSlider("opacity-slider",-1);Check(k.Opacity==.25f,"Opacity lower bound");k.SetSlider("size-slider",2);Check(k.Width==1.8f,"Size upper bound");
        string path=Path.Combine(Path.GetTempPath(),"hangeul-settings-test-"+Guid.NewGuid()+".json");
        try{k.Settings.Phrases.Clear();k.Settings.Phrases.Add("저장 확인");k.Settings.Save(path);var loaded=Preferences.Load(path);Check(loaded.Phrases.Count==1&&loaded.Phrases[0]=="저장 확인"&&loaded.Width==1.8f&&loaded.Opacity==.25f,"Preferences round trip");loaded.Phrases.Clear();loaded.Save(path);Check(Preferences.Load(path).Phrases.Count==0,"Atomic update preserves empty phrase list");}finally{if(File.Exists(path))File.Delete(path);}
        var chord=new DoubleTapChord();Check(!chord.Update(false,true,0),"Unmodified trigger cannot toggle");chord.Update(false,false,.1);Check(!chord.Update(true,true,.2),"First chord tap");chord.Update(true,false,.3);Check(chord.Update(true,true,.4),"Second chord tap toggles");Check(!chord.Update(true,true,.5),"Holding trigger does not retoggle");
        var press=new PressState();Check(press.Update(true,"key:ㄱ",0)=="key:ㄱ","Initial click");Check(press.Update(true,"key:ㄱ",1)==null,"No character repeat while held");press.Update(false,null,1.1);Check(press.Update(true,"key:ㄱ",1.12)=="key:ㄱ","Fast release and re-press accepted");press.Suppress(false);Check(press.Update(true,"back",2)=="back"&&press.Update(true,"back",2.46)=="back","Hold backspace repeats");Check(press.Update(true,null,3)==null,"Repeat cancels off key");
        var hand=new HmdMatrix34_t{m0=1,m5=1,m10=1,m3=.3f,m7=1,m11=.5f};var panel=Geometry.InFront(hand,.8f);var relative=Geometry.Multiply(Geometry.InverseRigid(hand),panel);hand.m3+=.7f;var moved=Geometry.Multiply(hand,relative);Check(Math.Abs(moved.m3-panel.m3-.7f)<.001&&Math.Abs(moved.m11-panel.m11)<.001,"Controller grab preserves panel offset");
        using(var rx=new UdpClient(new IPEndPoint(IPAddress.Loopback,0))){
            rx.Client.ReceiveTimeout=2000;var pending=new Keyboard{Port=((IPEndPoint)rx.Client.LocalEndPoint).Port,LastSent=10};pending.Input.Keys="취소할 전송";pending.ActivateAction("send",10.1);pending.CloseReset();Check(!pending.UpdateLive(20)&&rx.Available==0,"Close cancels queued manual and live send");
            pending.Input.Keys="보낼 문장";pending.LastSent=30;pending.ActivateAction("send",30.1);Check(pending.UpdateLive(31.6),"Queued completion eventually sends");Check(ReceiveText(rx).Contains("보낼 문장\0")&&pending.Input.Text=="","Queued completion clears only after send");
        }
    }
    public static void RenderBenchmark(string output){
        const int count=180;var k=new Keyboard();var clock=new System.Diagnostics.Stopwatch();
        using(var cache=new KeyboardRenderer()){
            cache.Draw(k,-1,-1,null,null);
            clock.Start();for(int i=0;i<count;i++)using(var b=k.Render(10,-1,new PointF(50+i,320),null)){}clock.Stop();double full=clock.Elapsed.TotalMilliseconds/count;
            clock.Restart();for(int i=0;i<count;i++)cache.Draw(k,10,-1,new PointF(50+i,320),null);clock.Stop();double cached=clock.Elapsed.TotalMilliseconds/count;
            using(var wait=new FrameWaiter()){
                clock.Restart();double deadline=0;
                for(int i=0;i<count;i++){cache.Draw(k,10,-1,new PointF(50+i,320),null);deadline+=1.0/90;wait.Wait(deadline-clock.Elapsed.TotalSeconds);}
                clock.Stop();double cadence=count/clock.Elapsed.TotalSeconds;
                File.WriteAllText(output,String.Format("Full redraw: {0:F3} ms/frame\r\nCached pointer: {1:F3} ms/frame\r\nCPU rendering speedup: {2:F1}x\r\n90 Hz paced CPU loop: {3:F1} iterations/s\r\nExcludes GPU submit, tracking, and headset compositor. Not measured in-headset FPS.\r\n",full,cached,full/cached,cadence));
            }
        }
    }
    static void RendererTests(){
        var k=new Keyboard();
        using(var cache=new KeyboardRenderer()){
            cache.Draw(k,-1,-1,null,null);int rebuilt=cache.Rebuilds;
            for(int i=0;i<20;i++)cache.Draw(k,-1,-1,new PointF(i*4,350),null);
            Check(cache.Rebuilds==rebuilt,"Pointer motion reuses background");
            k.Input.Keys="한글";cache.Draw(k,-1,-1,null,null);Check(cache.Rebuilds==++rebuilt,"Draft refreshes cache");
            k.Input.Move("left",false,false);cache.Draw(k,-1,-1,null,null);Check(cache.Rebuilds==++rebuilt,"Caret motion refreshes cache without changing text");
            k.Input.Move("left",true,false);cache.Draw(k,-1,-1,null,null);Check(cache.Rebuilds==++rebuilt,"Selection refreshes cache without changing text");
            k.Status="상태 변경";cache.Draw(k,-1,-1,null,null);Check(cache.Rebuilds==++rebuilt,"Status refreshes cache");
            k.Shift=true;k.Layout();cache.Draw(k,-1,-1,null,null);Check(cache.Rebuilds==++rebuilt,"Layout refreshes cache");
            cache.Draw(k,5,-1,null,null);Check(cache.Rebuilds==++rebuilt,"Hover refreshes cache");
            var actual=cache.Draw(k,5,-1,new PointF(150.25f,330.5f),new PointF(750,450));
            using(var expected=k.Render(5,-1,new PointF(150.25f,330.5f),new PointF(750,450))){
                for(int y=0;y<Keyboard.H;y+=7)for(int x=0;x<Keyboard.W;x+=7)Check(actual.GetPixel(x,y)==expected.GetPixel(x,y),"Cached image matches full redraw");
            }
        }
    }
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void PlacementAndLayoutTests(){
        foreach(double pitch in new[]{-Math.PI/2,-.7,0,.7,Math.PI/2})foreach(double yaw in new[]{0,1.2,Math.PI})foreach(double roll in new[]{0,.3}){
            float cp=(float)Math.Cos(pitch),sp=(float)Math.Sin(pitch),cy=(float)Math.Cos(yaw),sy=(float)Math.Sin(yaw),cr=(float)Math.Cos(roll),sr=(float)Math.Sin(roll);
            var head=new HmdMatrix34_t{m0=cy*cr+sy*sp*sr,m1=-cy*sr+sy*sp*cr,m2=sy*cp,m3=1,m4=cp*sr,m5=cp*cr,m6=-sp,m7=1.7f,m8=-sy*cr+cy*sp*sr,m9=sy*sr+cy*sp*cr,m10=cy*cp,m11=2};
            var panel=Geometry.InFront(head,.85f);float u,v;
            Check(Geometry.Hit(panel,head,1,.6f,out u,out v)&&Math.Abs(u-.5f)<.001&&Math.Abs(v-.5f)<.001,"Gaze centered for pitch/yaw/roll");
            Check(Math.Abs(panel.m7-(head.m7-.85f*head.m6))<.001,"Vertical gaze placement");
            var hand=head;hand.m3+=panel.m1*.15f;hand.m7+=panel.m5*.15f;hand.m11+=panel.m9*.15f;
            Check(Geometry.Hit(panel,hand,1,.6f,out u,out v)&&Math.Abs(v-.25f)<.001,"Tilted panel pointer aligns");
        }
        var k=new Keyboard();
        foreach(int mode in new[]{0,1})foreach(bool shift in new[]{false,true})foreach(bool caps in new[]{false,true}){
            k.Mode=mode;k.Shift=shift;k.Caps=caps;k.Layout();
            for(int i=0;i<k.Keys.Count;i++){
                var key=k.Keys[i];Check(key.Box.Left>=0&&key.Box.Right<=Keyboard.W&&key.Box.Top>=0&&key.Box.Bottom<=Keyboard.H,"All layout states within texture");
                Check(k.Hit(key.Box.X+key.Box.Width/2,key.Box.Y+key.Box.Height/2)==i,"Unique key hit");
                for(int j=i+1;j<k.Keys.Count;j++)Check(!key.Box.IntersectsWith(k.Keys[j].Box),"No overlapping keys");
            }
            Check(k.Keys.FindAll(x=>x.Action=="shift").Count==2,"Both Shift keys present");
        }
        k.Mode=0;k.Shift=false;k.Caps=false;k.Layout();
        var back=k.Keys.Find(x=>x.Action=="back");Check(back.Box.Y==240+Keyboard.KeyboardOffset&&back.Box.X>900,"Backspace in top right");
        var enter=k.Keys.Find(x=>x.Label=="Enter ↵");Check(enter.Box.Y==368+Keyboard.KeyboardOffset&&enter.Action=="send","Enter at home row right");
        using(var split=k.Render(-1,-1,null,null)){
            Check(split.GetPixel(Keyboard.W/2,Keyboard.ChatHeight+Keyboard.PanelGap/2).A==0,"Panel gap is transparent");
            Check(!Keyboard.IsPanel(Keyboard.W/2,Keyboard.ChatHeight+Keyboard.PanelGap/2)&&k.HitTyping(Keyboard.W/2,Keyboard.ChatHeight+Keyboard.PanelGap/2)==-1,"Panel gap cannot start grab or type");
        }
        float q=k.Keys.Find(x=>x.Action=="key:ㅂ").Box.X,a=k.Keys.Find(x=>x.Action=="key:ㅁ").Box.X,z=k.Keys.Find(x=>x.Action=="key:ㅋ").Box.X;Check(q<a&&a<z,"Standard staggered rows");
        k.Shift=true;k.Layout();Check(k.Keys.Exists(x=>x.Action=="key:!")&&k.Keys.Exists(x=>x.Action=="key::")&&k.Keys.Exists(x=>x.Action=="key:?"),"Shift punctuation");
        Check(k.Keys.Find(x=>x.Label=="Enter ↵").Action=="key:\n","Shift Enter newline");
        k.Activate(k.Keys.FindIndex(x=>x.Label=="Enter ↵"),1);Check(k.Input.Text=="\n"&&!k.Shift,"Shift Enter inserts newline, no send");
    }
    static string ReceiveText(UdpClient rx){IPEndPoint remote=null;return Encoding.UTF8.GetString(rx.Receive(ref remote));}
    static void LiveTests(){
        using(var rx=new UdpClient(new IPEndPoint(IPAddress.Loopback,0))){
            rx.Client.ReceiveTimeout=2000;int port=((IPEndPoint)rx.Client.LocalEndPoint).Port;
            var k=new Keyboard{Port=port,Sound=true};
            Check(k.Live,"Live enabled by default");
            k.Input.Keys="ㅎ";Check(!k.UpdateLive(0),"First edit coalescing delay");
            Check(k.UpdateLive(.3),"Automatic first send");string packet=ReceiveText(rx);
            Check(packet.Contains("ㅎ\0")&&packet.Contains(",sTF\0"),"Live drafts always silent");
            k.Input.Keys="ㅎㅏ";Check(!k.UpdateLive(1),"Rate limit intermediate draft");
            k.Input.Keys="ㅎㅏㄴ";Check(!k.UpdateLive(1.5),"Coalesce newest draft");
            Check(k.UpdateLive(2),"Continuous typing not starved");Check(ReceiveText(rx).Contains("한\0"),"Only newest composed text sent");
            k.Input.Delete();k.UpdateLive(2.1);Check(k.UpdateLive(3.6),"Deletion updated live");Check(ReceiveText(rx).Contains("하\0"),"Deleted text payload");
            k.Activate(k.Keys.FindIndex(x=>x.Action=="live"),3.7);k.Input.Keys="비공개";Check(!k.UpdateLive(10)&&rx.Available==0,"Live off prevents pending sends");
            k.Activate(k.Keys.FindIndex(x=>x.Action=="live"),10);k.UpdateLive(10);Check(k.UpdateLive(10.3),"Live on publishes current draft");Check(ReceiveText(rx).Contains("비공개\0"),"Toggle on uses newest draft");
            k.Activate(k.Keys.FindIndex(x=>x.Action=="send"),10.4);Check(k.Input.Text==""&&rx.Available==0,"Finish already-sent draft without duplicate");
            k.Input.Keys="다음 문장";k.UpdateLive(10.5);Check(!k.UpdateLive(10.8),"Manual finish preserves shared cooldown");
            Check(k.UpdateLive(12),"Next draft sent");Check(ReceiveText(rx).Contains("다음 문장\0"),"Next message payload");
            k.Input.Keys="";Check(!k.UpdateLive(14)&&rx.Available==0,"Empty drafts not transmitted");
            k.Input.Keys="재시도";k.Port=-1;k.UpdateLive(14.1);Check(k.UpdateLive(14.4)&&k.Input.Text=="재시도","Failure preserves draft");
            k.Port=port;k.Input.Keys="재시도 최신";Check(!k.UpdateLive(15),"Failure backoff");
            Check(k.UpdateLive(16.5),"Automatic recovery");Check(ReceiveText(rx).Contains("재시도 최신\0"),"Retry uses latest draft");
            Check(!k.UpdateLive(20)&&rx.Available==0,"Unchanged text not repeatedly sent");
        }
    }
    public static void Run(string output){
        var inputs=new[]{"ㅇㅏㄴㄴㅕㅇㅎㅏㅅㅔㅇㅛ","ㄱㅏㅂㅅㅏ","ㄷㅏㄹㄱㅏ","ㄱㅗㅏㄴ","ㄲㅗㅊ"};
        var expected=new[]{"안녕하세요","갑사","달가","관","꽃"};
        for(int i=0;i<inputs.Length;i++)Check(Hangul.Compose(inputs[i])==expected[i],"Hangul "+expected[i]);
        string[] split={"","ㄱ","ㄲ","ㄱㅅ","ㄴ","ㄴㅈ","ㄴㅎ","ㄷ","ㄹ","ㄹㄱ","ㄹㅁ","ㄹㅂ","ㄹㅅ","ㄹㅌ","ㄹㅍ","ㄹㅎ","ㅁ","ㅂ","ㅂㅅ","ㅅ","ㅆ","ㅇ","ㅈ","ㅊ","ㅋ","ㅌ","ㅍ","ㅎ"};
        for(int l=0;l<19;l++)for(int v=0;v<21;v++)for(int t=0;t<28;t++)Check(Hangul.Compose(""+Hangul.L[l]+Hangul.V[v]+split[t])==((char)(0xac00+(l*21+v)*28+t)).ToString(),"Syllable "+l+","+v+","+t);
        var h=new Hangul{Keys="ㄱㅗㅏㄹㄱ"};foreach(var e in new[]{"괄","과","고","ㄱ",""}){h.Delete();Check(h.Text==e,"Delete "+e);}
        var head=new HmdMatrix34_t{m0=1,m5=1,m10=1,m7=1.7f};var panel=Geometry.InFront(head,1);
        Check(Math.Abs(panel.m11+1)<0.001f,"Panel distance");float u,vv;
        var hand=head;hand.m7=panel.m7;Check(Geometry.Hit(panel,hand,1,0.6f,out u,out vv)&&Math.Abs(u-.5f)<.001&&Math.Abs(vv-.5f)<.001,"Center ray");
        hand.m7+=.15f;Check(Geometry.Hit(panel,hand,1,.6f,out u,out vv)&&Math.Abs(vv-.25f)<.001,"Top ray");
        hand.m3=2;Check(!Geometry.Hit(panel,hand,1,.6f,out u,out vv),"Miss ray");
        hand=head;hand.m10=-1;Check(!Geometry.Hit(panel,hand,1,.6f,out u,out vv),"Backwards ray");
        var hold=new HoldToggle();Check(!hold.Update(true,0),"Hold starts");Check(!hold.Update(true,.6),"Hold waits");Check(hold.Update(true,.8),"Hold fires");Check(!hold.Update(true,1.8),"Hold only once");hold.Update(false,2);Check(!hold.Update(true,3),"Hold reset");Check(hold.Update(true,4),"Hold second fire");
        using(var rx=new UdpClient(new IPEndPoint(IPAddress.Loopback,0))){
            rx.Client.ReceiveTimeout=2000;Osc.Send("한글 테스트",false,((IPEndPoint)rx.Client.LocalEndPoint).Port);IPEndPoint remote=null;var packet=rx.Receive(ref remote);
            Check(packet.Length%4==0,"OSC alignment");string raw=Encoding.UTF8.GetString(packet);Check(raw.StartsWith("/chatbox/input\0"),"OSC address");Check(raw.Contains(",sTF\0"),"OSC bool tags");Check(raw.Contains("한글 테스트\0"),"OSC UTF8");
        }
        bool rejected=false;try{Osc.Packet(new String('가',145),false);}catch(ArgumentException){rejected=true;}Check(rejected,"Length limit");
        var k=new Keyboard();foreach(var key in k.Keys){Check(key.Box.Left>=0&&key.Box.Top>=0&&key.Box.Right<=Keyboard.W&&key.Box.Bottom<=Keyboard.H,"Key bounds "+key.Label);Check(k.Hit(key.Box.Left+key.Box.Width/2,key.Box.Top+key.Box.Height/2)>=0,"Key hit "+key.Label);}
        foreach(char c in "ㅎㅏㄴㄱㅡㄹ")k.Activate(k.Keys.FindIndex(x=>x.Action=="key:"+c),10);
        Check(k.Input.Text=="한글","Controller selection composes Hangul");
        k.Activate(k.Keys.FindIndex(x=>x.Action=="back"),11);Check(k.Input.Text=="한그","UI delete");
        k.Activate(k.Keys.FindIndex(x=>x.Action=="ctrl"),12);k.Activate(k.Keys.FindIndex(x=>x.Action=="shortcut:z"),12);Check(k.Input.Text=="한글","UI Ctrl Z undo");
        using(var rx=new UdpClient(new IPEndPoint(IPAddress.Loopback,0))){
            rx.Client.ReceiveTimeout=2000;k.Port=((IPEndPoint)rx.Client.LocalEndPoint).Port;
            k.Activate(k.Keys.FindIndex(x=>x.Action=="send"),20);IPEndPoint remote=null;
            Check(Encoding.UTF8.GetString(rx.Receive(ref remote)).Contains("한글\0"),"Send button UDP payload");Check(k.Input.Text=="","Clear after send");
            k.Input.Keys="중복";k.Activate(k.Keys.FindIndex(x=>x.Action=="send"),20.2);Check(k.Input.Text=="중복","Cooldown preserves draft");Check(rx.Available==0,"Cooldown prevents UDP send");
        }
        k.Input.Keys="";k.Activate(k.Keys.FindIndex(x=>x.Action=="shift"),30);k.Activate(k.Keys.FindIndex(x=>x.Action=="key:ㄲ"),30);Check(k.Input.Text=="ㄲ"&&!k.Shift,"One shot shifted key");
        using(var b=k.Render(-1,-1,null,null)){Check(b.Width==Keyboard.W&&b.Height==Keyboard.H,"Render");}
        LiveTests();PlacementAndLayoutTests();RendererTests();InteractionTests();UpdateTests.Run(Check);
        File.WriteAllText(output,"PASS: "+checks+" assertions\r\nHangul (11,172 syllables), caret/selection, clipboard shortcuts, undo/redo, close reset, phrase persistence, sliders, gestures, press/repeat, drag transform, ray geometry, OSC UDP loopback, layout, live coalescing/rate limit/mute/retry and rendering.\r\nHardware/SteamVR integration NOT tested by this command.\r\n");
    }
}
}
