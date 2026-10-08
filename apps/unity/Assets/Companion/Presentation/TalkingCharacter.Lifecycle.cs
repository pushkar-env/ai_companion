using UnityEngine.UIElements;

namespace Companion.Presentation
{
    public sealed partial class TalkingCharacter
    {
        bool appSuspended, suspendedCameraEnabled, restoreSuspendedCamera;
        public bool IsSuspended=>appSuspended;

        // Used by the OS callback and deterministic Editor lifecycle checks.
        public void SetApplicationSuspended(bool suspended)
        {
            if(appSuspended==suspended)return;
            appSuspended=suspended;
            if(suspended) {
                Interrupt();historyNavigation?.Pause();
                if(setupRequest!=null){setupRequest.Abort();setupRequest.Dispose();setupRequest=null;}
                if(setupRoutine!=null){StopCoroutine(setupRoutine);setupRoutine=null;SetSetup("Check setup when ready");}
                setup?.SetEnabled(true);
                // Keep the last frame while saving the portrait camera's previous state.
                if(portraitCamera!=null){suspendedCameraEnabled=portraitCamera.enabled;restoreSuspendedCamera=true;portraitCamera.enabled=false;}
                SetState("Paused — draft kept; voice stopped");
            } else {
                RestoreLifecycleCamera();
                // Never restart recording, requests, history streams or speech on resume.
                SetState("Welcome back — continue your draft or send a new message");
            }
        }

        void RestoreLifecycleCamera()
        {
            if(restoreSuspendedCamera&&portraitCamera!=null)portraitCamera.enabled=suspendedCameraEnabled;
            restoreSuspendedCamera=false;
        }

        void OnNavigationCancel(NavigationCancelEvent e)
        {
            if(HandleBack()){e.StopPropagation();e.PreventDefault();}
        }

        // Cancel routes close the topmost surface without losing a chat draft.
        public bool HandleBack()
        {
            if(appSuspended)return false;
            if(PermissionPanelOpen){ClosePermissionPanel();return true;}
            if(historyNavigation?.IsOpen==true){historyNavigation.Close();return true;}
            if(settings?.style.display.value==DisplayStyle.Flex){CloseSettings();return true;}
            if(WardrobeOpen){CloseWardrobe(false);return true;}
            if(IsRecording){Interrupt();return true;}
            // At the root, leave system navigation to the host; never clear the chat.
            return false;
        }
    }
}
