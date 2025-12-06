using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;

namespace RequeueRelief;

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
            if (_ticketsByPlayerUid.TryGetValue(playerUid, out var ticket) && ticket.IsValid)
            {
                if (invalidateIfValid)
                {
                    api.Logger.Notification($"[RequeueRelief] Consuming bypass ticket for player {playerUid}");
                    InvalidateTicket(ticket);
                }
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
        api.Logger.Notification($"[RequeueRelief] Bypass ticket issued for player {playerUid}");
        OnTicketIssued?.Invoke(ticket);
        

        api.Logger.Notification($"[RequeueRelief] Expires in {expireAfter.TotalMilliseconds}ms");
        ticket.ListenerId = api.Event.RegisterCallback(_ =>
        {
            api.Logger.Notification($"[RequeueRelief] Invalidating due to expiry for {playerUid}");
            InvalidateTicket(ticket);
        }, (int) expireAfter.TotalMilliseconds, true);
    }

    public void InvalidateAllTicketsByPlayer(string playerUid)
    {
        api.Logger.Notification($"[RequeueRelief] Invalidate all bypass tickets issued for player {playerUid}");
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
        api.Logger.Notification($"[RequeueRelief] Invalidating bypass ticket issued for player {ticket.PlayerId} ({ticket.ListenerId})");
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
            foreach (var ticket in _ticketsByPlayerUid.Values)
            {
                api.Event.UnregisterCallback(ticket.ListenerId);
                ticket.ListenerId = -1;
            }
            _ticketsByPlayerUid.Clear();
        }
        api.Logger.Notification("[RequeueRelief] Invalidating all bypass tickets");
    }

    public List<QueueBypassTicket> GetPlayersWithTicket()
    {
        lock (_ticketsByPlayerUid)
        {
            return _ticketsByPlayerUid.Values.ToList();
        }
    }
}