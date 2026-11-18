using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace HangeulVR {
public class Key {
    public RectangleF Box;public string Label,Action,Secondary="";
    public Key(float x,float y,float w,float h,string label,string action){Box=new RectangleF(x,y,w,h);Label=label;Action=action;}
}
public class Keyboard {
    public const int ChatLeft=75,ChatWidth=950;
    public const int ChatHeight=166,PanelGap=32,KeyboardOffset=ChatHeight+PanelGap-230;
    public const int W=1100,H=590+KeyboardOffset;
    public static bool IsPanel(float x,float y){return x>=0&&x<W&&y>=0&&y<H&&((y<=ChatHeight&&x>=ChatLeft&&x<ChatLeft+ChatWidth)||y>=ChatHeight+PanelGap);}
    public EditBuffer Input=new EditBuffer();
    public List<Key> Keys=new List<Key>();
    public List<EditState> Undo=new List<EditState>(),Redo=new List<EditState>();
    public Preferences Settings=new Preferences();
    public string SettingsPath;
    public string Status="실시간 전송 켜짐 · 작성 중인 내용도 다른 사람에게 보입니다.";
    public bool Shift,Sound,Caps,Ctrl,DeletePhrases,SettingsOpen;
    public int Mode,Port=9000,PhrasePage;
    public float Width {get{return Settings.Width;}set{Settings.Width=value;}}
    public float Opacity {get{return Settings.Opacity;}set{Settings.Opacity=value;}}
    public bool Live=true;
    string observed="",delivered="";double pendingSince=-1,retryAt;bool finishPending;
    public double LastSent=-100;
    public Action<string> Command;
    public Func<string> ReadClipboard=()=>Clipboard.ContainsText()?Clipboard.GetText():"";
    public Action<string> WriteClipboard=s=>Clipboard.SetText(s);
    public Keyboard(){Layout();}
    public int LayoutRevision {get;private set;}
    public string HotkeyLabel {get{return "왼손 그립 + 오른손 트리거 두 번";}}
    public void LoadSettings(string path){SettingsPath=path;try{Settings=Preferences.Load(path);}catch(Exception e){Status="설정 읽기 실패: "+e.Message;}Layout();}
    public void SaveSettings(){if(SettingsPath==null)return;try{Settings.Save(SettingsPath);}catch(Exception e){Status="설정 저장 실패: "+e.Message;}}
    void Add(float x,float y,float w,float h,string label,string action){Keys.Add(new Key(x,y>=240?y+KeyboardOffset:y,w,h,label,action));}
    void Physical(ref float unit,float y,float size,string label,string action,string secondary){Add(24+unit*(1052f/15),y,size*(1052f/15)-6,56,label,action);Keys[Keys.Count-1].Secondary=secondary;unit+=size;}
    void LetterRow(ref float unit,float y,string latin,string korean){
        for(int i=0;i<latin.Length;i++){
            char c=Mode==0?korean[i]:(Shift^Caps)?Char.ToUpperInvariant(latin[i]):latin[i];
            string label=c.ToString();
            if(Ctrl)switch(latin[i]){
                case 'a':label="전체 선택";break;case 'c':label="복사하기";break;
                case 'v':label="붙여넣기";break;case 'x':label="잘라내기";break;
                case 'z':label=Shift?"다시 실행":"되돌리기";break;case 'y':label="다시 실행";break;
            }
            Physical(ref unit,y,1,label,Ctrl?"shortcut:"+latin[i]:"key:"+c,Ctrl?"Ctrl+"+Char.ToUpperInvariant(latin[i]):Mode==0?Char.ToUpperInvariant(latin[i]).ToString():"");
        }
    }
    public void Layout(){
        LayoutRevision++;Keys.Clear();
        Add(861,7,42,40,SettingsOpen?"키보드로":"설정","settings");Add(909,7,42,40,Live?"실시간 켬":"실시간 끔","live");Add(957,7,42,40,"닫기","hide");
        Add(933,75,66,62,finishPending?"전송 대기…":Live?"작성 완료":"보내기","send");
        Add(117,64,784,64,"커서 이동","caret");
        if(SettingsOpen){
            Add(100,286,900,54,"불투명도 "+(int)(Opacity*100)+"%","opacity-slider");
            Add(100,352,900,54,"키보드 크기 "+Width.ToString("F2")+"m","size-slider");
            Add(100,418,900,44,"잡고 조이스틱으로 거리 조절: "+(Settings.GripDistance?"켬":"끔"),"grip-distance");
            return;
        }
        float u=0;string plain="`1234567890-=",upper="~!@#$%^&*()_+";
        for(int i=0;i<plain.Length;i++)Physical(ref u,240,1,(Shift?upper[i]:plain[i]).ToString(),"key:"+(Shift?upper[i]:plain[i]),Shift?"":upper[i].ToString());
        Physical(ref u,240,2,"⌫ Backspace","back","");
        u=0;Physical(ref u,304,1.5f,"Tab","key:    ","공백 4칸");LetterRow(ref u,304,"qwertyuiop",Shift?"ㅃㅉㄸㄲㅆㅛㅕㅑㅒㅖ":"ㅂㅈㄷㄱㅅㅛㅕㅑㅐㅔ");
        Physical(ref u,304,1,Shift?"{":"[",Shift?"key:{":"key:[","");Physical(ref u,304,1,Shift?"}":"]",Shift?"key:}":"key:]","");Physical(ref u,304,1.5f,Shift?"|":"\\",Shift?"key:|":"key:\\","");
        u=0;Physical(ref u,368,1.75f,Caps?"Caps ON":"Caps Lock","caps","");LetterRow(ref u,368,"asdfghjkl","ㅁㄴㅇㄹㅎㅗㅓㅏㅣ");
        Physical(ref u,368,1,Shift?":":";",Shift?"key::":"key:;","");Physical(ref u,368,1,Shift?"\"":"'",Shift?"key:\"":"key:'","");Physical(ref u,368,2.25f,"Enter ↵",Shift?"key:\n":"send",Shift?"줄바꿈":"작성 완료 / 전송");
        u=0;Physical(ref u,432,2.25f,Shift?"Shift ON":"⇧ Shift","shift","");LetterRow(ref u,432,"zxcvbnm","ㅋㅌㅊㅍㅠㅜㅡ");
        string p=Shift?"<>?":",./";foreach(char c in p)Physical(ref u,432,1,c.ToString(),"key:"+c,"");Physical(ref u,432,2.75f,Shift?"Shift ON":"Shift ⇧","shift","");
        u=0;Physical(ref u,496,1.5f,Ctrl?"Ctrl ON":"Ctrl","ctrl","");Physical(ref u,496,1.5f,"줄바꿈","key:\n","");Physical(ref u,496,7,"Space · 띄어쓰기","key: ","");Physical(ref u,496,1.5f,Mode==0?"한 / 영":"한 / 영 EN","language","");float arrows=24+u*(1052f/15), aw=(1052f-u*(1052f/15))/3;
        Add(arrows,496,aw-6,56,"←","left");Add(arrows+aw,496,aw-6,26,"↑","up");Add(arrows+aw,526,aw-6,26,"↓","down");Add(arrows+aw*2,496,aw-6,56,"→","right");
    }
    public void CloseReset(){Input.Keys="";Undo.Clear();Redo.Clear();Shift=Caps=Ctrl=DeletePhrases=SettingsOpen=false;Mode=PhrasePage=0;finishPending=false;delivered="";ResetLive();Status="초안을 초기화했습니다.";Layout();}
    void SaveUndo(){PushUndo(Input.Save());}
    void PushUndo(EditState state){Undo.Add(state);if(Undo.Count>100)Undo.RemoveAt(0);Redo.Clear();finishPending=false;}
    void UndoEdit(bool redo){var from=redo?Redo:Undo;var to=redo?Undo:Redo;if(from.Count==0)return;to.Add(Input.Save());Input.Restore(from[from.Count-1]);from.RemoveAt(from.Count-1);finishPending=false;}
    void ResetLive(){observed=Input.Text;pendingSince=-1;retryAt=0;}
    bool Valid(){return Input.Text.Length<=144&&Input.Text.Split('\n').Length<=9;}
    void Insert(string value,bool literal){
        var before=Input.Save();if(literal)Input.InsertText(value);else Input.InsertKeys(value);
        if(!Valid()){Input.Restore(before);Status="144자 / 최대 9줄까지 작성할 수 있어요.";}
        else {PushUndo(before);Status=Live?"실시간 전송 중 · 선택한 글자는 입력 시 대체됩니다.":"작성 중 · 완료 버튼으로 전송합니다.";}
    }
    void Finish(){SaveUndo();Input.Keys="";finishPending=false;ResetLive();delivered="";Status="작성 완료 · 마지막 전송 문장을 유지합니다.";Layout();}
    public bool UpdateLive(double now){
        string text=Input.Text;
        if(text!=observed){observed=text;if(pendingSince<0)pendingSince=now;}
        if((!Live&&!finishPending)||String.IsNullOrWhiteSpace(text)){pendingSince=-1;return false;}
        if(text==delivered){pendingSince=-1;if(finishPending){Finish();return true;}return false;}
        if(pendingSince<0)pendingSince=now;
        if((!finishPending&&now-pendingSince<.25)||now-LastSent<1.5||now<retryAt)return false;
        try {Osc.Send(text,finishPending&&Sound,Port);LastSent=now;delivered=text;pendingSince=-1;retryAt=0;Status="실시간 OSC 전송 · 최신 문장으로 갱신했습니다.";if(finishPending)Finish();}
        catch(Exception e){retryAt=now+2;Status="전송 실패 · 초안 보관 중: "+e.Message;}
        return true;
    }
    public int Hit(float x,float y){for(int i=0;i<Keys.Count;i++)if(Keys[i].Box.Contains(x,y))return i;return -1;}
    public int HitTyping(float x,float y){int exact=Hit(x,y);if(exact>=0)return exact;for(int i=0;i<Keys.Count;i++){string a=Keys[i].Action;if(!a.StartsWith("key:")&&!a.StartsWith("shortcut:")&&a!="back"&&a!="delete")continue;var box=Keys[i].Box;box.Inflate(2,2);if(box.Contains(x,y))return i;}return -1;}
    public void SetSlider(string action,float fraction){
        fraction=Math.Max(0,Math.Min(1,fraction));
        if(action=="opacity-slider"){float next=(float)Math.Round(.25+.75*fraction,2);if(Opacity==next)return;Opacity=next;}
        else {float next=(float)Math.Round(.55+1.25*fraction,2);if(Width==next)return;Width=next;}
        if(Command!=null)Command(action=="opacity-slider"?"opacity":"resize");Layout();
    }
    public void Activate(int index,double now){if(index>=0&&index<Keys.Count)ActivateAction(Keys[index].Action,now);}
    public void ActivateAction(string a,double now){
        if(a==null)return;
        try {
            if(a.StartsWith("key:")){Insert(a.Substring(4),false);if(Shift){Shift=false;Layout();}return;}
            if(a.StartsWith("shortcut:")){try{Shortcut(a.Substring(9));}finally{Ctrl=false;Shift=false;Layout();}return;}
            if(a.StartsWith("phrase-index:")){
                int n=Int32.Parse(a.Substring(13));if(n>=Settings.Phrases.Count)return;
                if(DeletePhrases){Settings.Phrases.RemoveAt(n);SaveSettings();Layout();}else Insert(Settings.Phrases[n],true);return;
            }
            switch(a){
                case "settings":SettingsOpen=!SettingsOpen;break;
                case "grip-distance":Settings.GripDistance=!Settings.GripDistance;SaveSettings();break;
                case "back":SaveUndo();Input.Delete();break;
                case "delete":SaveUndo();Input.DeleteForward();break;
                case "clear":SaveUndo();Input.Keys="";break;
                case "undo":UndoEdit(false);break;
                case "redo":UndoEdit(true);break;
                case "shift":Shift=!Shift;break;
                case "caps":Caps=!Caps;break;
                case "ctrl":Input.Commit();Ctrl=!Ctrl;break;
                case "language":Input.Commit();Mode=Mode==0?1:0;Shift=false;break;
                case "ko":Input.Commit();Mode=0;Shift=false;break;
                case "en":Input.Commit();Mode=1;Shift=false;break;
                case "home":case "end":case "left":case "right":case "up":case "down":Input.Move(a,Shift,Ctrl);Ctrl=false;break;
                case "live":Live=!Live;finishPending=false;ResetLive();delivered="";Status=Live?"실시간 전송 켜짐 · 현재 초안부터 전송합니다.":"실시간 전송 꺼짐 · 완료 버튼으로 전송합니다.";break;
                case "send":
                    if(String.IsNullOrWhiteSpace(Input.Text)){Status="보낼 내용을 입력해 주세요.";break;}
                    if(Input.Text==delivered){Finish();break;}
                    finishPending=true;Status="최신 문장 전송 대기 중…";UpdateLive(now);break;
                case "phrase-add":
                    string phrase=Input.Text.Trim();if(phrase.Length==0){Status="추가할 문장을 먼저 작성해 주세요.";break;}
                    if(Settings.Phrases.Count>=30){Status="단축어는 최대 30개입니다.";break;}
                    if(Settings.Phrases.Contains(phrase)){Status="이미 저장한 단축어입니다.";break;}
                    Settings.Phrases.Add(phrase);PhrasePage=(Settings.Phrases.Count-1)/3;SaveSettings();Status="단축어를 저장했습니다.";break;
                case "phrase-delete":DeletePhrases=!DeletePhrases;Status=DeletePhrases?"삭제할 단축어를 누르세요.":"단축어 삭제 모드 종료";break;
                case "phrase-prev":PhrasePage--;break;
                case "phrase-next":PhrasePage++;break;
                case "hotkey":Settings.Hotkey=(Settings.Hotkey+1)%3;SaveSettings();Status="호출: "+HotkeyLabel;if(Command!=null)Command("hotkey");break;
                default:if(Command!=null)Command(a);break;
            }
        } catch(Exception e){Status="처리하지 못했습니다: "+e.Message;}
        Layout();
    }
    void Shortcut(string key){
        switch(key){
            case "a":Input.SelectAll();break;
            case "c":if(Input.SelectionLength>0)WriteClipboard(Input.SelectedText);Status="선택 영역을 복사했습니다.";break;
            case "x":if(Input.SelectionLength>0){WriteClipboard(Input.SelectedText);SaveUndo();Input.Delete();}break;
            case "v":string value=ReadClipboard();if(value==null)break;value=value.Replace("\r\n","\n").Replace('\r','\n').Replace("\t","    ");foreach(char c in value)if(Char.IsControl(c)&&c!='\n')throw new ArgumentException("클립보드에 지원하지 않는 제어 문자가 있습니다.");Insert(value,true);break;
            case "z":UndoEdit(Shift);break;
            case "y":UndoEdit(true);break;
            default:Status="지원 단축키: Ctrl+A/C/V/X/Z/Y";break;
        }
    }
    static void Text(Graphics g,string text,float size,Color color,RectangleF box,bool center){
        using(var font=new Font("Malgun Gothic",size,FontStyle.Regular,GraphicsUnit.Pixel))using(var brush=new SolidBrush(color))using(var format=new StringFormat{Alignment=center?StringAlignment.Center:StringAlignment.Near,LineAlignment=center?StringAlignment.Center:StringAlignment.Near,Trimming=StringTrimming.EllipsisCharacter})g.DrawString(text,font,brush,box,format);
    }
    static void Card(Graphics g,RectangleF box,float radius,Color fill,Color border,float stroke){
        float d=radius*2;
        using(var path=new GraphicsPath()){
            path.AddArc(box.X,box.Y,d,d,180,90);path.AddArc(box.Right-d,box.Y,d,d,270,90);
            path.AddArc(box.Right-d,box.Bottom-d,d,d,0,90);path.AddArc(box.X,box.Bottom-d,d,d,90,90);path.CloseFigure();
            using(var brush=new SolidBrush(fill))g.FillPath(brush,path);
            using(var pen=new Pen(border,stroke))g.DrawPath(pen,path);
        }
    }
    readonly List<PointF> caretPoints=new List<PointF>();readonly List<int> caretIndices=new List<int>();
    public int CaretAt(float x,float y){
        // Reuse the exact glyph metrics, wrapping and scroll used by Draft.
        using(var bitmap=new Bitmap(1,1))using(var g=Graphics.FromImage(bitmap)){g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;Draft(g);}
        int result=0;float best=Single.MaxValue;
        for(int i=0;i<caretPoints.Count;i++){
            var p=caretPoints[i];float row=Math.Abs(p.Y+16-y);float score=row*10000+Math.Abs(p.X-x);
            if(score<best){best=score;result=caretIndices[i];}
        }return result;
    }
    public void ClickDraft(float x,float y){Input.PlaceCaret(CaretAt(x,y),Shift);Ctrl=false;Layout();}
    void Draft(Graphics g){
        var box=new RectangleF(117,64,784,64);string text=Input.Text;
        using(var font=new Font("Malgun Gothic",26,FontStyle.Regular,GraphicsUnit.Pixel))using(var format=(StringFormat)StringFormat.GenericTypographic.Clone()){
            format.FormatFlags|=StringFormatFlags.MeasureTrailingSpaces;float x=0,y=0,line=32;var positions=new List<PointF>();var values=new List<string>();var starts=StringInfo.ParseCombiningCharacters(text);PointF caret=new PointF();
            for(int i=0;i<starts.Length;i++){
                string c=text.Substring(starts[i],(i+1<starts.Length?starts[i+1]:text.Length)-starts[i]);float w=c=="\n"?8:g.MeasureString(c,font,999,format).Width;
                if(x+w>box.Width){x=0;y+=line;}if(starts[i]==Input.Caret)caret=new PointF(x,y);
                positions.Add(new PointF(x,y));values.Add(c);if(c=="\n"){x=0;y+=line;}else x+=w;
            }
            if(Input.Caret==text.Length)caret=new PointF(x,y);float scroll=Math.Max(0,caret.Y+line-box.Height);var saved=g.Save();g.SetClip(box);
            caretPoints.Clear();caretIndices.Clear();
            for(int i=0;i<positions.Count;i++){caretPoints.Add(new PointF(box.X+positions[i].X,box.Y+positions[i].Y-scroll));caretIndices.Add(starts[i]);}
            caretPoints.Add(new PointF(box.X+x,box.Y+y-scroll));caretIndices.Add(text.Length);
            using(var selected=new SolidBrush(Color.FromArgb(53,99,130)))for(int i=0;i<values.Count;i++){
                var pt=new PointF(box.X+positions[i].X,box.Y+positions[i].Y-scroll);string c=values[i];float w=c=="\n"?8:g.MeasureString(c,font,999,format).Width;
                if(starts[i]>=Input.SelectionStart&&starts[i]<Input.SelectionStart+Input.SelectionLength)g.FillRectangle(selected,pt.X,pt.Y,w,line);
                if(c!="\n")g.DrawString(c,font,Brushes.White,pt,format);
            }
            using(var pen=new Pen(Color.Aquamarine,2))g.DrawLine(pen,box.X+caret.X,box.Y+caret.Y-scroll,box.X+caret.X,box.Y+caret.Y+30-scroll);g.Restore(saved);
        }
    }
    public Bitmap Render(int left,int right,PointF? lp,PointF? rp){
        var b=new Bitmap(W,H,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(b)){
            g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;g.Clear(Color.Transparent);
            Card(g,new RectangleF(ChatLeft+1,1,ChatWidth-2,ChatHeight-2),16,Color.FromArgb(17,23,31),Color.FromArgb(57,77,88),1);
            Card(g,new RectangleF(1,ChatHeight+PanelGap,W-2,H-ChatHeight-PanelGap-1),18,Color.FromArgb(17,23,31),Color.FromArgb(57,77,88),1);
            Card(g,new RectangleF(99,54,820,100),10,Color.FromArgb(20,30,39),Live?Color.FromArgb(99,201,172):Color.FromArgb(64,89,98),1);
            Text(g,"협합 · 한글뜰",18,Color.FromArgb(231,241,246),new RectangleF(250,10,600,30),true);
            int hovered=right>=0?right:left;
            if(hovered>=0&&hovered<Keys.Count&&Keys[hovered].Box.Y<ChatHeight)Text(g,Keys[hovered].Label,13,Color.FromArgb(169,195,201),new RectangleF(99,17,180,24),false);
            Text(g,Input.Text.Length+" / 144",12,Input.Text.Length>=130?Color.FromArgb(255,204,139):Color.FromArgb(153,175,191),new RectangleF(825,131,76,20),true);
            if(Input.Text.Length==0)Text(g,"메시지를 입력하세요",26,Color.FromArgb(110,132,151),new RectangleF(117,64,784,38),false);Draft(g);
            Text(g,Status,11,Color.FromArgb(149,169,184),new RectangleF(117,133,690,18),false);
            if(SettingsOpen){Text(g,"키보드 설정",24,Color.White,new RectangleF(100,240+KeyboardOffset,900,40),false);Text(g,"슬라이더를 트리거로 잡아 조절하세요. 크기는 양손 그립으로도 조절할 수 있습니다.",18,Color.Silver,new RectangleF(100,466+KeyboardOffset,900,34),true);}
            if(SettingsOpen){Text(g,AppInfo.Feedback,15,Color.FromArgb(182,201,214),new RectangleF(100,503+KeyboardOffset,900,24),true);Text(g,AppInfo.Copyright+"  "+AppInfo.ThirdParty,12,Color.FromArgb(153,175,191),new RectangleF(100,535+KeyboardOffset,900,22),true);}
            for(int i=0;i<Keys.Count;i++){var k=Keys[i];if(k.Action=="caret")continue;bool send=k.Action=="send";
                bool active=(k.Action=="live"&&Live)||(k.Action=="shift"&&Shift)||(k.Action=="caps"&&Caps)||(k.Action=="ctrl"&&Ctrl)||(k.Action=="settings"&&SettingsOpen);
                bool letter=k.Action.StartsWith("key:")||k.Action.StartsWith("shortcut:");
                Color fill=send?Color.FromArgb(156,222,199):active?Color.FromArgb(37,85,77):i==left||i==right?Color.FromArgb(54,78,93):letter?Color.FromArgb(36,48,63):Color.FromArgb(27,38,51);
                Card(g,k.Box,Math.Min(9,k.Box.Height/3),fill,i==left?Color.Aquamarine:i==right?Color.FromArgb(255,205,133):active?Color.FromArgb(80,155,135):send?Color.FromArgb(176,238,214):Color.FromArgb(56,73,90),i==left||i==right?2:1);
                var labelBox=k.Box;
                if(k.Box.Y<ChatHeight){DrawIcon(g,k,send?Color.FromArgb(16,65,49):active?Color.Aquamarine:Color.FromArgb(221,235,241));continue;}
                if(k.Secondary.Length>0){Text(g,k.Secondary,10,send?Color.FromArgb(35,77,64):Color.FromArgb(150,169,193),new RectangleF(k.Box.X+7,k.Box.Y+3,k.Box.Width-10,15),false);labelBox.Y+=8;labelBox.Height-=8;}
                if(k.Action.EndsWith("-slider")){
                    float fraction=k.Action=="opacity-slider"?(Opacity-.25f)/.75f:(Width-.55f)/1.25f;
                    using(var pen=new Pen(Color.FromArgb(95,141,131),4))g.DrawLine(pen,k.Box.X+12,k.Box.Bottom-8,k.Box.Right-12,k.Box.Bottom-8);
                    g.FillEllipse(Brushes.Aquamarine,k.Box.X+fraction*k.Box.Width-5,k.Box.Bottom-13,10,10);labelBox.Height-=12;
                }
                Text(g,k.Label,k.Action.StartsWith("shortcut:")&&k.Label.Length>1?13:send&&k.Box.Height>100?32:k.Action=="drag"?23:k.Action.StartsWith("key:")&&k.Label.Length==1?27:k.Box.Width<110?16:18,send?Color.FromArgb(16,43,36):Color.White,labelBox,true);
            }
            if(lp.HasValue)Dot(g,lp.Value,Color.Aquamarine);if(rp.HasValue)Dot(g,rp.Value,Color.FromArgb(255,205,133));
            Text(g,"호출: "+HotkeyLabel+"  ·  한 손 그립으로 이동 · 양손 그립으로 크기 조절",12,Color.SlateGray,new RectangleF(24,564+KeyboardOffset,1050,22),true);
        }return b;
    }
    static void DrawIcon(Graphics g,Key key,Color color){
        float x=key.Box.X+key.Box.Width/2,y=key.Box.Y+key.Box.Height/2;
        using(var pen=new Pen(color,2.2f))using(var brush=new SolidBrush(color)){
            pen.StartCap=pen.EndCap=LineCap.Round;
            if(key.Action=="hide"){g.DrawLine(pen,x-6,y-6,x+6,y+6);g.DrawLine(pen,x+6,y-6,x-6,y+6);}
            else if(key.Action=="settings"){
                var teeth=new PointF[32];
                for(int i=0;i<teeth.Length;i++){double a=(i-.5)*Math.PI/16;float radius=i%4==1||i%4==2?12:9;teeth[i]=new PointF(x+(float)Math.Cos(a)*radius,y+(float)Math.Sin(a)*radius);}
                using(var gear=new GraphicsPath(FillMode.Alternate)){gear.AddPolygon(teeth);gear.AddEllipse(x-4,y-4,8,8);g.FillPath(brush,gear);}
            }else if(key.Action=="live"){
                g.FillEllipse(brush,x-3,y-3,6,6);g.DrawArc(pen,x-8,y-8,16,16,-60,120);g.DrawArc(pen,x-8,y-8,16,16,120,120);
                g.DrawArc(pen,x-12,y-12,24,24,-55,110);g.DrawArc(pen,x-12,y-12,24,24,125,110);
            }else if(key.Action=="send"){
                g.FillPolygon(brush,new[]{new PointF(x-13,y-13),new PointF(x+16,y),new PointF(x-13,y+13),new PointF(x-6,y)});
            }
        }
    }
    internal static void Dot(Graphics g,PointF p,Color c){using(var brush=new SolidBrush(c))g.FillEllipse(brush,p.X-7,p.Y-7,14,14);using(var pen=new Pen(Color.White,2))g.DrawEllipse(pen,p.X-7,p.Y-7,14,14);}
}
}
