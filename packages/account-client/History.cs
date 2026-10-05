namespace Companion.AccountClient;

public sealed record Turn(Guid Id,string UserText,string? AssistantText,string State,long Version);
public sealed record StoredEvent(Guid event_id,long sequence,Guid turn_id,string type,long version,string? text);

// Session-only projection. One instance belongs to one authenticated conversation.
public sealed class History {
    readonly Dictionary<Guid,Turn> turns=new();
    public long Cursor {get;private set;}
    public IReadOnlyCollection<Turn> Turns=>turns.Values.ToArray();
    public bool Apply(StoredEvent item) {
        if(item.sequence<=Cursor)return false;
        if(item.sequence!=Cursor+1 || item.event_id==Guid.Empty || item.turn_id==Guid.Empty)
            throw new InvalidDataException("Invalid event order.");
        if(item.type=="turn.accepted") {
            if(item.version!=1 || item.text is null || turns.ContainsKey(item.turn_id))throw new InvalidDataException("Invalid admission.");
            if(turns.Count>=1000)throw new InvalidDataException("Local history limit reached.");
            turns.Add(item.turn_id,new(item.turn_id,item.text,null,"accepted",item.version));
        } else {
            if(!turns.TryGetValue(item.turn_id,out var turn) || turn.State!="accepted" || item.version!=turn.Version+1
                || item.type is not ("turn.completed" or "turn.cancelled" or "turn.failed")
                || (item.type=="turn.completed" ? item.text is null : item.text is not null))
                throw new InvalidDataException("Invalid terminal event.");
            turns[item.turn_id]=turn with {AssistantText=item.text,State=item.type[5..],Version=item.version};
        }
        // Checkpoint only after the projection was successfully applied.
        Cursor=item.sequence;
        return true;
    }
}
