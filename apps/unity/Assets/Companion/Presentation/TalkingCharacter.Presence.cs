using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Companion.Presentation
{
    public sealed partial class TalkingCharacter
    {
        Label presenceLabel,typingLabel;
        bool serviceOnline;
        DateTime lastAvailable;
        float lastAvailabilityTime=-100,nextAvailabilityProbe;
        public string PresenceText=>presenceLabel?.text??"";
        void MarkAvailability(bool available)
        {
            serviceOnline=available;
            if(available){lastAvailable=DateTime.Now;lastAvailabilityTime=Time.realtimeSinceStartup;}
            nextAvailabilityProbe=Time.realtimeSinceStartup+30;
        }
        void RefreshPresence()
        {
            if(presenceLabel==null)return;
            bool typing=responding&&!textFinal&&!appSuspended;
            string dots=reduceMotion?"…":new string('.',1+(int)(Time.unscaledTime*2)%3);
            string activity=appSuspended?"Paused":speaking?"Speaking…":typing?"Typing"+dots:
                serviceOnline&&Time.realtimeSinceStartup-lastAvailabilityTime<45?"Online":
                lastAvailable!=default?"Last seen "+lastAvailable.ToString(lastAvailable.Date==DateTime.Now.Date?"HH:mm":"dd MMM HH:mm"):"Offline";
            string value=ConnectionSource.DisplayName+" · "+activity;
            if(presenceLabel.text!=value)presenceLabel.text=value;
            if(typingLabel!=null){typingLabel.style.display=typing?DisplayStyle.Flex:DisplayStyle.None;typingLabel.text=SelectedCharacterName+" is typing"+dots;}
            // Bounded, foreground-only health checks. Never infer online from device connectivity.
            if(isActiveAndEnabled&&!appSuspended&&request==null&&setupRequest==null&&Time.realtimeSinceStartup>=nextAvailabilityProbe){nextAvailabilityProbe=Time.realtimeSinceStartup+30;CheckSetup();}
        }
        void UpdateStreamingBubble()
        {
            if(replyLabel==null)return;
            bool follow=followConversation;
            replyLabel.text=LastResponse;
            if(follow)transcript.schedule.Execute(()=>{if(followConversation&&replyLabel?.parent!=null)transcript.ScrollTo(replyLabel.parent);});
            else newMessages.style.display=DisplayStyle.Flex;
        }
    }
}
