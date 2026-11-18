using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using Valve.VR;

namespace HangeulVR {
public sealed class Overlay : IDisposable {
    public readonly SteamVrStartup Startup=new SteamVrStartup();
    public Keyboard Keyboard=new Keyboard();public string Info="SteamVR 연결 대기";
    public bool Connected {get{return system!=null;}}public bool Visible;public int TargetHz=90;
    readonly KeyboardRenderer renderer=new KeyboardRenderer();
    CVRSystem system;CVROverlay overlay;CVRInput input;D3DTexture texture;
    ulong handle,actionSet,click,grip,pose,haptic,pull,menu,distanceAxis;
    readonly float[] sticks=new float[2];double lastCaptureTime;
    readonly ulong[] hands=new ulong[2];
    readonly bool[] gripEdges=new bool[2],grips=new bool[2],pressed=new bool[2],tracked=new bool[2];
    readonly PressState[] presses={new PressState(),new PressState()};
    readonly InputDigitalActionData_t[] clicks=new InputDigitalActionData_t[2];
    readonly HmdMatrix34_t[] handPoses=new HmdMatrix34_t[2];
    readonly string[] actions=new string[2];
    readonly int[] hover={-1,-1};readonly PointF?[] points=new PointF?[2];
    readonly VRActiveActionSet_t[] sets=new VRActiveActionSet_t[1];
    readonly TrackedDevicePose_t[] poses=new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
    readonly DoubleTapChord chord=new DoubleTapChord();readonly HoldToggle hold=new HoldToggle();
    readonly Stopwatch clock=Stopwatch.StartNew();
    HmdMatrix34_t transform,captureRelative;readonly ETrackingUniverseOrigin origin=ETrackingUniverseOrigin.TrackingUniverseStanding;
    int captureHand=-1;string captureAction;RectangleF captureBox;float captureWidth,captureRadius,dualDistance;
    double nextConnect,nextRender,nextTextureRetry;int textureFailures;bool placed,dirty=true;
    public Overlay(){Keyboard.Command=Command;}
    static void Check(EVROverlayError e){if(e!=EVROverlayError.None)throw new InvalidOperationException("SteamVR overlay: "+e);}
    static void Check(EVRInputError e){if(e!=EVRInputError.None)throw new InvalidOperationException("SteamVR input: "+e);}
    void Init(){
        if(!ProcessExists("vrserver")){Info="SteamVR을 실행하고 헤드셋을 연결해 주세요.";return;}
        var error=EVRInitError.None;system=OpenVR.Init(ref error,EVRApplicationType.VRApplication_Overlay);
        if(error!=EVRInitError.None||system==null)throw new InvalidOperationException(OpenVR.GetStringForHmdError(error));
        overlay=OpenVR.Overlay;input=OpenVR.Input;if(overlay==null||input==null)throw new InvalidOperationException("SteamVR 인터페이스를 찾을 수 없습니다.");
        Check(overlay.CreateOverlay("local.hangeul.vr.keyboard","한글뜰 VR 키보드",ref handle));Check(overlay.SetOverlayWidthInMeters(handle,Keyboard.Width));Check(overlay.SetOverlayAlpha(handle,Keyboard.Opacity));
        int adapter=0;system.GetDXGIOutputInfo(ref adapter);texture=new D3DTexture(Keyboard.W,Keyboard.H,adapter);
        var prop=ETrackedPropertyError.TrackedProp_Success;float hz=system.GetFloatTrackedDeviceProperty(0,ETrackedDeviceProperty.Prop_DisplayFrequency_Float,ref prop);TargetHz=prop==ETrackedPropertyError.TrackedProp_Success&&hz>=60&&hz<=144?(int)Math.Round(hz):90;
        Check(overlay.SetOverlayInputMethod(handle,VROverlayInputMethod.None));
        Check(input.SetActionManifestPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"actions.json")));
        Check(input.GetActionSetHandle("/actions/keyboard",ref actionSet));Check(input.GetActionHandle("/actions/keyboard/in/click",ref click));Check(input.GetActionHandle("/actions/keyboard/in/grip",ref grip));Check(input.GetActionHandle("/actions/keyboard/in/pose",ref pose));Check(input.GetActionHandle("/actions/keyboard/in/pull",ref pull));Check(input.GetActionHandle("/actions/keyboard/in/menu",ref menu));Check(input.GetActionHandle("/actions/keyboard/out/haptic",ref haptic));
        Check(input.GetInputSourceHandle("/user/hand/left",ref hands[0]));Check(input.GetInputSourceHandle("/user/hand/right",ref hands[1]));
        Check(input.GetActionHandle("/actions/keyboard/in/distance",ref distanceAxis));
        placed=false;Visible=false;dirty=true;textureFailures=0;nextTextureRetry=nextRender=0;Info="SteamVR 연결됨 · 헤드셋 추적 대기";
    }
    static bool ProcessExists(string name){var ps=Process.GetProcessesByName(name);bool found=ps.Length>0;foreach(var p in ps)p.Dispose();return found;}
    public void Reconnect(){Disconnect();nextConnect=0;}
    public void Tick(){
        double now=clock.Elapsed.TotalSeconds;
        try {
            if(system==null){if(now>=nextConnect){nextConnect=now+3;Init();}return;}
            var e=new VREvent_t();while(system.PollNextEvent(ref e,(uint)Marshal.SizeOf(typeof(VREvent_t))))if(e.eventType==(uint)EVREventType.VREvent_Quit){system.AcknowledgeQuit_Exiting();Disconnect();Info="SteamVR 종료 · 재연결 대기";nextConnect=now+5;return;}
            system.GetDeviceToAbsoluteTrackingPose(origin,0,poses);if(!placed&&poses[0].bPoseIsValid&&poses[0].bDeviceIsConnected)Show();
            sets[0]=new VRActiveActionSet_t{ulActionSet=actionSet};Check(input.UpdateActionState(sets,(uint)Marshal.SizeOf(typeof(VRActiveActionSet_t))));bool dashboard=overlay.IsDashboardVisible();int active=0;
            // Sample both hands before any key mutates layout/Shift/Ctrl state.
            for(int i=0;i<2;i++){
                var gd=new InputDigitalActionData_t();var cd=new InputDigitalActionData_t();var analog=new InputAnalogActionData_t();var pd=new InputPoseActionData_t();
                Check(input.GetDigitalActionData(grip,ref gd,(uint)Marshal.SizeOf(typeof(InputDigitalActionData_t)),hands[i]));Check(input.GetDigitalActionData(click,ref cd,(uint)Marshal.SizeOf(typeof(InputDigitalActionData_t)),hands[i]));
                Check(input.GetAnalogActionData(pull,ref analog,(uint)Marshal.SizeOf(typeof(InputAnalogActionData_t)),hands[i]));Check(input.GetPoseActionDataRelativeToNow(pose,origin,0,ref pd,(uint)Marshal.SizeOf(typeof(InputPoseActionData_t)),hands[i]));
                clicks[i]=cd;bool nextGrip=gd.bActive&&gd.bState;gripEdges[i]=nextGrip&&!grips[i];grips[i]=nextGrip;tracked[i]=pd.bActive&&pd.pose.bPoseIsValid&&pd.pose.bDeviceIsConnected;
                if(tracked[i])active++;handPoses[i]=pd.pose.mDeviceToAbsoluteTracking;
                pressed[i]=analog.bActive?analog.x>=(pressed[i] ? .15f : .35f):cd.bActive&&cd.bState;
                var stick=new InputAnalogActionData_t();Check(input.GetAnalogActionData(distanceAxis,ref stick,(uint)Marshal.SizeOf(typeof(InputAnalogActionData_t)),hands[i]));sticks[i]=stick.bActive?stick.y:0;
            }
            bool toggled=false;
            if(dashboard)chord.Reset();else toggled=chord.Update(grips[0],pressed[1],now);
            if(toggled){if(Visible)Hide();else Show();for(int i=0;i<2;i++)presses[i].Suppress(pressed[i]);}
            if(captureAction=="dual"){
                if(!Visible||dashboard||!tracked[0]||!tracked[1])EndCapture();
                else if(!grips[0]||!grips[1]){
                    EndCapture();
                    if(grips[0]||grips[1])BeginCapture(grips[0]?0:1,new Key(0,0,0,0,"","drag"));
                }else UpdateDualScale();
            }else if(captureHand>=0){
                if(!Visible||dashboard||!tracked[captureHand]||!(captureAction=="drag"?grips[captureHand]:pressed[captureHand]))EndCapture();else UpdateCapture();
            }
            // A grip already held while summoning must be released before grabbing.
            if(Visible&&!dashboard&&!toggled&&captureHand<0){
                for(int i=0;i<2;i++){
                    float gu,gv;
                    if(gripEdges[i]&&tracked[i]&&Geometry.Hit(transform,handPoses[i],Keyboard.Width,Keyboard.Width*Keyboard.H/Keyboard.W,out gu,out gv)&&HangeulVR.Keyboard.IsPanel(gu*HangeulVR.Keyboard.W,gv*HangeulVR.Keyboard.H)){
                        BeginCapture(i,new Key(0,0,0,0,"","drag"));break;
                    }
                }
            }
            if(captureAction=="drag"&&grips[0]&&grips[1]&&tracked[0]&&tracked[1]&&(gripEdges[0]||gripEdges[1])&&HandDistance()>=.08f){
                captureAction="dual";dualDistance=HandDistance();captureWidth=Keyboard.Width;
            }
            for(int i=0;i<2;i++){
                int hit=-1;PointF? point=null;float u,v;actions[i]=null;
                bool reserved=i==1&&grips[0];
                if(Visible&&!dashboard&&tracked[i]&&Geometry.Hit(transform,handPoses[i],Keyboard.Width,Keyboard.Width*Keyboard.H/Keyboard.W,out u,out v)){
                    point=new PointF(u*Keyboard.W,v*Keyboard.H);hit=Keyboard.HitTyping(point.Value.X,point.Value.Y);
                }
                if(hover[i]!=hit||points[i]!=point)dirty=true;hover[i]=hit;points[i]=point;
                if(!Visible||dashboard||reserved||toggled||captureHand>=0){presses[i].Suppress(pressed[i]);continue;}
                int pressHit=hit;
                // Use the pose at the digital press when available, reducing trigger squeeze drift.
                if(pressed[i]&&!presses[i].Held&&clicks[i].bActive&&clicks[i].bChanged&&clicks[i].bState){
                    var eventPose=new InputPoseActionData_t();
                    if(input.GetPoseActionDataRelativeToNow(pose,origin,Math.Max(-.08f,clicks[i].fUpdateTime),ref eventPose,(uint)Marshal.SizeOf(typeof(InputPoseActionData_t)),hands[i])==EVRInputError.None&&eventPose.pose.bPoseIsValid&&Geometry.Hit(transform,eventPose.pose.mDeviceToAbsoluteTracking,Keyboard.Width,Keyboard.Width*Keyboard.H/Keyboard.W,out u,out v))pressHit=Keyboard.HitTyping(u*Keyboard.W,v*Keyboard.H);
                }
                string action=pressHit>=0?Keyboard.Keys[pressHit].Action:null;
                actions[i]=presses[i].Update(pressed[i],action,now);
                if(actions[i]=="caret"&&points[i].HasValue){Keyboard.ClickDraft(points[i].Value.X,points[i].Value.Y);actions[i]=null;dirty=true;}
                if(actions[i]=="resize-drag"||(actions[i]!=null&&actions[i].EndsWith("-slider"))){BeginCapture(i,Keyboard.Keys[pressHit]);actions[i]=null;}
            }
            for(int i=0;i<2;i++)if(actions[i]!=null&&Visible){Keyboard.ActivateAction(actions[i],now);dirty=true;input.TriggerHapticVibrationAction(haptic,0,.025f,120,.25f,hands[i]);}
            if(Visible&&!dashboard&&Keyboard.UpdateLive(now))dirty=true;
            Info=String.Format("SteamVR 연결 · 컨트롤러 {0}/2 · {1}\r\n호출: {2}\r\nOSC 127.0.0.1:{3} · 갱신 목표 {4}Hz",active,Visible?"표시 중":"닫힘",Keyboard.HotkeyLabel,Keyboard.Port,TargetHz);
            if(textureFailures>0)Info+=" · 이미지 재시도 중";
            if(dirty&&Visible&&now>=nextRender&&now>=nextTextureRetry){if(Upload())dirty=false;nextRender+=1.0/TargetHz;if(nextRender<now)nextRender=now+1.0/TargetHz;}
        }catch(Exception ex){Info="연결 오류: "+ex.Message;Log(ex);Disconnect();nextConnect=now+5;}
    }
    float HandDistance(){
        float x=handPoses[0].m3-handPoses[1].m3,y=handPoses[0].m7-handPoses[1].m7,z=handPoses[0].m11-handPoses[1].m11;
        return (float)Math.Sqrt(x*x+y*y+z*z);
    }
    internal static float GripScale(float width,float startDistance,float distance){return (float)Math.Round(Math.Max(.55,Math.Min(1.8,width*distance/Math.Max(.08f,startDistance))),2);}
    void UpdateDualScale(){
        float next=GripScale(captureWidth,dualDistance,HandDistance());
        if(next==Keyboard.Width)return;
        Keyboard.Width=next;Check(overlay.SetOverlayWidthInMeters(handle,next));Keyboard.Layout();dirty=true;
    }
    void BeginCapture(int hand,Key key){
        lastCaptureTime=clock.Elapsed.TotalSeconds;
        captureHand=hand;captureAction=key.Action;captureBox=key.Box;captureWidth=Keyboard.Width;
        captureRelative=Geometry.Multiply(Geometry.InverseRigid(handPoses[hand]),transform);
        float u,v;captureRadius=1;
        if(Geometry.HitPlane(transform,handPoses[hand],captureWidth,captureWidth*Keyboard.H/Keyboard.W,out u,out v))captureRadius=(float)Math.Sqrt((u-.5f)*(u-.5f)+(v-.5f)*(v-.5f));
        UpdateCapture();
    }
    void UpdateCapture(){
        if(captureAction=="drag"){
            double now=clock.Elapsed.TotalSeconds,dt=Math.Max(0,Math.Min(.05,now-lastCaptureTime));lastCaptureTime=now;
            if(Keyboard.Settings.GripDistance)captureRelative.m11=AdjustDepth(captureRelative.m11,sticks[captureHand],dt);
            transform=Geometry.Multiply(handPoses[captureHand],captureRelative);
            if(poses[0].bPoseIsValid&&poses[0].bDeviceIsConnected)Keyboard.Settings.Distance=PanelDistance(poses[0].mDeviceToAbsoluteTracking,transform);
            Check(overlay.SetOverlayTransformAbsolute(handle,origin,ref transform));return;
        }
        float u,v;
        float width=captureWidth;
        if(!Geometry.HitPlane(transform,handPoses[captureHand],width,width*Keyboard.H/Keyboard.W,out u,out v))return;
        if(captureAction=="resize-drag"){
            float radius=(float)Math.Sqrt((u-.5f)*(u-.5f)+(v-.5f)*(v-.5f));float next=(float)Math.Round(Math.Max(.55,Math.Min(1.8,captureWidth*radius/Math.Max(.05,captureRadius))),2);
            if(next!=Keyboard.Width){Keyboard.Width=next;Check(overlay.SetOverlayWidthInMeters(handle,next));Keyboard.Layout();dirty=true;}
        }else{Keyboard.SetSlider(captureAction,(u*Keyboard.W-captureBox.X)/captureBox.Width);dirty=true;}
    }
    internal static float AdjustDepth(float z,float axis,double seconds){
        if(Math.Abs(axis)<=.2f||Single.IsNaN(axis))return z;
        float speed=(Math.Abs(axis)-.2f)/.8f*Math.Sign(axis)*.65f;
        return Math.Max(-3f,Math.Min(-.2f,z-speed*(float)Math.Max(0,Math.Min(.05,seconds))));
    }
    internal static float PanelDistance(HmdMatrix34_t head,HmdMatrix34_t panel){
        float x=panel.m3-head.m3,y=panel.m7-head.m7,z=panel.m11-head.m11;
        return Preferences.NormalizeDistance((float)Math.Sqrt(x*x+y*y+z*z));
    }
    void EndCapture(){if(captureHand<0)return;captureHand=-1;captureAction=null;Keyboard.SaveSettings();}
    public void Show(){
        if(system==null)return;system.GetDeviceToAbsoluteTrackingPose(origin,0,poses);if(!poses[0].bPoseIsValid){Info="헤드셋 추적이 필요합니다.";return;}
        transform=Geometry.InFront(poses[0].mDeviceToAbsoluteTracking,Keyboard.Settings.Distance);Check(overlay.SetOverlayTransformAbsolute(handle,origin,ref transform));Check(overlay.SetOverlayWidthInMeters(handle,Keyboard.Width));Check(overlay.SetOverlayAlpha(handle,Keyboard.Opacity));
        dirty=!Upload();Check(overlay.ShowOverlay(handle));Visible=true;placed=true;
    }
    public void Hide(){EndCapture();if(overlay!=null&&handle!=0)Check(overlay.HideOverlay(handle));Visible=false;Keyboard.CloseReset();dirty=true;}
    void Command(string cmd){
        if(cmd=="hide")Hide();else if(cmd=="recenter")Show();else if(cmd=="resize"&&overlay!=null)Check(overlay.SetOverlayWidthInMeters(handle,Keyboard.Width));else if(cmd=="opacity"&&overlay!=null)Check(overlay.SetOverlayAlpha(handle,Keyboard.Opacity));else if(cmd=="hotkey"){chord.Reset();hold.Update(false,clock.Elapsed.TotalSeconds);}dirty=true;
    }
    bool Upload(){
        var bitmap=renderer.Draw(Keyboard,hover[0],hover[1],points[0],points[1]);var result=texture.Submit(overlay,handle,bitmap);
        if(result==EVROverlayError.None){textureFailures=0;nextTextureRetry=0;return true;}
        textureFailures++;nextTextureRetry=clock.Elapsed.TotalSeconds+Math.Min(2,.1*textureFailures);if(textureFailures==1||textureFailures%30==0)Log(new InvalidOperationException("GPU texture: "+result+"; retaining overlay"));return false;
    }
    public void OpenBindings(){if(input!=null)Check(input.OpenBindingUI(null,actionSet,0,true));}
    void Disconnect(){
        Startup.Disconnected();
        EndCapture();if(overlay!=null&&handle!=0){try{overlay.DestroyOverlay(handle);}catch{}}
        if(texture!=null){texture.Dispose();texture=null;}handle=0;overlay=null;input=null;system=null;Visible=false;placed=false;
        for(int i=0;i<2;i++){grips[i]=gripEdges[i]=pressed[i]=false;presses[i].Suppress(false);}chord.Reset();hold.Update(false,0);try{OpenVR.Shutdown();}catch{}
    }
    static void Log(Exception e){try{File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"overlay.log"),DateTime.Now.ToString("s")+" "+e+Environment.NewLine);}catch{}}
    public void Dispose(){Disconnect();renderer.Dispose();}
}
}
