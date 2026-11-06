using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Program {
    [STAThread] static int Main(string[] args) {
        if(args.Length==4 && args[0]=="--self-test") {
            try { SelfTest(args[1],args[2],args[3]);return 0; }
            catch(Exception e) { File.WriteAllText(args[3],"FAIL\r\n"+e,Encoding.UTF8);return 1; }
        }
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        if(args.Length==2 && args[0]=="--render-preview") {
            using(var form=new InstallerForm()) {
                form.ShowInTaskbar=false;form.Opacity=0;form.Show();Application.DoEvents();
                using(var bitmap=new Bitmap(form.Width,form.Height)) {
                    form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));
                    bitmap.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            return 0;
        }
        Application.Run(new InstallerForm());return 0;
    }
    internal static byte[] Resource(string name) {
        using(var source=Assembly.GetExecutingAssembly().GetManifestResourceStream("CWZh."+name)) {
            if(source==null)throw new InvalidDataException("安裝檔缺少必要資料："+name);
            using(var zip=new GZipStream(source,CompressionMode.Decompress))
            using(var output=new MemoryStream()) { zip.CopyTo(output);return output.ToArray(); }
        }
    }
    internal static string Hash(byte[] bytes) {
        using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
    }
    internal static string HashFile(string path) { return Hash(File.ReadAllBytes(path)); }
    internal static Dictionary<string,byte[]> LoadPayload() {
        var result=new Dictionary<string,byte[]>();
        foreach(var entry in Build.Files) {
            var bytes=Resource(entry.Key);
            if(Hash(bytes)!=entry.Value)throw new InvalidDataException("安裝檔資料不完整，請重新取得安裝程式。");
            result.Add(entry.Key,bytes);
        }
        return result;
    }
    internal static byte[] ApplyDelta(byte[] original, byte[] delta) {
        using(var reader=new BinaryReader(new MemoryStream(delta)))
        using(var output=new MemoryStream()) {
            if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="CWD1")throw new InvalidDataException("補丁格式不符。");
            uint length=reader.ReadUInt32();if(length>100000000)throw new InvalidDataException("補丁大小不符。");
            while(true) {
                byte op=reader.ReadByte();if(op==(byte)'E')break;
                if(op==(byte)'C') {
                    uint offset=reader.ReadUInt32(),count=reader.ReadUInt32();
                    if((long)offset+count>original.Length)throw new InvalidDataException("補丁範圍不符。");
                    output.Write(original,(int)offset,(int)count);
                } else if(op==(byte)'L') {
                    uint count=reader.ReadUInt32();if(count>length)throw new InvalidDataException("補丁長度不符。");
                    byte[] data=reader.ReadBytes((int)count);if(data.Length!=count)throw new InvalidDataException("補丁資料不完整。");output.Write(data,0,data.Length);
                } else throw new InvalidDataException("補丁指令不符。");
                if(output.Length>length)throw new InvalidDataException("補丁輸出過長。");
            }
            if(output.Length!=length || reader.BaseStream.Position!=reader.BaseStream.Length)throw new InvalidDataException("補丁輸出長度不符。");
            return output.ToArray();
        }
    }
    internal static string AssemblyPath(string folder) {return Path.Combine(folder,"Cursed Words_Data","Managed","Assembly-CSharp.dll");}
    static void CheckGame(string folder,bool allowPatched) {
        if(!File.Exists(Path.Combine(folder,"Cursed Words.exe"))||!File.Exists(AssemblyPath(folder)))
            throw new InvalidOperationException("這個資料夾不是 Cursed Words 的遊戲資料夾。請選取包含 Cursed Words.exe 的位置。");
        string sha=HashFile(AssemblyPath(folder));
        if(sha!=Build.Original && !(allowPatched&&sha==Build.Patched))
            throw new InvalidOperationException("遊戲版本與本中文化補丁不符，尚未修改任何檔案。請取得適用目前遊戲版本的補丁。");
    }
    static void CheckClosed() {
        if(System.Diagnostics.Process.GetProcessesByName("Cursed Words").Length>0)
            throw new InvalidOperationException("請先關閉 Cursed Words，再按一次安裝或還原。");
    }
    internal static void AtomicWrite(string path,byte[] bytes) {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary=path+".cwzh-"+Guid.NewGuid().ToString("N");
        try { File.WriteAllBytes(temporary,bytes); File.Copy(temporary,path,true); }
        finally { if(File.Exists(temporary))File.Delete(temporary); }
    }
    internal static void Install(string folder,bool checkProcess=true) {
        if(checkProcess)CheckClosed();CheckGame(folder,true);
        var payload=LoadPayload();
        string assembly=AssemblyPath(folder);
        string backup=Path.Combine(folder,"Cursed Words_Data","ZhTW-Backup","Assembly-CSharp.dll");
        if(!File.Exists(backup)) {
            if(HashFile(assembly)==Build.Original)AtomicWrite(backup,File.ReadAllBytes(assembly));
            else throw new InvalidOperationException("目前已安裝補丁，但找不到原始檔備份。請先以 Steam 驗證遊戲檔案，再執行此安裝程式。");
        }
        if(HashFile(backup)!=Build.Original)throw new InvalidOperationException("原始檔備份的版本不符，已停止安裝，以免覆寫備份。");
        byte[] patched=ApplyDelta(File.ReadAllBytes(backup),payload["assembly"]);
        if(Hash(patched)!=Build.Patched)throw new InvalidDataException("本機產生的補丁檔案校驗失敗，尚未修改遊戲程式。");
        string data=Path.Combine(folder,"Cursed Words_Data");
        AtomicWrite(Path.Combine(data,"ZhTW","translations.tsv"),payload["translations"]);
        AtomicWrite(Path.Combine(data,"ZhTW","dictionary.tsv"),payload["dictionary"]);
        AtomicWrite(Path.Combine(data,"ZhTW","ECDICT-LICENSE.txt"),payload["license"]);
        AtomicWrite(Path.Combine(data,"Managed","CursedWordsZh.dll"),payload["helper"]);
        // Replace the entry assembly last, after every dependency is in place.
        AtomicWrite(assembly,patched);
        if(HashFile(assembly)!=Build.Patched)throw new IOException("安裝後的檔案檢查未通過，請重新安裝。");
    }
    internal static void Restore(string folder,bool checkProcess=true) {
        if(checkProcess)CheckClosed();CheckGame(folder,true);
        if(HashFile(AssemblyPath(folder))==Build.Original)return;
        string backup=Path.Combine(folder,"Cursed Words_Data","ZhTW-Backup","Assembly-CSharp.dll");
        if(!File.Exists(backup)||HashFile(backup)!=Build.Original)
            throw new InvalidOperationException("找不到正確的原始檔備份，尚未修改遊戲。請使用 Steam 的「驗證遊戲檔案」功能還原。");
        AtomicWrite(AssemblyPath(folder),File.ReadAllBytes(backup));
    }
    internal static string FindGame() {
        var roots=new List<string>();
        using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) {
            if(key!=null && key.GetValue("SteamPath")!=null)roots.Add(key.GetValue("SteamPath").ToString());
        }
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));
        var libraries=new List<string>(roots);
        foreach(var root in roots.Distinct(StringComparer.OrdinalIgnoreCase)) {
            string file=Path.Combine(root,"steamapps","libraryfolders.vdf");
            if(!File.Exists(file))continue;
            foreach(Match match in Regex.Matches(File.ReadAllText(file),"\"path\"\\s+\"([^\"]+)\""))
                libraries.Add(match.Groups[1].Value.Replace(@"\\",@"\"));
        }
        foreach(var library in libraries.Distinct(StringComparer.OrdinalIgnoreCase)) {
            string install="Cursed Words";
            string manifest=Path.Combine(library,"steamapps","appmanifest_3856460.acf");
            if(File.Exists(manifest)) {
                var match=Regex.Match(File.ReadAllText(manifest),"\"installdir\"\\s+\"([^\"]+)\"");
                if(match.Success)install=match.Groups[1].Value;
            }
            string folder=Path.Combine(library,"steamapps","common",install);
            if(File.Exists(AssemblyPath(folder)))return Path.GetFullPath(folder);
        }
        return "";
    }
    static void SelfTest(string root,string original,string report) {
        root=Path.GetFullPath(root);
        if(Directory.Exists(root))throw new InvalidOperationException("Self-test requires a new fixture directory.");
        var lines=new List<string>();var payload=LoadPayload();lines.Add("PASS: all embedded payload SHA-256 checks");
        if(HashFile(original)!=Build.Original)throw new Exception("Incorrect original fixture");
        Directory.CreateDirectory(Path.GetDirectoryName(AssemblyPath(root)));
        File.Copy(original,AssemblyPath(root));File.WriteAllBytes(Path.Combine(root,"Cursed Words.exe"),new byte[0]);
        Install(root,false);
        if(HashFile(AssemblyPath(root))!=Build.Patched)throw new Exception("Install hash mismatch");
        lines.Add("PASS: installation from an unmodified game");
        Install(root,false);lines.Add("PASS: repeat installation");
        foreach(var pair in new[]{new[]{"dictionary","ZhTW/dictionary.tsv"},new[]{"translations","ZhTW/translations.tsv"},new[]{"helper","Managed/CursedWordsZh.dll"}})
            if(HashFile(Path.Combine(root,"Cursed Words_Data",pair[1]))!=Build.Files[pair[0]])throw new Exception("Payload mismatch: "+pair[0]);
        lines.Add("PASS: installed dictionary, translations and helper");
        Restore(root,false);if(HashFile(AssemblyPath(root))!=Build.Original)throw new Exception("Restore hash mismatch");
        lines.Add("PASS: exact restoration of original assembly");
        File.WriteAllText(AssemblyPath(root),"unsupported game version");bool blocked=false;
        try {Install(root,false);}catch(InvalidOperationException){blocked=true;}
        if(!blocked||File.ReadAllText(AssemblyPath(root))!="unsupported game version")throw new Exception("Version guard failed");
        lines.Add("PASS: unsupported versions rejected without mutation");
        if(Encoding.UTF8.GetString(payload["dictionary"]).Count(c=>c=='\n')<300000)throw new Exception("Dictionary too small");
        lines.Add("PASS: offline dictionary contains more than 300,000 entries");
        File.WriteAllLines(report,lines,Encoding.UTF8);
    }
}

internal sealed class InstallerForm : Form {
    readonly TextBox folder=new TextBox();
    readonly Label status=new Label();
    readonly Button install=new Button();
    readonly Button restore=new Button();
    readonly Button browse=new Button();
    public InstallerForm() {
        Text="Cursed Words 繁體中文化";ClientSize=new Size(650,420);MinimumSize=MaximumSize=Size;
        FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
        AutoScaleMode=AutoScaleMode.Dpi;Font=new Font("Microsoft JhengHei UI",10f);BackColor=Color.FromArgb(246,247,249);
        var heading=new Label{Text="Cursed Words 繁體中文化",Font=new Font(Font.FontFamily,18,FontStyle.Bold),AutoSize=true,Location=new Point(28,25)};
        var intro=new Label{Text="繁中介面 ＋ 離線單字辭典\n保留英文字母；送出單字後顯示基本釋義。",AutoSize=true,Location=new Point(30,74)};
        var locationLabel=new Label{Text="遊戲位置",AutoSize=true,Location=new Point(30,139)};
        folder.SetBounds(30,166,494,30);folder.ReadOnly=true;
        browse.Text="選擇資料夾";browse.SetBounds(535,163,90,32);browse.Click+=(s,e)=>Browse();
        status.SetBounds(30,207,590,50);status.ForeColor=Color.FromArgb(64,73,86);
        install.Text="一鍵中文化";install.SetBounds(30,275,360,50);install.BackColor=Color.FromArgb(37,98,185);install.ForeColor=Color.White;install.FlatStyle=FlatStyle.Flat;install.Font=new Font(Font,FontStyle.Bold);
        restore.Text="還原英文";restore.SetBounds(410,275,215,50);
        install.Click+=(s,e)=>Run(false);restore.Click+=(s,e)=>Run(true);
        var coffee=new Button{Text="☕ Buy Me a Coffee",BackColor=Color.FromArgb(255,221,0),ForeColor=Color.Black,FlatStyle=FlatStyle.Flat};
        coffee.SetBounds(30,352,195,38);
        coffee.Click+=(s,e)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://buymeacoffee.com/koehhian"){UseShellExecute=true});
        var optional=new Label{Text="免費使用，贊助完全自願。\n支持與否都不影響功能。",AutoSize=true,Location=new Point(241,351)};
        Controls.AddRange(new Control[]{heading,intro,locationLabel,folder,browse,status,install,restore,coffee,optional});
        AcceptButton=install;
        try {folder.Text=Program.FindGame();RefreshStatus();}catch(Exception e){status.Text=e.Message;}
    }
    void Browse() {
        using(var dialog=new FolderBrowserDialog{Description="選取包含 Cursed Words.exe 的遊戲資料夾",SelectedPath=folder.Text,ShowNewFolderButton=false}) {
            if(dialog.ShowDialog(this)==DialogResult.OK){folder.Text=dialog.SelectedPath;RefreshStatus();}
        }
    }
    void RefreshStatus() {
        install.Enabled=restore.Enabled=folder.Text.Length>0;
        if(folder.Text.Length==0){status.Text="尚未找到遊戲，請選擇遊戲資料夾。";return;}
        try {
            string hash=Program.HashFile(Program.AssemblyPath(folder.Text));
            status.Text=hash==Build.Original?"已找到支援的遊戲版本。按一下即可安裝，原始檔會自動備份。":hash==Build.Patched?"此版本已完成中文化。可重新安裝，或還原英文。":"遊戲版本不符，這份補丁不會修改目前的遊戲。";
            install.Enabled=restore.Enabled=(hash==Build.Original||hash==Build.Patched);
        }catch{status.Text="這裡找不到遊戲程式，請重新選擇資料夾。";install.Enabled=restore.Enabled=false;}
    }
    void Run(bool reverting) {
        install.Enabled=restore.Enabled=browse.Enabled=false;UseWaitCursor=true;
        status.Text=reverting?"正在還原原始檔……":"正在安裝繁中介面與離線辭典……";status.Refresh();
        try {
            if(reverting)Program.Restore(folder.Text);else Program.Install(folder.Text);
            status.Text=reverting?"已還原英文介面並停用辭典。存檔不受影響。":"中文化完成！請從 Steam 啟動遊戲。\n辭典可拖曳移動，F8 顯示／隱藏。";
        }catch(UnauthorizedAccessException){status.Text="沒有寫入遊戲資料夾的權限，請以系統管理員身分執行此程式。";}
        catch(Exception e){status.Text=e.Message;}
        finally{UseWaitCursor=false;install.Enabled=restore.Enabled=browse.Enabled=true;}
    }
}
