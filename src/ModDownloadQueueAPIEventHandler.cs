using System;
using System.Collections.Generic;
using QueueAPI;
using QueueAPI.Default;
using QueueAPI.Harmony.Accessors;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace ModDownloadQueueBypass;

/// <summary>
/// Extends the vanilla-like behaviour by allowing players to bypass the queue if they recently joined then immediately disconnected. Likely due to mod downloads..
/// </summary>
class ModDownloadQueueAPIEventHandler : DefaultQueueAPIEventHandler, IQueueAPIEventHandler
{
    private readonly ICoreServerAPI _api;

    /// <summary>
    /// Responsible for keeping track of bypass tickets.
    /// </summary>
    private readonly BypassTicketManager _bypassTicketManager;

    /// <summary>
    /// Holds the IDs of recently joined connections. Connections are removed from this list after _quickDisconnectThreshold or when they disconnect.
    /// </summary>
    private readonly ISet<int> _recentlyJoinedClients = new HashSet<int>();

    /// <summary>
    /// How long after getting through the queue (if any) before the player is assumed to have joined successfully.
    /// </summary>
    private TimeSpan _quickDisconnectThreshold;

    /// <summary>
    /// How long before the reserved slot of a player expires (meaning they no longer can bypass the queue).
    /// </summary>
    private TimeSpan _expireTicketsAfter;

    public int WorldRemainingCapacity => WorldTotalCapacity - WorldPopulation - _bypassTicketManager.ActiveTicketCount;

    public ModDownloadQueueAPIEventHandler(ServerMain server, BypassTicketManager ticketManager, Config config) : base(server)
    {
        _api = (ICoreServerAPI) server.Api;
        _bypassTicketManager = ticketManager;

        LoadConfig(config);

        _bypassTicketManager.OnTicketIssued += ticket =>
        {
            if (Queue.Remove(ticket.PlayerId, out var queuedClient))
            {
                _bypassTicketManager.InvalidateTicket(ticket);
                server.FinalizePlayerIdentification(queuedClient!.Identification, queuedClient.Client, queuedClient.Entitlements);
            }
        };
        _bypassTicketManager.OnTicketInvalidate += _ =>
        {
            var worldCapacity = (this as IQueueAPIEventHandler).WorldRemainingCapacity;
            for (; worldCapacity > 0; worldCapacity--)
            {
                QueuedClient? queuedClient = Queue.RemoveNext();
                if (queuedClient == null)
                {
                    break;
                }
                server.FinalizePlayerIdentification(queuedClient.Identification, queuedClient.Client, queuedClient.Entitlements);
            }
        };
    }

    internal void LoadConfig(Config config)
    {
        _quickDisconnectThreshold = TimeSpan.FromSeconds(config.QuickDisconnectThresholdInSeconds);
        _expireTicketsAfter = TimeSpan.FromSeconds(config.QueueBypassTicketExpiry);
    }

    protected override AcceptanceResult RequestAcceptance(ConnectedClient client)
    {
        if (_bypassTicketManager.HasBypassTicket(client.SentPlayerUid, true))
        {
            return AcceptanceResult.Accept;
        }
        return base.RequestAcceptance(client);
    }

    public override void OnClientAccepted(ConnectedClient client)
    {
        _recentlyJoinedClients.Add(client.Id);
        _api.Event.RegisterCallback(_ => _recentlyJoinedClients.Remove(client.Id), (int) _quickDisconnectThreshold.TotalMilliseconds, true);
    }

    public override void OnClientDisconnect(ConnectedClient client)
    {
        if (_recentlyJoinedClients.Remove(client.Id))
        {
            // Player only recently joined. Issue them a bypass ticket.
            _bypassTicketManager.IssueTicket(client.SentPlayerUid, _expireTicketsAfter);
        }
        base.OnClientDisconnect(client);
    }

    public override void OnAttached(IQueueAPIEventHandler? previousHandler)
    {
        _bypassTicketManager.Reset();
        _recentlyJoinedClients.Clear();
    }

    public override void OnDetached(IQueueAPIEventHandler? newHandler)
    {
        _bypassTicketManager.Reset();
        _recentlyJoinedClients.Clear();
    }
}