namespace ModDownloadQueueBypass; 

public class QueueBypassTicket(string playerId)
{
    public string PlayerId => playerId;
    internal long ListenerId;
    
    public bool IsValid => ListenerId != -1;
}