using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace DivideoGui {
class TimeBox : UserControl {
    TextBox edit=new TextBox { Dock=DockStyle.Fill }; decimal value;
    public decimal Maximum=10000000;
    public event EventHandler ValueChanged;
    public static string Format(double seconds) { long ms=(long)Math.Round(Math.Max(0,seconds)*1000); return string.Format(CultureInfo.InvariantCulture,"{0:00}:{1:00}:{2:00}.{3:000}",ms/3600000,ms/60000%60,ms/1000%60,ms%1000); }
    public static bool Parse(string text,out decimal seconds) {
        seconds=0; var parts=text.Trim().Replace(',','.').Split(':'); if(parts.Length!=2 && parts.Length!=3)return false;
        for(int i=0;i<parts.Length;i++) { decimal n; if(!decimal.TryParse(parts[i],NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out n) || n<0 || (i>0 && n>=60) || (i<parts.Length-1 && n!=decimal.Truncate(n)))return false; seconds=seconds*60+n; } return true;
    }
    public TimeBox() { Width=130; Height=28; Controls.Add(edit); edit.Font=new Font("Consolas",10); edit.Text=Format(0); edit.Validating+=(s,e)=> { decimal n; if(!Parse(edit.Text,out n) || n>Maximum) { edit.BackColor=Color.MistyRose; e.Cancel=true; } else { Value=n; edit.BackColor=SystemColors.Window; } }; edit.KeyDown+=(s,e)=> { if(e.KeyCode==Keys.Enter) { decimal n; if(Parse(edit.Text,out n)&&n<=Maximum)Value=n; else edit.BackColor=Color.MistyRose; e.SuppressKeyPress=true; } }; }
    public decimal Value { get { return value; } set { decimal next=Math.Max(0,Math.Min(Maximum,value)); bool changed=this.value!=next; this.value=next; edit.Text=Format((double)next); edit.BackColor=SystemColors.Window; if(changed&&ValueChanged!=null)ValueChanged(this,EventArgs.Empty); } }
}
class Timeline : Control {
    public double Total=1,Start,End=1,Position;
    int drag; public event Action<int,double> Changed;
    public Timeline() { Width=920; Height=74; DoubleBuffered=true; TabStop=true; Cursor=Cursors.Hand; }
    float X(double t) { return 12+(float)(Math.Max(0,Math.Min(Total,t))/Math.Max(.001,Total)*(Width-24)); }
    double T(int x) { return Math.Max(0,Math.Min(Total,(x-12.0)/(Width-24)*Total)); }
    protected override void OnPaint(PaintEventArgs e) {
        var g=e.Graphics; g.Clear(BackColor); g.SmoothingMode=SmoothingMode.AntiAlias;
        using(var p=new Pen(Color.SlateGray,4))g.DrawLine(p,12,17,Width-12,17);
        using(var b=new SolidBrush(Color.FromArgb(70,120,210)))g.FillRectangle(b,X(Start),37,Math.Max(1,X(End)-X(Start)),12);
        using(var p=new Pen(Color.FromArgb(24,90,200),8)) { g.DrawLine(p,X(Start),31,X(Start),54); g.DrawLine(p,X(End),31,X(End),54); }
        using(var p=new Pen(Color.OrangeRed,3))g.DrawLine(p,X(Position),6,X(Position),28);
        g.DrawString("Начало: "+TimeBox.Format(Start),Font,Brushes.Black,8,55);
        string end="Конец: "+TimeBox.Format(End); g.DrawString(end,Font,Brushes.Black,Width-g.MeasureString(end,Font).Width-8,55);
    }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if(e.Button!=MouseButtons.Left)return; Focus(); drag=e.Y<31?1:(Math.Abs(e.X-X(Start))<=Math.Abs(e.X-X(End))?2:3); Capture=true; MoveTo(e.X); }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if(drag!=0)MoveTo(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); drag=0; Capture=false; }
    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if(!Capture)drag=0; }
    void MoveTo(int x) { double t=T(x); if(drag==2)t=Math.Min(t,Math.Max(0,End-.04)); if(drag==3)t=Math.Max(t,Math.Min(Total,Start+.04)); if(Changed!=null)Changed(drag,t); }
    internal void TestDrag(int kind,double target) { int origin=(int)X(kind==2?Start:kind==3?End:Position); OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,origin,kind==1?17:42,0)); OnMouseMove(new MouseEventArgs(MouseButtons.Left,0,(int)X(target),kind==1?17:42,0)); OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,(int)X(target),17,0)); }
    protected override bool IsInputKey(Keys key) { return key==Keys.Left || key==Keys.Right || base.IsInputKey(key); }
    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Right) { if(Changed!=null)Changed(1,Math.Max(0,Math.Min(Total,Position+(e.KeyCode==Keys.Left?-.04:.04)))); e.Handled=true; } }
}
class CropView : Control {
    public Bitmap Frame; public int SourceWidth=1,SourceHeight=1,LeftCrop,RightCrop,TopCrop,BottomCrop,Fit;
    public bool Result; public CropView() { Width=920; Height=260; DoubleBuffered=true; BackColor=Color.FromArgb(17,21,28); }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e); var g=e.Graphics; if(Frame==null) { g.DrawString("Откройте видео — здесь появится исходный кадр",Font,Brushes.White,16,16); return; }
        float l=(float)LeftCrop/SourceWidth,r=(float)RightCrop/SourceWidth,t=(float)TopCrop/SourceHeight,b=(float)BottomCrop/SourceHeight;
        if(l+r>=1 || t+b>=1) { g.DrawString("Обрезка удаляет весь кадр",Font,Brushes.OrangeRed,16,16); return; }
        g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        var src=new RectangleF(l*Frame.Width,t*Frame.Height,(1-l-r)*Frame.Width,(1-t-b)*Frame.Height);
        if(Result) {
            float h=Math.Min(Height,Width*.75f),w=h*4/3; var box=new RectangleF((Width-w)/2,(Height-h)/2,w,h); var dest=box;
            if(Fit==0) { float scale=Math.Min(w/src.Width,h/src.Height); dest=new RectangleF(box.X+(w-src.Width*scale)/2,box.Y+(h-src.Height*scale)/2,src.Width*scale,src.Height*scale); }
            else if(Fit==1) { float ratio=4f/3; if(src.Width/src.Height>ratio) { float nw=src.Height*ratio; src.X+=(src.Width-nw)/2; src.Width=nw; } else { float nh=src.Width/ratio; src.Y+=(src.Height-nh)/2; src.Height=nh; } }
            g.FillRectangle(Brushes.Black,box); g.DrawImage(Frame,dest,src,GraphicsUnit.Pixel);
        } else {
            float scale=Math.Min((float)Width/Frame.Width,(float)Height/Frame.Height); var dest=new RectangleF((Width-Frame.Width*scale)/2,(Height-Frame.Height*scale)/2,Frame.Width*scale,Frame.Height*scale); g.DrawImage(Frame,dest);
            var keep=new RectangleF(dest.X+dest.Width*l,dest.Y+dest.Height*t,dest.Width*(1-l-r),dest.Height*(1-t-b));
            using(var region=new Region(dest)) { region.Exclude(keep); using(var brush=new SolidBrush(Color.FromArgb(170,0,0,0)))g.FillRegion(brush,region); }
            using(var pen=new Pen(Color.LimeGreen,2))g.DrawRectangle(pen,keep.X,keep.Y,keep.Width,keep.Height);
        }
    }
}
}
