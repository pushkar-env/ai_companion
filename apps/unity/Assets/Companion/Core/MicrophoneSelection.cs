using System;
using System.Collections.Generic;

namespace Companion.Core
{
    // Device order can change after hotplug. Never replace an established selection.
    public sealed class MicrophoneSelection
    {
        public const string Missing="Select microphone — previous input unavailable";
        public const string None="No microphone available — connect and Refresh";
        readonly List<string> available=new List<string>();
        bool initialized;
        public string Selected {get;private set;}
        public bool IsAvailable=>!string.IsNullOrEmpty(Selected)&&available.Contains(Selected);
        public string Display=>IsAvailable?Selected:available.Count==0?None:Missing;
        public List<string> Choices {get;private set;}=new List<string>();
        public MicrophoneSelection(string remembered){Selected=remembered;}
        public void Refresh(IEnumerable<string> devices)
        {
            available.Clear();
            foreach(var device in devices)if(!string.IsNullOrEmpty(device)&&!available.Contains(device))available.Add(device);
            if(!initialized&&string.IsNullOrEmpty(Selected)&&available.Count>0)Selected=available[0];
            initialized=true;Choices=new List<string>(available);
            if(!IsAvailable)Choices.Insert(0,Display);
        }
        public bool Select(string device)
        {
            if(!available.Contains(device))return false;
            Selected=device;return true;
        }
    }
}
