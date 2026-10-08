using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Presentation
{
    public sealed partial class TalkingCharacter
    {
        IMicrophonePermission microphonePermission=new MicrophonePermission();
        VisualElement permissionPanel;
        Label permissionMessage;
        Button permissionContinue,permissionSettings;
        Coroutine permissionRoutine;
        public bool PermissionPanelOpen=>permissionPanel!=null&&permissionPanel.style.display.value==DisplayStyle.Flex;

        void BuildPermissionPanel()
        {
            permissionPanel=new VisualElement {name="microphone-permission"};permissionPanel.AddToClassList("settings");root.Add(permissionPanel);
            var card=new VisualElement();card.AddToClassList("settings-card");permissionPanel.Add(card);
            var body=new ScrollView();body.style.flexGrow=1;card.Add(body);
            body.Add(Text("Use your microphone",23));
            permissionMessage=Text("Record a voice message, then review the words before sending. You can always type instead. Recording stops when you leave the app.",16);body.Add(permissionMessage);
            permissionContinue=MakeButton("Continue",()=>{if(permissionRoutine==null&&!appSuspended)permissionRoutine=StartCoroutine(RequestMicrophonePermission());});permissionContinue.name="permission-continue";body.Add(permissionContinue);
            permissionSettings=MakeButton("Open device settings",()=>{if(!microphonePermission.OpenSettings())permissionMessage.text="Open your device settings and allow microphone access for this app. You can still type a message.";});permissionSettings.name="permission-settings";body.Add(permissionSettings);
            var cancel=MakeButton("Not now",ClosePermissionPanel);cancel.name="permission-cancel";body.Add(cancel);
            permissionPanel.style.display=DisplayStyle.None;
        }
        void ShowPermissionPanel()
        {
            permissionMessage.text="Record a voice message, then review the words before sending. You can always type instead. Recording stops when you leave the app.";
            permissionContinue.SetEnabled(true);permissionSettings.style.display=DisplayStyle.None;
            permissionPanel.style.display=DisplayStyle.Flex;shell.SetEnabled(false);permissionContinue.Focus();
        }
        IEnumerator RequestMicrophonePermission()
        {
            permissionContinue.SetEnabled(false);permissionMessage.text="Choose microphone access in the system prompt. No recording will start yet.";
            yield return null; // Assign coroutine ownership before a synchronous platform result.
            // Run the adapter one step at a time so a platform exception has a recoverable UI.
            var operation=microphonePermission.Request();bool failed=false;
            while(true) {
                bool more=false;object current=null;
                try{more=operation.MoveNext();if(more)current=operation.Current;}catch{failed=true;}
                if(!more||failed)break;yield return current;
            }
            permissionRoutine=null;
            if(microphonePermission.Granted&&!failed){ClosePermissionPanel();RefreshMicrophones();SetState("Microphone ready — tap the mic to start recording");}
            else {
                permissionMessage.text="Microphone access is unavailable. You can try again, enable it in device settings, or keep typing.";
                permissionContinue.SetEnabled(true);permissionSettings.style.display=microphonePermission.CanOpenSettings?DisplayStyle.Flex:DisplayStyle.None;
            }
        }
        void ClosePermissionPanel()
        {
            if(permissionRoutine!=null){StopCoroutine(permissionRoutine);permissionRoutine=null;}
            if(permissionPanel!=null)permissionPanel.style.display=DisplayStyle.None;
            if(shell!=null)shell.SetEnabled(settings==null||settings.style.display.value!=DisplayStyle.Flex);
        }
        public void HandleAudioInterruption()
        {
            bool active=IsRecording||speaking||request!=null;
            Interrupt();
            if(active)SetState("Audio interrupted — check your headset or microphone, then try again");
        }
    }
}
