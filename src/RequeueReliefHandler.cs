using System;
using QueueAPI;
using QueueAPI.Default;
using QueueAPI.Harmony.Accessors;
using Vintagestory.API.Server;
using Vintagestory.Server;
using static RequeueRelief.ClientDisconnectReprocessor.ClientDisconnectEventData.DisconnectCause;

namespace RequeueRelief;


/// <summary>
/// Extends the vanilla-like behaviour by allowing players to bypass the queue if they recently joined then immediately disconnected. Likely due to mod downloads..
/// </summary>
class RequeueReliefHandler : DefaultQueueAPIHandler, IQueueAPIHandler
{
    private readonly ICoreServerAPI _api;

    /// <summary>
    /// Receives client disconnects, adds additional context and emits the enhanced event data.
    /// </summary>
    private readonly ClientDisconnectReprocessor _disconnectReprocessor;

    /// <summary>
    /// Responsible for keeping track of bypass tickets.
    /// </summary>
    private readonly BypassTicketManager _bypassTicketManager;

    public int WorldRemainingCapacity => WorldTotalCapacity - WorldPopulation - _bypassTicketManager.ActiveTicketCount;

    public RequeueReliefHandler(ServerMain server, BypassTicketManager ticketManager, Config config) : base(server)
    {
        _api = (ICoreServerAPI) server.Api;
        _bypassTicketManager = ticketManager;
        _disconnectReprocessor = new ClientDisconnectReprocessor(config);

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
            var worldCapacity = (this as IQueueAPIHandler).WorldRemainingCapacity;
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

        _disconnectReprocessor.OnClientDisconnect += data =>
        {
            double ttl;
            switch (data.Cause)
            {
                case Quit:
                    ttl = config.Timings.QuitTicketTTL;
                    break;
                case Crash:
                    ttl = config.Timings.CrashTicketTTL;
                    break;
                case Timeout:
                    ttl = config.Timings.TimeoutTicketTTL;
                    break;
                case Kicked:
                default:
                    // No bypass ticket for kicks and unknown cases
                    return;
            }

            if (data.WasFailedJoin)
            {
                // If this was a failed join, pick the longer of the two TTLs.
                ttl = Math.Max(ttl, config.Timings.FailedJoinTicketTTL);
            }

            if (ttl > 0)
            {
                // Player only recently joined. Issue them a bypass ticket.
                _bypassTicketManager.IssueTicket(data.Client.SentPlayerUid, TimeSpan.FromSeconds(ttl));
            }
        };
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
        _disconnectReprocessor.HandleClientAccepted(client);
    }

    public override void OnClientDisconnect(ConnectedClient client, string? othersReason, string? theirReason)
    {
        _disconnectReprocessor.HandleClientDisconnect(client, othersReason, theirReason);
        base.OnClientDisconnect(client, othersReason, theirReason);
    }

    public override void OnAttached(IQueueAPIHandler? previousHandler)
    {
        _bypassTicketManager.Reset();
        _disconnectReprocessor.Reset();
    }

    public override void OnDetached(IQueueAPIHandler? newHandler)
    {
        _bypassTicketManager.Reset();
        _disconnectReprocessor.Reset();
    }
}