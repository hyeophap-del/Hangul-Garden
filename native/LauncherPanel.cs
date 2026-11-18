using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HangeulVR {
public sealed class LauncherPanel : UserControl {
    readonly CheckBox auto=new CheckBox();readonly Label help=new Label();
    public Action<bool> SetAutoStart;
    public void RefreshStartup(StartupState state){changing=true;auto.Checked=state.Enabled;help.Text=state.Message;changing=false;}
    readonly Label status=new Label();readonly Timer timer=new Timer();bool changing,busy;
    public LauncherPanel(){
        Size=new Size(525,120);BackColor=Color.FromArgb(20,27,39);ForeColor=Color.White;
        auto.Text="SteamVR 시작 시 한글뜰 자동 실행";auto.SetBounds(0,0,520,30);Controls.Add(auto);
        help.Text="SteamVR 연결 후 시작 앱 목록에 등록합니다.";help.SetBounds(0,32,525,40);Controls.Add(help);
        status.SetBounds(0,84,525,30);status.Text="버전 확인 중…";Controls.Add(status);
        auto.CheckedChanged+=delegate{
            if(changing||SetAutoStart==null)return;SetAutoStart(auto.Checked);
        };
        timer.Interval=6*60*60*1000;timer.Tick+=async delegate{await CheckNow();};timer.Start();
    }
    public async Task CheckNow(bool prompt=false){
        if(busy||IsDisposed)return;
        busy=true;
        try{
            var result=await Task.Run(()=>Updates.Check(Updates.DefaultRepository));if(IsDisposed)return;
            status.Text=result==null?"현재 최신버전입니다":"이전 버전입니다";
            if(prompt&&result!=null){
                using(var dialog=new UpdatePrompt(result)){
                    if(dialog.ShowDialog(FindForm())==DialogResult.OK){Program.RestartTarget=dialog.Target;FindForm().Close();}
                }
            }
        }catch(Exception){if(!IsDisposed)status.Text="버전을 확인할 수 없습니다";}
        finally{busy=false;}
    }
    protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}
public sealed class UpdatePrompt : Form {
    readonly Label message=new Label();readonly Button install=new Button(),later=new Button();
    readonly ReleaseInfo release;readonly Func<ReleaseInfo,Task<string>> prepare;bool installing;
    public string Target {get;private set;}
    public UpdatePrompt(ReleaseInfo info):this(info,r=>Task.Run(()=>Updates.Prepare(r))){}
    internal UpdatePrompt(ReleaseInfo info,Func<ReleaseInfo,Task<string>> download){
        prepare=download;
        release=info;Text="협합 · 한글뜰 업데이트";ClientSize=new Size(460,210);StartPosition=FormStartPosition.CenterParent;
        FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=MinimizeBox=false;ShowInTaskbar=false;
        Font=new Font("Malgun Gothic",10);BackColor=Color.FromArgb(20,27,39);ForeColor=Color.White;
        message.SetBounds(24,22,412,110);message.Text="새 버전 감지됨\r\n버전 "+info.Version+"으로 업데이트할 수 있습니다.\r\n업데이트 후 재시작하며, 작성 중인 문장은 지워집니다.";Controls.Add(message);
        install.Text="업데이트하기";install.SetBounds(24,150,200,40);Controls.Add(install);
        later.Text="나중에하기";later.SetBounds(236,150,200,40);later.DialogResult=DialogResult.Cancel;Controls.Add(later);CancelButton=later;
        install.Click+=async delegate{
            if(installing)return;installing=true;install.Enabled=later.Enabled=false;
            message.Text="업데이트 다운로드 중…\r\n서명과 파일을 검증한 뒤 자동으로 재시작합니다.";
            try{Target=await prepare(release);installing=false;DialogResult=DialogResult.OK;Close();}
            catch(Exception){message.Text="업데이트하지 못했습니다. 기존 버전은 유지됩니다.\r\n인터넷 연결과 보안 프로그램 알림을 확인해 주세요.\r\n다시 시도하거나 나중에 진행할 수 있습니다.";}
            finally{installing=false;if(!IsDisposed)install.Enabled=later.Enabled=true;}
        };
        FormClosing+=delegate(object sender,FormClosingEventArgs e){if(installing)e.Cancel=true;};
    }
}
}
