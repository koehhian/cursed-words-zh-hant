using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public sealed partial class CursedWordsZh : MonoBehaviour {
    static CursedWordsZh instance;
    static Dictionary<string,string> texts = new Dictionary<string,string>();
    static Dictionary<string,string> lexicon = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    static List<KeyValuePair<Regex,string>> templates = new List<KeyValuePair<Regex,string>>();
    static List<KeyValuePair<string,string>> fragments;
    static Dictionary<string,string> cache = new Dictionary<string,string>();
    static string root;
    Font font; TMP_FontAsset fallback;
    float nextScan; bool visible = true; bool minimized;
    Rect window = new Rect(16,70,350,240);
    string meaning = "送出單字後，這裡會顯示基本意思。\n釋義會保留到下一次送出單字。";
    Vector2 scroll;
    GUIStyle label, title, windowStyle, header;
    RectTransform inputBlocker;
    float UiScale { get { return Mathf.Clamp(Screen.height/900f,0.8f,2f); } }
    static string Decode(string s) { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
    public static void Boot() {
        if(instance != null) return;
        try {
            root = Path.Combine(Application.dataPath,"ZhTW");
            var go = new GameObject("CursedWords Traditional Chinese Dictionary");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CursedWordsZh>();
        } catch(Exception e) { Debug.LogError("[CWZh] Boot: "+e); }
    }
    void Awake() {
        window.x=Mathf.Max(16,Screen.width/UiScale-370);
        try {
            foreach(var line in File.ReadAllLines(Path.Combine(root,"translations.tsv"),Encoding.UTF8)) {
                var p=line.Split('\t'); if(p.Length!=2)continue;
                texts[Decode(p[0])]=Decode(p[1]);
            }
            foreach(var kv in texts) {
                var slots=Regex.Matches(kv.Key,@"\{\d+[^}]*\}|\[[A-Z][A-Z 0-9]*\]");
                if(slots.Count==0 || Regex.Replace(kv.Key,@"\{\d+[^}]*\}|\[[A-Z][A-Z 0-9]*\]","").Length<5)continue;
                var pattern="^"; int pos=0;var replacements=new Dictionary<string,string>();
                foreach(Match m in slots) {
                    string group;
                    if(!replacements.TryGetValue(m.Value,out group)) { group="v"+replacements.Count; replacements[m.Value]=group; }
                    pattern+=Regex.Escape(kv.Key.Substring(pos,m.Index-pos))+"(?<"+group+">.*?)";
                    pos=m.Index+m.Length;
                }
                pattern+=Regex.Escape(kv.Key.Substring(pos))+"$";
                string translatedTemplate=kv.Value;
                foreach(var slot in replacements)translatedTemplate=translatedTemplate.Replace(slot.Key,"\uE000"+slot.Value+(slot.Key.Contains("WORD")?"!":"")+"\uE001");
                templates.Add(new KeyValuePair<Regex,string>(new Regex(pattern,RegexOptions.Singleline),translatedTemplate));
            }
            fragments=texts.Where(k=>k.Key.Length>=3 && (k.Key.Contains(" ") || k.Key.Contains("\n")) && !k.Key.Contains("{") && !k.Key.Contains("[") && !k.Key.Contains("<") && k.Key!=k.Value).OrderByDescending(k=>k.Key.Length).ToList();
            foreach(var line in File.ReadLines(Path.Combine(root,"dictionary.tsv"),Encoding.UTF8)) {
                int n=line.IndexOf('\t'); if(n>0)lexicon[line.Substring(0,n)]=line.Substring(n+1).Replace("\\n","\n");
            }
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft JhengHei","Microsoft YaHei","Arial Unicode MS"},24);
            fallback=TMP_FontAsset.CreateFontAsset(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Fonts/msjh.ttc"),0,48,5,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048);
            fallback.isMultiAtlasTexturesEnabled=true;
            DontDestroyOnLoad(font); DontDestroyOnLoad(fallback);
            if(TMP_Settings.fallbackFontAssets!=null)TMP_Settings.fallbackFontAssets.Add(fallback);
            Debug.Log("[CWZh] Ready: "+texts.Count+" translations, "+lexicon.Count+" dictionary entries");
            var canvasGO=new GameObject("Dictionary input canvas",typeof(Canvas),typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform,false);
            var canvas=canvasGO.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32760;
            var panel=new GameObject("Dictionary input shield",typeof(RectTransform),typeof(Image));
            panel.transform.SetParent(canvasGO.transform,false);
            panel.GetComponent<Image>().color=Color.clear;
            inputBlocker=panel.GetComponent<RectTransform>();
            inputBlocker.anchorMin=inputBlocker.anchorMax=new Vector2(0,1);inputBlocker.pivot=new Vector2(0,1);
        } catch(Exception e) { Debug.LogError("[CWZh] Awake: "+e); }
    }
    public static string Translate(string s) {
        if(String.IsNullOrEmpty(s)||s.Length<2)return s;
        string value; if(texts.TryGetValue(s,out value))return value;
        if(s.IndexOf("\r")>=0 && texts.TryGetValue(s.Replace("\r\n","\n").Replace("\r","\n"),out value))return value;
        if(s.Length<3)return s;
        var trim=s.Trim(); if(texts.TryGetValue(trim,out value))return s.Replace(trim,value);
        if(cache.TryGetValue(s,out value))return value;
        foreach(var pair in templates) {
            var m=pair.Key.Match(s); if(!m.Success)continue;
            value=Regex.Replace(pair.Value,"\uE000(v[0-9]+)(!?)\uE001",x=>x.Groups[2].Length>0?m.Groups[x.Groups[1].Value].Value:LocalizeSlot(m.Groups[x.Groups[1].Value].Value));
            cache[s]=value;return value;
        }
        if(Regex.IsMatch(s,@"[\u3400-\u9fff]")) {
            value=Regex.Replace(s,@"\b(RED|BLUE|VOID|SHINY|COLOURLESS|PURPLE|GOLD|WHITE|GREEN|PINK|GLITCH|CACTUS)(?=流派|字塊)",x=>LocalizeSlot(x.Value),RegexOptions.IgnoreCase);
            cache[s]=value;return value;
        }
        value=s;
        if(fragments!=null) foreach(var kv in fragments) if(value.Contains(kv.Key)) value=value.Replace(kv.Key,kv.Value);
        value=Regex.Replace(value,@"\b(RED|BLUE|VOID|SHINY|COLOURLESS|PURPLE|GOLD|WHITE|GREEN|PINK|GLITCH|CACTUS|tiles|tile|Stickers|Sticker|Stamps|Stamp|cards|card)\b",x=>LocalizeSlot(x.Value));
        if(cache.Count>12000)cache.Clear();cache[s]=value;return value;
    }
    static string LocalizeSlot(string source) {
        string result;
        if(texts.TryGetValue(source,out result))return result;
        if(source.Length>0 && texts.TryGetValue(source.Trim(),out result))return result;
        return source;
    }
    static bool IsSpelling(TMP_Text t) {
        if(t.name.Equals("ONE PER ENCOUNTER",StringComparison.OrdinalIgnoreCase))return false;
        for(Transform p=t.transform;p!=null;p=p.parent) {
            string n=p.name.ToLowerInvariant();
            if(n.Contains("wordletter")||n.Contains("gridtile")||n.Contains("currentword")||n.Contains("puzzleentry")||n.Contains("tileobject"))return true;
            if(p.GetComponent<TileObject>()!=null || p.GetComponent<WordLetterController>()!=null)return true;
            var history=p.GetComponent<WordHistoryObject>();
            if(history!=null) {
                var field=typeof(WordHistoryObject).GetField("_realWordTMP",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                if(field!=null && (field.GetValue(history) as TMP_Text)==t)return true;
            }
        }
        return false;
    }
    static bool IsAnimatedDialogue(TMP_Text t) {
        for(Transform p=t.transform;p!=null;p=p.parent) {
            var controller=p.GetComponent<DialogueController>();
            if(controller!=null) {
                var field=typeof(DialogueController).GetField("_myTMP",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                if(field!=null && (field.GetValue(controller) as TMP_Text)==t)return true;
            }
        }
        string n=t.name.ToLowerInvariant();
        return n.Contains("speechbubble")||n.Contains("dialoguetext")||n.Contains("bubbletext");
    }
    public static void SetText(TMP_Text target,string source) {
        if(target==null)return;
        if(IsSpelling(target)||IsAnimatedDialogue(target)) { target.text=source;return; }
        target.text=Translate(source);
    }
    void Update() {
        if(Input.GetKeyDown(KeyCode.F8))visible=!visible;
        if(inputBlocker!=null) {
            inputBlocker.gameObject.SetActive(visible);
            inputBlocker.anchoredPosition=new Vector2(window.x,-window.y)*UiScale;
            inputBlocker.sizeDelta=new Vector2(window.width,window.height)*UiScale;
        }
        if(Time.unscaledTime<nextScan)return;
        nextScan=Time.unscaledTime+0.15f;
        try {
            foreach(var t in Resources.FindObjectsOfTypeAll<TMP_Text>()) {
                if(!t.gameObject.scene.IsValid()||!t.gameObject.activeInHierarchy||IsSpelling(t))continue;
                if(t.font!=null && fallback!=null && t.font!=fallback) {
                    if(t.font.fallbackFontAssetTable==null)t.font.fallbackFontAssetTable=new List<TMP_FontAsset>();
                    if(!t.font.fallbackFontAssetTable.Contains(fallback))t.font.fallbackFontAssetTable.Add(fallback);
                }
                if(IsAnimatedDialogue(t))continue;
                var source=t.text; var translated=Translate(source);
                if(translated!=source) {
                    if(!t.enableAutoSizing && t.rectTransform.rect.width>0 && t.rectTransform.rect.height>0) {
                        t.fontSizeMax=t.fontSize;t.fontSizeMin=Mathf.Max(10,t.fontSize*0.6f);t.enableAutoSizing=true;
                    }
                    t.text=translated; t.ForceMeshUpdate();
                }
            }
        } catch(Exception e) { Debug.LogError("[CWZh] Scan: "+e.Message); }
    }
    public static void ShowWords(List<string> words) {
        Boot(); if(instance==null||words==null)return;
        try {
            var sb=new StringBuilder();
            foreach(var raw in words.Distinct()) {
                var word=Regex.Replace(raw,@"<[^>]*>","").Trim(); string def;
                sb.AppendLine(word.ToUpperInvariant());
                if(!lexicon.TryGetValue(word,out def))def="離線辭典未收錄此字。";
                sb.AppendLine(def);sb.AppendLine();
            }
            instance.meaning=sb.ToString();instance.scroll=Vector2.zero;instance.visible=true;
            Debug.Log("[CWZh] Dictionary: "+String.Join(", ",words.ToArray()));
        }catch(Exception e){Debug.LogError("[CWZh] Lookup: "+e);}
    }
    public static void ShowHistoric(HistoricWord word) {
        if(word!=null && !word.IsWordSkipped)ShowWords(word.Words);
    }
    void OnGUI() {
        if(!visible||font==null)return;
        if(label==null) {
            label=new GUIStyle(GUI.skin.label){font=font,fontSize=18,wordWrap=true,richText=false};
            label.normal.textColor=Color.white;
            title=new GUIStyle(GUI.skin.button){font=font,fontSize=15};
            header=new GUIStyle(label){fontSize=14,wordWrap=false};
            var background=new Texture2D(1,1);background.SetPixel(0,0,new Color(.065f,.085f,.12f,1));background.Apply();
            windowStyle=new GUIStyle(GUI.skin.window);windowStyle.normal.background=background;windowStyle.onNormal.background=background;
            foreach(var state in new[]{windowStyle.hover,windowStyle.active,windowStyle.focused,windowStyle.onHover,windowStyle.onActive,windowStyle.onFocused})state.background=background;
            windowStyle.border=new RectOffset(0,0,0,0);
            windowStyle.padding=new RectOffset(12,12,36,12);
        }
        float scale=UiScale;
        window.width=Mathf.Min(350,Screen.width/scale-20); window.height=minimized?44:260;
        window.x=Mathf.Clamp(window.x,0,Screen.width/scale-window.width);window.y=Mathf.Clamp(window.y,0,Screen.height/scale-window.height);
        var matrix=GUI.matrix;GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(scale,scale,1));
        var old=GUI.skin.font;GUI.skin.font=font;
        window=GUI.Window(974613,window,DrawWindow,"",windowStyle);
        GUI.skin.font=old;GUI.matrix=matrix;
    }
    void DrawWindow(int id) {
        var previousColor=GUI.color;GUI.color=new Color(.065f,.085f,.12f,1);
        GUI.DrawTexture(new Rect(0,0,window.width,window.height),Texture2D.whiteTexture,ScaleMode.StretchToFill,false);GUI.color=previousColor;
        GUI.Label(new Rect(12,6,window.width-65,25),"單字辭典 · 拖曳移動 · F8 開關",header);
        if(GUI.Button(new Rect(window.width-45,4,36,26),minimized?"＋":"－",title))minimized=!minimized;
        if(!minimized) {
            GUILayout.Space(8);scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label(meaning,label);GUILayout.EndScrollView();
        }
        GUI.DragWindow(new Rect(0,0,window.width-50,25));
    }
}
