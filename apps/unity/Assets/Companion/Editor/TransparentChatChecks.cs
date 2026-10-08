using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using Companion.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Editor
{
    public static class TransparentChatChecks
    {
        static TalkingCharacter app;static UIDocument doc;static PanelSettings original,panel;static RenderTexture target;
        static StyleLength width,height;static int step;static double next;static Vector3 cameraPosition;static Rect stageBounds;
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly List<string> lines=new List<string>();
        static string Folder=>Path.GetFullPath(Application.dataPath+"/../../../docs/evidence/transparent-chat");
        static void Check(bool ok,string label){lines.Add((ok?"PASS ":"FAIL ")+label);if(!ok)throw new Exception(label);}
        static void Add(string who,string text)=>typeof(TalkingCharacter).GetMethod("AddMessage",Flags).Invoke(app,new object[]{who,text});
        static void Set(string field,object value)=>typeof(TalkingCharacter).GetField(field,Flags).SetValue(app,value);
        public static void Run()
        {
            if(!EditorApplication.isPlaying||app!=null)throw new Exception("Run once in Play mode");
            app=UnityEngine.Object.FindAnyObjectByType<TalkingCharacter>();doc=app.GetComponent<UIDocument>();original=doc.panelSettings;
            panel=UnityEngine.Object.Instantiate(original);panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;
            target=new RenderTexture(390,844,24);target.Create();panel.targetTexture=target;doc.panelSettings=panel;
            width=doc.rootVisualElement.style.width;height=doc.rootVisualElement.style.height;
            Configure(390,844);app.PreviewSafeInsets=new Vector2(24,24);app.NewChat();doc.rootVisualElement.RemoveFromClassList("opaque");
            lines.Clear();Directory.CreateDirectory(Folder);step=0;EditorApplication.update+=Tick;
        }
        static void Configure(int w,int h){target.Release();target.width=w;target.height=h;target.Create();doc.rootVisualElement.style.width=w;doc.rootVisualElement.style.height=h;next=EditorApplication.timeSinceStartup+1;}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Folder,name+".png"),image.EncodeToPNG());RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);}
        static void Tick()
        {
            EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;
            try {
                var root=doc.rootVisualElement;var chat=root.Q("chat-drawer");var stage=root.Q("companion-stage");var scroll=root.Q<ScrollView>("conversation");
                switch(step++) {
                    case 0:
                        cameraPosition=app.portraitCamera.transform.position;stageBounds=stage.worldBound;
                        Add("You","I finally took a little time for myself today.");Add("Companion","That sounds lovely. What did you do with that time?");break;
                    case 1:
                        Check(chat.worldBound.yMin>=root.Q("top-navigation").worldBound.yMax,"Chat starts below top navigation");
                        Check(chat.worldBound.height>root.worldBound.height*.8f,"Chat occupies the full area below navigation");
                        Check(stage.worldBound.Overlaps(chat.worldBound),"Character scene sits behind the conversation");
                        Check(chat.resolvedStyle.backgroundColor.a==0&&chat.resolvedStyle.borderTopWidth==0,"Chat has no background panel or border");
                        foreach(var bubble in scroll.contentContainer.Children())Check(bubble.resolvedStyle.backgroundColor.a==0,"Message background fully transparent");
                        Check(Vector3.Distance(cameraPosition,app.portraitCamera.transform.position)<.0001f,"Adding messages does not zoom the character");Capture("conversation");
                        for(int i=0;i<14;i++)Add(i%2==0?"You":"Companion","Message "+(i+1)+" — Taking a quiet moment to talk about the day. There is room to read, pause, and come back to an earlier thought.");break;
                    case 2:
                        Check(scroll.verticalScroller.highValue>100,"Long conversation scrolls beyond one screen");
                        Check(Vector3.Distance(cameraPosition,app.portraitCamera.transform.position)<.0001f,"Long history leaves camera framing unchanged");
                        scroll.scrollOffset=Vector2.zero;Set("followConversation",false);Add("Companion","A new message while you read earlier in the conversation.");break;
                    case 3:
                        Check(scroll.scrollOffset.y<5,"New message does not jump away from older history");
                        Check(root.Q("new-messages").resolvedStyle.display==DisplayStyle.Flex,"New-message control offers return to latest");Capture("scrollback");
                        var latest=root.Q<Button>("new-messages");latest.Focus();using(var e=NavigationSubmitEvent.GetPooled()){latest.SendEvent(e);}break;
                    case 4:
                        Check(scroll.verticalScroller.highValue-scroll.scrollOffset.y<8,"Return to latest reaches final message");
                        app.PreviewKeyboardInset=280;break;
                    case 5:
                        Check(Vector3.Distance(cameraPosition,app.portraitCamera.transform.position)<.0001f&&stage.worldBound==stageBounds,"Keyboard moves chat without resizing scene or zooming model");
                        Check(root.Q("composer").worldBound.yMax<=844-280,"Composer remains above keyboard");
                        Check(scroll.verticalScroller.highValue-scroll.scrollOffset.y<8,"Keyboard opening keeps latest visible (gap="+(scroll.verticalScroller.highValue-scroll.scrollOffset.y)+")");Capture("keyboard");
                        app.PreviewKeyboardInset=0;app.OpenWardrobe();break;
                    case 6:
                        Check(Vector3.Distance(cameraPosition,app.portraitCamera.transform.position)<.0001f,"Wardrobe overlay does not zoom model");
                        Check(root.Q("save-look").worldBound.yMax<844,"Wardrobe Save stays reachable");app.CloseWardrobe(false);Configure(360,640);Set("largeText",true);root.AddToClassList("large-text");break;
                    case 7:
                        foreach(string id in new[]{"message-input","record-voice","send-message"}){var b=root.Q(id).worldBound;Check(b.height>=48&&b.xMin>=0&&b.xMax<=360&&b.yMax<=616,id+" fits compact portrait and safe area");}
                        Check(root.Q("conversation").worldBound.height>300,"Large text retains a generous scroll viewport");Capture("compact-large-text");Finish(null);return;
                }
                next=EditorApplication.timeSinceStartup+1;
            }catch(Exception e){Finish(e.ToString());}
        }
        static void Finish(string error)
        {
            EditorApplication.update-=Tick;if(error!=null)lines.Add("FAIL "+error);app.PreviewKeyboardInset=0;app.PreviewSafeInsets=Vector2.zero;if(app.WardrobeOpen)app.CloseWardrobe(false);app.NewChat();
            bool large=PlayerPrefs.GetInt("Companion.UI.LargeText",0)==1;Set("largeText",large);doc.rootVisualElement.EnableInClassList("large-text",large);doc.rootVisualElement.EnableInClassList("opaque",PlayerPrefs.GetInt("Companion.UI.Opaque",0)==1);
            doc.rootVisualElement.style.width=width;doc.rootVisualElement.style.height=height;doc.panelSettings=original;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(panel);
            File.WriteAllLines(Path.Combine(Folder,"checks.txt"),lines);app=null;
        }
    }
}
