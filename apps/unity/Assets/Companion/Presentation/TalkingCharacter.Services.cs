using Companion.Core;

namespace Companion.Presentation
{
    public sealed partial class TalkingCharacter
    {
        IConversationConnectionSource connectionSource;
        ConversationConnection session;
        IConversationConnectionSource ConnectionSource=>connectionSource??(connectionSource=ConversationRequests.DefaultSource());
        bool LoadSession()
        {
            // Never retain an earlier credential after a failed refresh.
            session=null;
            return ConnectionSource.TryGet(out session)&&session!=null;
        }
        bool RequireConnection()
        {
            if(LoadSession())return true;
            MarkAvailability(false);SetState(ConnectionSource.UnavailableMessage);return false;
        }
    }
}
