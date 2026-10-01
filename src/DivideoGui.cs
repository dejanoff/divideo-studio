using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace DivideoGui {
static class Program {
    [STAThread] static void Main(string[] args) {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try {
            if (args.Length > 0 && args[0] == "--self-test") { SelfTest.Run(args[1]); return; }
            Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
        } catch (Exception ex) { MessageBox.Show(ex.ToString(), "DivIDEo Studio — ошибка"); }
    }
}
class Options {
    public string Input, Output, Name = "Video", Caption = "";
    public double Start, Duration, Contrast = 5.5, Brightness = 0.3, Volume = 1;
    public int Left, Right, Top, Bottom, Fit, Border, Dither = 4, Frames, AudioIndex;
    public bool Colour = true, Mute;
}
class Media {
    public int Width, Height; public double Duration; public List<string> Audio = new List<string>();
}
static class Engine {
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static string N(double v) { return v.ToString("0.########", Inv); }
    public static string Q(string s) {
        var b=new StringBuilder("\""); int slashes=0;
        foreach(char c in s) { if(c=='\\') { slashes++; continue; } if(c=='\"') { b.Append('\\',slashes*2+1); b.Append(c); } else { b.Append('\\',slashes); b.Append(c); } slashes=0; }
        b.Append('\\',slashes*2); return b.Append('"').ToString();
    }
    public static string Tool(string name) {
        string dir = Path.Combine(Path.GetTempPath(), "DivideoStudio-tools-1.0");
        Directory.CreateDirectory(dir); string file = Path.Combine(dir, name);
        lock (typeof(Engine)) {
            using (var stream = typeof(Engine).Assembly.GetManifestResourceStream(name)) {
                if (stream == null) throw new Exception("Нет встроенной утилиты " + name);
                if (!File.Exists(file) || new FileInfo(file).Length != stream.Length) {
                    string tmp = file + "." + Guid.NewGuid().ToString("N");
                    using (var output = File.Create(tmp)) stream.CopyTo(output);
                    if (File.Exists(file)) File.Delete(file); File.Move(tmp, file);
                }
            }
        }
        return file;
    }
    public static string Run(string tool, string args, string cwd, CancellationToken token, Action<string> log) {
        token.ThrowIfCancellationRequested();
        var output = new StringBuilder(); var errors = new Queue<string>();
        using (var p = new Process()) {
            p.StartInfo = new ProcessStartInfo(Tool(tool), args) { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = cwd ?? Path.GetTempPath(),
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            p.OutputDataReceived += (s,e) => { if(e.Data != null) { lock(output) output.AppendLine(e.Data); if(log != null) log(e.Data); } };
            p.ErrorDataReceived += (s,e) => { if(e.Data != null) { lock(errors) { errors.Enqueue(e.Data); if(errors.Count > 20) errors.Dequeue(); } if(log != null) log(e.Data); } };
            p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
            using(token.Register(() => { try { if(!p.HasExited) p.Kill(); } catch {} })) { p.WaitForExit(); }
            token.ThrowIfCancellationRequested();
            if(p.ExitCode != 0) throw new Exception(tool + " завершился с кодом " + p.ExitCode + "\r\n" + string.Join("\r\n", errors));
        }
        return output.ToString();
    }
    public static Media Probe(string path, CancellationToken token) {
        string json = Run("ffprobe.exe", "-v error -show_streams -show_format -of json " + Q(path), null, token, null);
        var root = new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);
        var result = new Media();
        foreach(var item in (System.Collections.IEnumerable)root["streams"]) {
            var s = (Dictionary<string,object>)item; string type = Convert.ToString(s["codec_type"]);
            if(type == "video" && result.Width == 0) { result.Width = Convert.ToInt32(s["width"]); result.Height = Convert.ToInt32(s["height"]); }
            if(type == "audio") { result.Audio.Add("Дорожка " + (result.Audio.Count + 1) + " • " + (s.ContainsKey("codec_name") ? s["codec_name"] : "audio") + " • " + (s.ContainsKey("channels") ? s["channels"] : "?") + " кан."); }
        }
        if(root.ContainsKey("format")) { var f = (Dictionary<string,object>)root["format"]; if(f.ContainsKey("duration")) double.TryParse(Convert.ToString(f["duration"]), NumberStyles.Float, Inv, out result.Duration); }
        if(result.Width == 0 || result.Height == 0 || result.Duration <= 0) throw new Exception("Не удалось определить видеопоток и длительность файла.");
        return result;
    }
    public static Bitmap SourceFrame(string path,double time,CancellationToken token) {
        string file=Path.Combine(Path.GetTempPath(),"DivideoFrame-"+Guid.NewGuid()+".png");
        try { Run("ffmpeg.exe","-hide_banner -loglevel error -nostdin -y -ss "+N(time)+" -i "+Q(path)+" -map 0:v:0 -frames:v 1 -vf "+Q("scale=960:540:force_original_aspect_ratio=decrease,scale=trunc(iw*sar):ih,setsar=1")+" "+Q(file),null,token,null); using(var image=Image.FromFile(file))return new Bitmap(image); }
        finally { if(File.Exists(file))File.Delete(file); }
    }
    public static string Filter(Options o) {
        string f = "crop=iw-" + (o.Left+o.Right) + ":ih-" + (o.Top+o.Bottom) + ":" + o.Left + ":" + o.Top + ":exact=1,scale=trunc(iw*sar):ih,setsar=1,";
        if(o.Fit == 0) f += "scale=256:192:force_original_aspect_ratio=decrease,pad=256:192:(ow-iw)/2:(oh-ih)/2:black";
        else if(o.Fit == 1) f += "scale=256:192:force_original_aspect_ratio=increase,crop=256:192";
        else f += "scale=256:192";
        f += ",setsar=1,fps=25";
        if(!string.IsNullOrWhiteSpace(o.Caption)) f += ",drawtext=fontfile='" + Environment.GetFolderPath(Environment.SpecialFolder.Fonts).Replace("\\","/").Replace(":","\\:") + "/arial.ttf':textfile=caption.txt:expansion=none:fontsize=14:fontcolor=white:box=1:boxcolor=black@0.75:x=(w-text_w)/2:y=h-text_h-8";
        return f;
    }
    public static void Validate(Options o, Media m) {
        if(!File.Exists(o.Input)) throw new Exception("Выберите существующий видеофайл.");
        if(o.Start < 0 || o.Duration < 0.04 || o.Start + o.Duration > m.Duration + 0.03) throw new Exception("Фрагмент должен находиться внутри видео и длиться минимум 0,04 секунды.");
        if(o.Left + o.Right >= m.Width || o.Top + o.Bottom >= m.Height) throw new Exception("Обрезка сторон удаляет весь кадр.");
        if(o.Name.Length > 32 || o.Name.Any(c => c < 32 || c > 126)) throw new Exception("Название DVO: до 32 символов, латиница, цифры и знаки ASCII.");
        if(!o.Mute && m.Audio.Count > 0 && (o.AudioIndex < 0 || o.AudioIndex >= m.Audio.Count)) throw new Exception("Выберите звуковую дорожку.");
    }
    public static void ConvertVideo(Options o, Media m, string destination, CancellationToken token, Action<string> log, Action<int> progress) {
        Validate(o,m); string dir = Path.Combine(Path.GetTempPath(), "DivideoJob-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try {
            double duration = o.Frames > 0 ? Math.Min(o.Duration, o.Frames / 25.0) : o.Duration;
            File.WriteAllText(Path.Combine(dir,"caption.txt"),o.Caption,new UTF8Encoding(false));
            bool silence = o.Mute || m.Audio.Count == 0;
            string a = "-hide_banner -loglevel error -nostdin -y -ss " + N(o.Start) + " -i " + Q(o.Input);
            if(silence) a += " -f lavfi -i anullsrc=r=44100:cl=mono";
            a += " -t " + N(duration) + " -map 0:v:0 -map " + (silence ? "1:a:0" : "0:a:" + o.AudioIndex) + " -vf " + Q(Filter(o));
            a += " -af apad -c:v rawvideo -pix_fmt bgr24 -c:a pcm_s16le -ar 44100 -ac 1 -progress pipe:1 " + Q(Path.Combine(dir,"input.avi"));
            log("Подготовка видео: 256×192, 25 fps, PCM mono…");
            Run("ffmpeg.exe", a, dir, token, line => { if(line.StartsWith("out_time_us=")) { double us; if(double.TryParse(line.Substring(12), out us)) progress(Math.Min(44, (int)(us/1000000/duration*44))); } else if(!line.Contains("=")) log(line); });
            log("Кодирование DVO…");
            string d = (o.Colour ? "-c" : "-m") + " -b " + o.Border + " -d " + o.Dither + " -k " + N(o.Contrast) + " -l " + N(o.Brightness) + " -v " + N(o.Volume) + " -n " + Q(o.Name);
            if(o.Frames > 0) d += " -f " + o.Frames;
            d += " input.avi output.dvo";
            Run("divideo.exe",d,dir,token,line => { if(line.StartsWith("frame ")) { int frame; string first = line.Split('\t')[0].Substring(6); if(int.TryParse(first,out frame)) progress(Math.Min(99,44+(int)(55*frame/Math.Max(1,duration*25)))); } else log(line); });
            string result = Path.Combine(dir,"output.dvo");
            // The original encoder omits unused padding in its final disk sector.
            using(var file = new FileStream(result,FileMode.Open,FileAccess.Write)) { file.SetLength(((file.Length+511)/512)*512); }
            CheckDvo(result);
            token.ThrowIfCancellationRequested(); File.Copy(result,destination,true); progress(100);
        } finally { try { Directory.Delete(dir,true); } catch {} }
    }
    public static int CheckDvo(string path) {
        using(var f = File.OpenRead(path)) {
            var h = new byte[512]; if(f.Read(h,0,512) != 512 || Encoding.ASCII.GetString(h,0,8) != "DivIDEog" || h[8] != 1) throw new Exception("Конвертер создал некорректный заголовок DVO.");
            int sectors=h[9], count=0;
            while(sectors > 0) {
                long end=f.Position+sectors*512; if(end>f.Length) throw new Exception("DVO обрывается внутри кадра.");
                bool marker=false;
                while(f.Position<end) {
                    long pos=f.Position; int lo=f.ReadByte(), hi=f.ReadByte(), cmd=f.ReadByte(), sound=f.ReadByte();
                    if(sound<0 || cmd<0 || cmd%2!=0) throw new Exception("Некорректный пакет DVO.");
                    if(cmd==0) { sectors=lo; marker=true; break; }
                    int len=(254-cmd)/2; if((pos%512)+4+len>512 || (4+len)%2!=0) throw new Exception("Нарушена граница пакета DVO.");
                    f.Position+=len;
                }
                if(!marker) throw new Exception("В DVO отсутствует конец кадра."); f.Position=end; count++;
            }
            if(count==0) throw new Exception("В DVO нет кадров."); return count;
        }
    }
    public static Bitmap DecodePreview(string path) {
        byte[] b=File.ReadAllBytes(path); byte[][] screens={new byte[6912],new byte[6912]}; int sectors=b[9], offset=512, frame=0;
        while(sectors>0 && frame<2) { int end=offset+sectors*512, next=0; while(offset<end) { int address=b[offset]+256*b[offset+1]-0xc000, code=b[offset+2]; if(code==0) { next=b[offset]; break; } int len=(254-code)/2; for(int j=0;j<len;j++) if(address+j>=0 && address+j<6912) screens[frame%2][address+j]=b[offset+4+j]; offset+=4+len; } sectors=next; offset=end; frame++; }
        byte[] screen=screens[Math.Max(0,frame-1)%2]; var bmp=new Bitmap(256,192);
        for(int y=0;y<192;y++) for(int x=0;x<256;x++) { int at=screen[6144+(y/8)*32+x/8], bits=screen[(y&192)*32+(y&7)*256+(y&56)*4+x/8]; int c=(bits&(128>>(x%8)))!=0 ? at&7 : (at>>3)&7; int v=(at&64)!=0?255:205; bmp.SetPixel(x,y,Color.FromArgb((c&2)!=0?v:0,(c&4)!=0?v:0,(c&1)!=0?v:0)); }
        return bmp;
    }
}

class MainForm : Form {
    TextBox input=new TextBox(), output=new TextBox(), name=new TextBox(), caption=new TextBox(), log=new TextBox();
    TimeBox start=new TimeBox(), end=new TimeBox(), duration=new TimeBox();
    Timeline timeline=new Timeline(); CropView sourceView=new CropView(), geometryView=new CropView();
    CheckBox cropResult=new CheckBox { Text="Показать результат обрезки и вписывания (4:3)",AutoSize=true };
    Label playTime=new Label { AutoSize=true }, frameState=new Label { AutoSize=true };
    System.Windows.Forms.Timer seekTimer=new System.Windows.Forms.Timer { Interval=140 };
    CancellationTokenSource frameCts; Bitmap sourceBitmap; double playPosition; int frameVersion; bool closing;
    NumericUpDown left=Num(0,32000,0,0),right=Num(0,32000,0,0),top=Num(0,32000,0,0),bottom=Num(0,32000,0,0);
    NumericUpDown contrast=Num(0.01m,100,5.5m,2), brightness=Num(0,1,0.3m,2), volume=Num(0,20,1,2), frames=Num(0,10000000,0,0);
    ComboBox fit=Combo("Вписать с полями","Заполнить с обрезкой","Растянуть"), colour=Combo("Цветное","Чёрно-белое"),dither=Combo("4 × 4","2 × 2","Без дизеринга"),border=Combo("0 • Чёрный","1 • Синий","2 • Красный","3 • Пурпурный","4 • Зелёный","5 • Голубой","6 • Жёлтый","7 • Белый"),audio=Combo("Нет дорожек");
    CheckBox mute=new CheckBox { Text="Без звука",AutoSize=true };
    Button convert=new Button(), cancel=new Button(), preview=new Button(), browse=new Button();
    PictureBox picture=new PictureBox { Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.Black };
    Label info=new Label(), status=new Label(); ProgressBar progress=new ProgressBar();
    Panel settings=new Panel(); Media media; string loaded; bool linking,busy; CancellationTokenSource cts;
    static NumericUpDown Num(decimal min,decimal max,decimal val,int dp) { return new NumericUpDown { Minimum=min,Maximum=max,Value=val,DecimalPlaces=dp,Increment=dp==0?1:0.04m,Width=115 }; }
    static ComboBox Combo(params string[] items) { var c=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,Width=225 }; c.Items.AddRange(items); c.SelectedIndex=0; return c; }
    static Label Label(string text) { return new Label { Text=text, AutoSize=true,Margin=new Padding(3,7,8,3) }; }
    static FlowLayoutPanel Row(params Control[] controls) { var f=new FlowLayoutPanel { AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,WrapContents=true,Padding=new Padding(6) }; f.Controls.AddRange(controls); return f; }
    static GroupBox Group(string title, params Control[] rows) { var g=new GroupBox { Text=title,AutoSize=true,Dock=DockStyle.Top,Padding=new Padding(8,22,8,8) }; var t=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=true,ColumnCount=1 }; foreach(var r in rows) t.Controls.Add(r); g.Controls.Add(t); return g; }
    static void AddSection(TabControl tabs, GroupBox group) { var page=new TabPage(group.Text.Substring(5)) { AutoScroll=true,Padding=new Padding(12),BackColor=Color.FromArgb(244,246,250) }; page.Controls.Add(group); tabs.TabPages.Add(page); }
    public MainForm(string initial) {
        Text="DivIDEo Studio • Видео для ZX Spectrum"; Font=new Font("Segoe UI",10); ClientSize=new Size(1120,870); MinimumSize=new Size(980,760); StartPosition=FormStartPosition.CenterScreen; AutoScaleMode=AutoScaleMode.Dpi; BackColor=Color.FromArgb(244,246,250);
        var root=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(18) }; root.RowStyles.Add(new RowStyle(SizeType.Absolute,60)); root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,75)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,75)); Controls.Add(root);
        root.Controls.Add(new Label { Text="DivIDEo Studio\nВидео → ZX Spectrum 128K + DivIDE • 256 × 192 • 25 fps",Font=new Font("Segoe UI",13,FontStyle.Bold),Dock=DockStyle.Fill },0,0);
        settings.Dock=DockStyle.Fill; settings.AutoScroll=true; root.Controls.Add(settings,0,1);
        var content=new TabControl { Dock=DockStyle.Fill,Padding=new Point(14,8) }; settings.Controls.Add(content);
        input.Width=720; input.ReadOnly=true; output.Width=720; name.Width=300; name.MaxLength=32; caption.Width=500;
        browse.Text="Открыть…"; browse.AutoSize=true; browse.Click+=(s,e)=>Choose();
        var save=new Button { Text="Сохранить…",AutoSize=true }; save.Click+=(s,e)=> { using(var d=new SaveFileDialog { Filter="DivIDEo (*.dvo)|*.dvo",FileName=output.Text }) if(d.ShowDialog()==DialogResult.OK) output.Text=d.FileName; };
        info.AutoSize=true; info.Text="Перетащите видео в окно или на EXE приложения.";
        AddSection(content,Group("01 / Файлы",Row(Label("Источник"),input,browse),Row(Label("Результат"),output,save),Row(info),Row(Label("MP4, MKV, AVI, MOV, WebM и другие форматы, поддерживаемые FFmpeg.")),Row(Label("Выход: DVO для Spectrum 128K + DivIDE. Частота автоматически приводится к 25 fps."))));
        start.ValueChanged+=(s,e)=>SyncTime(0); end.ValueChanged+=(s,e)=>SyncTime(1); duration.ValueChanged+=(s,e)=>SyncTime(2);
        var markStart=new Button { Text="Начало здесь",AutoSize=true }; markStart.Click+=(s,e)=> { if(media!=null)start.Value=(decimal)Math.Min(playPosition,Math.Max(0,(double)end.Value-.04)); };
        var markEnd=new Button { Text="Конец здесь",AutoSize=true }; markEnd.Click+=(s,e)=> { if(media!=null)end.Value=(decimal)Math.Max(playPosition,Math.Min(media.Duration,(double)start.Value+.04)); };
        AddSection(content,Group("02 / Фрагмент",Row(sourceView),Row(playTime,markStart,markEnd,frameState),Row(timeline),Row(Label("Начало"),start,Label("Конец"),end,Label("Длительность"),duration),Row(Label("Лимит кадров (0 = весь фрагмент)"),frames),Row(Label("Верхняя дорожка — кадр. Нижние маркеры — границы. Время: чч:мм:сс.мс (или мм:сс)."))));
        AddSection(content,Group("03 / Геометрия",Row(geometryView),Row(cropResult),Row(Label("Режим 4:3"),fit),Row(Label("Убрать пикселей: слева"),left,Label("справа"),right,Label("сверху"),top,Label("снизу"),bottom),Row(Label("Затемнённая область будет удалена. Кадр выбирается на вкладке «Фрагмент»."))));
        timeline.Changed+=(kind,time)=> { if(media==null)return; if(kind==2)start.Value=(decimal)time; else if(kind==3)end.Value=(decimal)time; Seek(time); };
        seekTimer.Tick+=async(s,e)=> { seekTimer.Stop(); await RefreshSource(); };
        foreach(var control in new[]{left,right,top,bottom})control.ValueChanged+=(s,e)=>UpdateCrop();
        fit.SelectedIndexChanged+=(s,e)=>UpdateCrop(); cropResult.CheckedChanged+=(s,e)=>UpdateCrop();
        AddSection(content,Group("04 / Изображение",Row(colour,Label("Дизеринг"),dither),Row(Label("Бордюр"),border),Row(Label("Контраст"),contrast,Label("Уровень яркости"),brightness),Row(Label("Название в меню DVO (ASCII)"),name),Row(Label("Титр на изображении"),caption),Row(Label("Название: до 32 символов ASCII. Титр поддерживает кириллицу и выводится внизу кадра."))));
        AddSection(content,Group("05 / Звук",Row(audio,mute,Label("Громкость ×"),volume),Row(Label("Звук автоматически переводится в mono. При отсутствии дорожки создаётся тишина."))));
        preview.Text="Предпросмотр DVO"; preview.AutoSize=true; preview.Click+=async(s,e)=>await Work(true);
        var imagePanel=new Panel { Width=512,Height=384 }; imagePanel.Controls.Add(picture);
        AddSection(content,Group("06 / Предпросмотр",Row(preview),Row(Label("Кадр у начала выбранного фрагмента; со всеми эффектами конвертера.")),Row(imagePanel)));
        log.Multiline=true; log.ReadOnly=true; log.ScrollBars=ScrollBars.Vertical; log.Dock=DockStyle.Fill; log.Font=new Font("Consolas",9); root.Controls.Add(log,0,2);
        convert.Text="Конвертировать в DVO"; convert.Width=235; convert.Height=36; convert.BackColor=Color.FromArgb(36,83,199); convert.ForeColor=Color.White; convert.FlatStyle=FlatStyle.Flat; convert.Click+=async(s,e)=>await Work(false);
        cancel.Text="Отмена"; cancel.Enabled=false; cancel.Height=36; cancel.Click+=(s,e)=> { if(cts!=null) cts.Cancel(); };
        progress.Width=220; progress.Height=28; status.AutoSize=true; status.Text="Готов к работе"; root.Controls.Add(Row(convert,cancel,progress,status),0,3);
        AddDrop(this); Shown+=async(s,e)=> { if(!string.IsNullOrEmpty(initial)) await LoadVideo(initial); };
        FormClosing+=(s,e)=> { if(busy) { e.Cancel=true; if(cts!=null) cts.Cancel(); status.Text="Остановка… затем закройте окно"; } };
        FormClosed+=(s,e)=> { closing=true; seekTimer.Stop(); seekTimer.Dispose(); frameVersion++; if(frameCts!=null)frameCts.Cancel(); if(sourceBitmap!=null)sourceBitmap.Dispose(); };
    }
    void AddDrop(Control c) { c.AllowDrop=true; c.DragEnter+=(s,e)=> { if(!busy && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect=DragDropEffects.Copy; }; c.DragDrop+=async(s,e)=> { var paths=e.Data.GetData(DataFormats.FileDrop) as string[]; if(!busy && paths!=null && paths.Length>0) await LoadVideo(paths[0]); }; foreach(Control child in c.Controls) AddDrop(child); }
    async void Choose() { using(var d=new OpenFileDialog { Filter="Видео|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.mpeg;*.mpg;*.wmv;*.m4v;*.ts|Все файлы|*.*" }) if(d.ShowDialog()==DialogResult.OK) await LoadVideo(d.FileName); }
    void SyncTime(int which) { if(linking || media==null) return; linking=true; try { decimal max=(decimal)media.Duration; if(start.Value>max) start.Value=max; if(which==2) end.Value=Math.Min(max,start.Value+duration.Value); else { if(end.Value>max) end.Value=max; if(end.Value<start.Value) end.Value=start.Value; } duration.Value=end.Value-start.Value; } finally { linking=false; } UpdateTimeline(); Seek(which==0?(double)start.Value:(double)end.Value); }
    void UpdateTimeline() { timeline.Total=media==null?1:media.Duration; timeline.Start=(double)start.Value; timeline.End=(double)end.Value; timeline.Position=playPosition; timeline.Invalidate(); playTime.Text="Кадр: "+TimeBox.Format(playPosition); }
    void Seek(double time) { if(media==null)return; playPosition=Math.Max(0,Math.Min(media.Duration,time)); UpdateTimeline(); frameVersion++; if(frameCts!=null)frameCts.Cancel(); seekTimer.Stop(); seekTimer.Start(); frameState.Text="Загрузка кадра…"; }
    internal async Task RefreshSource() {
        seekTimer.Stop(); if(media==null||closing)return; int version=++frameVersion; if(frameCts!=null)frameCts.Cancel(); var cancelFrame=new CancellationTokenSource(); frameCts=cancelFrame; string path=loaded; double time=Math.Min(playPosition,Math.Max(0,media.Duration-.1));
        try { var bitmap=await Task.Run(()=>Engine.SourceFrame(path,time,cancelFrame.Token)); if(closing||version!=frameVersion) { bitmap.Dispose(); return; } var old=sourceBitmap; sourceBitmap=bitmap; sourceView.Frame=geometryView.Frame=bitmap; UpdateCrop(); if(old!=null)old.Dispose(); frameState.Text="Исходный кадр"; }
        catch(OperationCanceledException) {} catch(Exception ex) { if(!closing&&version==frameVersion)frameState.Text="Не удалось прочитать кадр: "+ex.Message.Split('\n')[0]; }
        finally { if(frameCts==cancelFrame)frameCts=null; cancelFrame.Dispose(); }
    }
    void UpdateCrop() { if(media==null)return; foreach(var view in new[]{sourceView,geometryView}) { view.SourceWidth=media.Width; view.SourceHeight=media.Height; view.LeftCrop=(int)left.Value; view.RightCrop=(int)right.Value; view.TopCrop=(int)top.Value; view.BottomCrop=(int)bottom.Value; view.Fit=fit.SelectedIndex; view.Result=cropResult.Checked; view.Invalidate(); } }
    void SetBusy(bool value) { busy=value; settings.Enabled=!value; convert.Enabled=!value; cancel.Enabled=value; }
    void UI(Action action) { if(!IsDisposed && IsHandleCreated) BeginInvoke(action); }
    void Log(string s) { UI(()=> { if(log.TextLength>40000) log.Clear(); log.AppendText(s+Environment.NewLine); }); }
    internal async Task LoadVideo(string path) {
        if(busy)return; seekTimer.Stop(); frameVersion++; if(frameCts!=null)frameCts.Cancel(); SetBusy(true); cts=new CancellationTokenSource(); status.Text="Чтение видео…";
        try { var m=await Task.Run(()=>Engine.Probe(path,cts.Token)); media=m; loaded=Path.GetFullPath(path); input.Text=loaded; output.Text=Path.ChangeExtension(loaded,".dvo"); name.Text=new string(Path.GetFileNameWithoutExtension(path).Where(c=>c>=32 && c<=126).Take(32).ToArray()).Trim(); if(name.Text.Length==0) name.Text="Video";
            linking=true; start.Value=0; end.Value=Math.Min(end.Maximum,(decimal)m.Duration); duration.Value=end.Value; linking=false; left.Value=right.Value=top.Value=bottom.Value=0;
            audio.Items.Clear(); audio.Items.AddRange(m.Audio.Count>0?m.Audio.ToArray():new[]{"Нет дорожек — тишина"}); audio.SelectedIndex=0;
            info.Text=m.Width+" × "+m.Height+" • "+TimeBox.Format(m.Duration)+" • звуковых дорожек: "+m.Audio.Count; status.Text="Видео загружено"; if(picture.Image!=null) { picture.Image.Dispose(); picture.Image=null; }
            sourceView.Frame=geometryView.Frame=null; if(sourceBitmap!=null)sourceBitmap.Dispose(); sourceBitmap=null; playPosition=0; UpdateTimeline(); await RefreshSource();
        } catch(OperationCanceledException) { status.Text="Отменено"; } catch(Exception ex) { MessageBox.Show(this,ex.Message,"Не удалось открыть видео",MessageBoxButtons.OK,MessageBoxIcon.Error); status.Text="Ошибка открытия"; }
        finally { cts.Dispose(); cts=null; SetBusy(false); }
    }
    internal async Task VerifyAndCapture(string folder) {
        if(colour.SelectedIndex!=0 || dither.SelectedIndex!=0 || fit.SelectedIndex!=0 || media==null || !AllowDrop) throw new Exception("GUI defaults/loading failed");
        start.Value=0.2m; duration.Value=0.6m; if(end.Value!=0.8m)throw new Exception("Duration linkage failed"); end.Value=1m; if(duration.Value!=0.8m)throw new Exception("End linkage failed");
        Engine.Validate(ReadOptions(),media);
        decimal parsed; if(!TimeBox.Parse("02:00",out parsed)||parsed!=120 || TimeBox.Format(120)!="00:02:00.000" || TimeBox.Parse("00:61:00",out parsed))throw new Exception("Timecode parsing failed");
        timeline.TestDrag(2,.35); timeline.TestDrag(3,1.25); timeline.TestDrag(1,.7);
        if(Math.Abs((double)start.Value-.35)>.01 || Math.Abs((double)end.Value-1.25)>.01 || Math.Abs(playPosition-.7)>.01)throw new Exception("Timeline mouse selection failed");
        await RefreshSource(); if(sourceBitmap==null)throw new Exception("Source preview missing");
        left.Value=31; right.Value=15; top.Value=12; bottom.Value=8;
        if(sourceView.LeftCrop!=31||geometryView.RightCrop!=15)throw new Exception("Live crop failed");
        using(var bmp=new Bitmap(geometryView.Width,geometryView.Height)) { geometryView.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size)); bmp.Save(Path.Combine(folder,"crop-overlay.png")); }
        cropResult.Checked=true;
        using(var bmp=new Bitmap(geometryView.Width,geometryView.Height)) { geometryView.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size)); bmp.Save(Path.Combine(folder,"crop-result.png")); }
        cropResult.Checked=false;
        var tabs=(TabControl)settings.Controls[0];
        for(int i=0;i<tabs.TabPages.Count;i++) { tabs.SelectedIndex=i; Application.DoEvents(); using(var bmp=new Bitmap(Width,Height)) { DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size)); bmp.Save(Path.Combine(folder,"window"+i+".png")); } }
    }
    Options ReadOptions() { return new Options { Input=loaded,Output=output.Text,Name=name.Text,Caption=caption.Text,Start=(double)start.Value,Duration=(double)duration.Value,Left=(int)left.Value,Right=(int)right.Value,Top=(int)top.Value,Bottom=(int)bottom.Value,Fit=fit.SelectedIndex,Colour=colour.SelectedIndex==0,Border=border.SelectedIndex,Dither=new[]{4,2,1}[dither.SelectedIndex],Contrast=(double)contrast.Value,Brightness=(double)brightness.Value,Volume=(double)volume.Value,Frames=(int)frames.Value,AudioIndex=audio.SelectedIndex,Mute=mute.Checked }; }
    internal async Task Work(bool isPreview) {
        if(busy)return; string staged=null;
        try {
            if(!ValidateChildren())throw new Exception("Введите время в формате чч:мм:сс.мс или мм:сс.");
            if(media==null) throw new Exception("Сначала выберите видеофайл."); var o=ReadOptions(); Engine.Validate(o,media);
            if(!isPreview) { o.Output=Path.GetFullPath(o.Output); if(!string.Equals(Path.GetExtension(o.Output),".dvo",StringComparison.OrdinalIgnoreCase)) throw new Exception("Укажите выходной файл с расширением .dvo."); if(string.Equals(o.Output,o.Input,StringComparison.OrdinalIgnoreCase)) throw new Exception("Источник и результат должны быть разными файлами."); if(!Directory.Exists(Path.GetDirectoryName(o.Output))) throw new Exception("Папка результата не существует."); if(File.Exists(o.Output) && MessageBox.Show(this,"Заменить существующий файл?\n"+o.Output,"Перезапись DVO",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return; }
            else { o.Duration=Math.Min(o.Duration,0.08); o.Frames=2; }
            staged=isPreview?Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".dvo"):Path.Combine(Path.GetDirectoryName(o.Output),"."+Guid.NewGuid()+".dvo.tmp");
            SetBusy(true); cts=new CancellationTokenSource(); progress.Value=0; log.Clear(); status.Text=isPreview?"Предпросмотр…":"Конвертация…";
            await Task.Run(()=>Engine.ConvertVideo(o,media,staged,cts.Token,Log,p=>UI(()=>progress.Value=p)));
            if(isPreview) { var bitmap=Engine.DecodePreview(staged); if(picture.Image!=null)picture.Image.Dispose(); picture.Image=bitmap; status.Text="Предпросмотр готов"; }
            else { if(File.Exists(o.Output)) File.Replace(staged,o.Output,null); else File.Move(staged,o.Output); status.Text="DVO готов"; Log("Сохранено: "+o.Output); MessageBox.Show(this,"Готово!\n"+o.Output+"\n\nРазмер: "+(new FileInfo(o.Output).Length/1048576.0).ToString("0.00")+" МБ","DivIDEo Studio",MessageBoxButtons.OK,MessageBoxIcon.Information); }
        } catch(OperationCanceledException) { status.Text="Отменено"; Log("Конвертация отменена."); }
        catch(Exception ex) { status.Text="Ошибка"; Log(ex.Message); MessageBox.Show(this,ex.Message,"DivIDEo Studio",MessageBoxButtons.OK,MessageBoxIcon.Error); }
        finally { if(staged!=null && File.Exists(staged)) File.Delete(staged); if(cts!=null) { cts.Dispose(); cts=null; } SetBusy(false); }
    }
}
static class SelfTest {
    public static void Run(string folder) {
        Directory.CreateDirectory(folder); string report=Path.Combine(folder,"report.txt");
        try {
            var lines=new List<string>(); var token=CancellationToken.None;
            string source=Path.Combine(folder,"тест видео.mp4");
            Engine.Run("ffmpeg.exe","-hide_banner -loglevel error -y -f lavfi -i testsrc2=size=320x240:rate=30 -f lavfi -i sine=frequency=440:sample_rate=48000 -t 1.5 -c:v libx264 -pix_fmt yuv420p -c:a aac "+Engine.Q(source),folder,token,null);
            var m=Engine.Probe(source,token); lines.Add("Probe: "+m.Width+"x"+m.Height+" audio="+m.Audio.Count);
            for(int mode=0;mode<3;mode++) {
                var o=new Options { Input=source,Name="Test "+mode,Caption=mode==1?"Тест: ZX 100%":"",Start=0.2,Duration=0.6,Left=4,Right=4,Top=2,Bottom=2,Fit=mode,Colour=mode!=1,Mute=mode==2,Dither=new[]{4,2,1}[mode],Frames=mode==2?5:0,Border=mode,Volume=0.8 };
                string dst=Path.Combine(folder,"mode"+mode+".dvo"); Engine.ConvertVideo(o,m,dst,token,s=>{},p=>{}); int count=Engine.CheckDvo(dst); if(count!=(mode==2?5:15))throw new Exception("Unexpected frames "+count); lines.Add("Mode "+mode+": "+count+" frames; valid DVO"); using(var bmp=Engine.DecodePreview(dst)) bmp.Save(Path.Combine(folder,"preview"+mode+".png"));
            }
            string silent=Path.Combine(folder,"silent.mp4"); Engine.Run("ffmpeg.exe","-hide_banner -loglevel error -y -i "+Engine.Q(source)+" -an -c:v copy "+Engine.Q(silent),folder,token,null);
            var sm=Engine.Probe(silent,token); Engine.ConvertVideo(new Options { Input=silent,Duration=0.2 },sm,Path.Combine(folder,"silent.dvo"),token,s=>{},p=>{}); lines.Add("No-audio input: valid DVO");
            using(var cancel=new CancellationTokenSource()) { cancel.CancelAfter(300); try { Engine.Run("ffmpeg.exe","-hide_banner -loglevel error -re -f lavfi -i testsrc2 -t 30 -f null -",folder,cancel.Token,null); throw new Exception("Cancellation failed"); } catch(OperationCanceledException) { lines.Add("Active process cancellation: OK"); } }
            using(var form=new MainForm(null)) { form.Show(); Application.DoEvents(); var loading=form.LoadVideo(source); while(!loading.IsCompleted) { Application.DoEvents(); Thread.Sleep(10); } loading.GetAwaiter().GetResult(); var preview=form.Work(true); while(!preview.IsCompleted) { Application.DoEvents(); Thread.Sleep(10); } preview.GetAwaiter().GetResult(); var verify=form.VerifyAndCapture(folder); while(!verify.IsCompleted) { Application.DoEvents(); Thread.Sleep(10); } verify.GetAwaiter().GetResult(); form.Close(); lines.Add("GUI source seek, timeline mouse handles, timecodes, live crop, DVO preview and screenshots: OK"); }
            lines.Add("PASS"); File.WriteAllLines(report,lines);
        } catch(Exception ex) { File.WriteAllText(report,"FAIL\r\n"+ex); Environment.ExitCode=1; }
    }
}
}
