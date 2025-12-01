using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;

namespace ModDownloadQueueBypass;

public class BypassTicketManager(ICoreServerAPI api)
{
    private readonly Dictionary<string, QueueBypassTicket> _ticketsByPlayerUid = [];
    
    public event Action<QueueBypassTicket>? OnTicketIssued;
    public event Action<QueueBypassTicket>? OnTicketInvalidate;
    
    // ReSharper disable once InconsistentlySynchronizedField
    public int ActiveTicketCount => _ticketsByPlayerUid.Count;

    public bool HasBypassTicket(string playerUid, bool invalidateIfValid)
    {
        lock (_ticketsByPlayerUid)
        {
            if (_ticketsByPlayerUid.TryGetValue(playerUid, out var ticket) && ticket.IsValid && invalidateIfValid)
            {
                api.Logger.Notification($"[MDQB] Consuming bypass ticket for player {playerUid}");
                InvalidateTicket(ticket);
                return true;
            }
        }

        return false;
    }

    public void IssueTicket(string playerUid, TimeSpan expireAfter)
    {   
        var ticket = new QueueBypassTicket(playerUid);
        lock (_ticketsByPlayerUid)
        {
            InvalidateAllTicketsByPlayer(playerUid);
            _ticketsByPlayerUid[playerUid] = ticket;
        }
        api.Logger.Notification($"[MDQB] Bypass ticket issued for player {playerUid}");
        OnTicketIssued?.Invoke(ticket);
        

        api.Logger.Notification($"[MDQB] Expires in {expireAfter.TotalMilliseconds}ms");
        ticket.ListenerId = api.Event.RegisterCallback(_ =>
        {
            api.Logger.Notification($"[MDQB] Invalidating due to expiry for {playerUid}");
            InvalidateTicket(ticket);
        }, (int) expireAfter.TotalMilliseconds, true);
    }

    public void InvalidateAllTicketsByPlayer(string playerUid)
    {
        api.Logger.Notification($"[MDQB] Invalidate all bypass ticket issued for player {playerUid}");
        lock (_ticketsByPlayerUid)
        {
            if (_ticketsByPlayerUid.Remove(playerUid, out var ticket))
            {
                InvalidateTicket(ticket);
            }
        }
    }

    public void InvalidateTicket(QueueBypassTicket ticket)
    {
        api.Logger.Notification($"[MDQB] Invalidating bypass ticket issued for player {ticket.PlayerId} ({ticket.ListenerId})");
        lock (_ticketsByPlayerUid)
        {
            if (ticket.ListenerId == -1)
            {
                // Already invalidated
                return;
            }

            api.Event.UnregisterCallback(ticket.ListenerId);
            ticket.ListenerId = -1;

            if (_ticketsByPlayerUid.TryGetValue(ticket.PlayerId, out var playerTicket) && playerTicket == ticket)
            {
                _ticketsByPlayerUid.Remove(ticket.PlayerId);
            }
        }
        OnTicketInvalidate?.Invoke(ticket);
    }

    public void Reset()
    {
        lock (_ticketsByPlayerUid)
        {
            _ticketsByPlayerUid.Clear();
        }
        api.Logger.Notification("[MDQB] Invalidating all bypass tickets");
    }

    public List<QueueBypassTicket> GetPlayersWithTicket()
    {
        lock (_ticketsByPlayerUid)
        {
            return _ticketsByPlayerUid.Values.ToList();
        }
    }
}