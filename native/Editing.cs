using System;
using System.Collections.Generic;
using System.Globalization;

namespace HangeulVR {
public sealed class EditState {
    public string Text,Strokes;public int Cursor,Anchor;
}
public sealed class EditBuffer {
    string committed="",strokes="";int cursor,anchor;
    public string Keys {get{return committed.Substring(0,cursor)+strokes+committed.Substring(cursor);}set{committed="";strokes=value??"";cursor=anchor=0;}}
    public string Text {get{return committed.Insert(cursor,Hangul.Compose(strokes));}}
    public int Caret {get{return cursor+Hangul.Compose(strokes).Length;}}
    public int SelectionStart {get{return strokes.Length>0?Caret:Math.Min(cursor,anchor);}}
    public int SelectionLength {get{return strokes.Length>0?0:Math.Abs(cursor-anchor);}}
    public string SelectedText {get{return Text.Substring(SelectionStart,SelectionLength);}}
    public EditState Save(){return new EditState{Text=committed,Strokes=strokes,Cursor=cursor,Anchor=anchor};}
    public void Restore(EditState state){committed=state.Text;strokes=state.Strokes;cursor=state.Cursor;anchor=state.Anchor;}
    public void Commit(){if(strokes.Length==0)return;string built=Hangul.Compose(strokes);committed=committed.Insert(cursor,built);cursor+=built.Length;anchor=cursor;strokes="";}
    bool RemoveSelection(){if(SelectionLength==0)return false;int start=SelectionStart;committed=committed.Remove(start,SelectionLength);cursor=anchor=start;return true;}
    public void InsertKeys(string value){RemoveSelection();strokes+=value;}
    public void InsertText(string value){Commit();RemoveSelection();committed=committed.Insert(cursor,value);cursor+=value.Length;anchor=cursor;}
    static int Previous(string value,int at){int result=0;foreach(int p in StringInfo.ParseCombiningCharacters(value)){if(p>=at)break;result=p;}return result;}
    static int Next(string value,int at){foreach(int p in StringInfo.ParseCombiningCharacters(value))if(p>at)return p;return value.Length;}
    public void Delete(){
        if(strokes.Length>0){strokes=strokes.Substring(0,Previous(strokes,strokes.Length));return;}
        if(RemoveSelection())return;if(cursor==0)return;int previous=Previous(committed,cursor);committed=committed.Remove(previous,cursor-previous);cursor=anchor=previous;
    }
    public void DeleteForward(){Commit();if(RemoveSelection()||cursor==committed.Length)return;committed=committed.Remove(cursor,Next(committed,cursor)-cursor);anchor=cursor;}
    public void SelectAll(){Commit();anchor=0;cursor=committed.Length;}
    public void PlaceCaret(int at,bool extend){
        Commit();at=Math.Max(0,Math.Min(committed.Length,at));
        if(at<committed.Length){int boundary=0;foreach(int p in StringInfo.ParseCombiningCharacters(committed)){if(p>at)break;boundary=p;}at=boundary;}
        cursor=at;if(!extend)anchor=cursor;
    }
    public void Move(string direction,bool extend,bool control){
        Commit();int target=cursor;
        int lineStart=committed.LastIndexOf('\n',Math.Max(0,cursor-1),cursor)+1;
        int lineEnd=committed.IndexOf('\n',cursor);if(lineEnd<0)lineEnd=committed.Length;
        if(direction=="left"||direction=="right"){
            bool left=direction=="left";
            if(!extend&&SelectionLength>0)target=left?SelectionStart:SelectionStart+SelectionLength;
            else {
                target=left?Previous(committed,cursor):Next(committed,cursor);
                if(control)while(target>0&&target<committed.Length&&!Char.IsWhiteSpace(committed[left?target-1:target]))target=left?Previous(committed,target):Next(committed,target);
            }
        }else if(direction=="home")target=control?0:lineStart;
        else if(direction=="end")target=control?committed.Length:lineEnd;
        else if(direction=="up"){
            if(lineStart==0)target=0;
            else {int previousStart=committed.LastIndexOf('\n',Math.Max(0,lineStart-2),lineStart-1)+1;target=Math.Min(lineStart-1,previousStart+cursor-lineStart);}
        }else if(direction=="down"){
            if(lineEnd==committed.Length)target=committed.Length;
            else {int nextEnd=committed.IndexOf('\n',lineEnd+1);if(nextEnd<0)nextEnd=committed.Length;target=Math.Min(nextEnd,lineEnd+1+cursor-lineStart);}
        }
        // Snap away from the middle of surrogate/combining sequences.
        if(target<committed.Length&&target>0){int start=Previous(committed,target+1);target=start;}
        cursor=target;if(!extend)anchor=cursor;
    }
}
public sealed class DoubleTapChord {
    double first=-10;bool previous;
    public bool Update(bool modifier,bool trigger,double now){
        bool edge=trigger&&!previous;previous=trigger;
        if(!modifier){first=-10;return false;}
        if(!edge)return false;
        if(now-first<=.45){first=-10;return true;}first=now;return false;
    }
    public void Reset(){first=-10;previous=false;}
}
public sealed class PressState {
    bool held;string captured;double next;
    public string Update(bool pressed,string action,double now){
        if(!pressed){held=false;captured=null;return null;}
        if(!held){held=true;captured=action;next=now+.45;return action;}
        if(captured==null||now<next||action!=captured)return null;
        if(captured=="back"||captured=="delete"||captured=="left"||captured=="right"||captured=="up"||captured=="down"){next=now+.075;return captured;}
        return null;
    }
    public void Suppress(bool pressed){held=pressed;captured=null;}
    public bool Held {get{return held;}}
}
}
