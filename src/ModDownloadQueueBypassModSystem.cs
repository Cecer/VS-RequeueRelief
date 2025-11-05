using System;
using System.Linq;
using System.Text;
using QueueAPI;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

[assembly: ModInfo("Mod Download Queue Bypass", "moddownloadqueuebypass",
    Authors = ["Cecer"],
    Description = "Allows users to bypass the join queue if they had to download mods.",
    Version = "1.0.0",
    RequiredOnServer = true,
    RequiredOnClient = false)]

namespace ModDownloadQueueBypass; 

public class ModDownloadQueueBypassModSystem : ModSystem
{
    private Config _config = null!;
    private ModDownloadQueueAPIEventHandler _handler = null!;
    private BypassTicketManager _ticketManager = null!;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        _config = Config.Load(api);
        _ticketManager = new BypassTicketManager(api);
        _handler = new ModDownloadQueueAPIEventHandler(api.GetInternalServer(), _ticketManager, _config);
        api.ModLoader.GetModSystem<QueueAPIModSystem>().Handler = _handler;

        api.ChatCommands
            .Create("moddownloadqueuebypass")
            .WithAlias("mdqb")
            .WithDescription("Configure the Mod Download Queue Bypass settings")
            .RequiresPrivilege(Privilege.chat)
            .BeginSubCommand("status")
                .WithDescription("View the status of the mod.")
                .HandleWith(args =>
                {
                    var output = new StringBuilder();
                    output.AppendLine("[MDQB Status]");
                    output.Append("Joined players: ").AppendLine(_handler.WorldPopulation.ToString());
                    if (args.Caller.HasPrivilege(Privilege.readlists))
                    {
                        foreach (var client in api.GetInternalServer().Clients.Values.Where(c => c.State != EnumClientState.Queued))
                        {
                            output.Append("  [").Append(client.PlayerName).Append("] ").AppendLine(client.SentPlayerUid);
                        }
                    }

                    output.Append("Queued players: ").AppendLine((_handler.Queue.QueuePopulation).ToString());
                    if (args.Caller.HasPrivilege(Privilege.readlists))
                    {
                        lock (api.GetInternalServer().ConnectionQueue)
                        {
                            var position = 1;
                            foreach (var client in api.GetInternalServer().ConnectionQueue)
                            {
                                output.Append("  (").Append(position++).Append(") ").AppendLine(client.Client.SentPlayerUid);
                            }
                        }
                    }

                    output.Append("Bypass tickets: ").AppendLine(_ticketManager.ActiveTicketCount.ToString());
                    if (args.Caller.HasPrivilege(Privilege.readlists))
                    {
                        foreach (var ticket in _ticketManager.GetPlayersWithTicket())
                        {
                            output.Append("  ").AppendLine(ticket.PlayerId);
                        }
                    }

                    return TextCommandResult.Success(output.ToString());
                })
            .EndSubCommand()
            .BeginSubCommand("get")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("QuickDisconnectThreshold")
                .WithDescription("How long after getting through the queue (if any) before the player is assumed to have joined successfully. Set to zero to disable.")
                    .WithArgs(new DoubleArgParser("seconds", 0, 300, false))
                    .HandleWith(_ => TextCommandResult.Success($"[MDQB] The current value of QuickDisconnectThreshold is {_config.QuickDisconnectThresholdInSeconds} seconds."))
                .EndSubCommand()
                    .BeginSubCommand("QueueBypassTicketExpiry")
                    .WithDescription("How long before the reserved slot of a player expires (meaning they no longer can bypass the queue).")
                    .WithArgs(new DoubleArgParser("seconds", 0, 3600, false))
                    .HandleWith(_ => TextCommandResult.Success($"[MDQB] The current value of QuickDisconnectThreshold is {_config.QueueBypassTicketExpiry} seconds."))
                .EndSubCommand()
            .EndSubCommand()
            .BeginSubCommand("set")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("QuickDisconnectThreshold")
                    .WithDescription("How long after getting through the queue (if any) before the player is assumed to have joined successfully. Set to zero to disable.")
                    .WithArgs(new DoubleArgParser("seconds", 0, 300, true))
                    .HandleWith(args =>
                    {
                        var oldValue = _config.QuickDisconnectThresholdInSeconds;
                        _config.QuickDisconnectThresholdInSeconds = (double)args[0];
                        _handler.LoadConfig(_config);
                        Config.Save(api, _config);
                        return TextCommandResult.Success($"[MDQB] Changed QuickDisconnectThreshold from {oldValue} to {_config.QuickDisconnectThresholdInSeconds} seconds.");
                    })
                .EndSubCommand()
                .BeginSubCommand("QueueBypassTicketExpiry")
                    .WithDescription("How long before the reserved slot of a player expires (meaning they no longer can bypass the queue).")
                    .WithArgs(new DoubleArgParser("seconds", 0, 3600, true))
                    .HandleWith(args =>
                    {
                        var oldValue = _config.QueueBypassTicketExpiry;
                        _config.QueueBypassTicketExpiry = (double)args[0];
                        _handler.LoadConfig(_config);
                        Config.Save(api, _config);
                        return TextCommandResult.Success($"[MDQB] Changed QuickDisconnectThreshold from {oldValue} to {_config.QuickDisconnectThresholdInSeconds} seconds.");
                    })
                .EndSubCommand()
            .EndSubCommand()
            .BeginSubCommand("reset")
                .RequiresPrivilege(Privilege.kick)
                .BeginSubCommand("queue")
                    .WithDescription("Kicks all queued players.")
                    .HandleWith(_ =>
                    {
                        var queueSize = _handler.Queue.QueuePopulation;
                        _handler.Queue.RemoveAll("Queue reset by");
                        return TextCommandResult.Success($"[MDQB] Kicked {queueSize} queued players.");
                    })
                .EndSubCommand()
                .BeginSubCommand("tickets")
                    .WithDescription("Invalidate all active bypass tickets.")
                    .HandleWith(_ =>
                    {
                        var ticketCount = _ticketManager.ActiveTicketCount;
                        _ticketManager.Reset();
                        return TextCommandResult.Success($"[MDQB] Invalidated {ticketCount} bypass tickets.");
                    })
                .EndSubCommand()
            .EndSubCommand()
            .BeginSubCommand("fastpass")
                .RequiresPrivilege(Privilege.controlserver)
                .WithDescription("Brings a queued player into the server immediately. Ignores all client and queue limits.")
                .BeginSubCommand("byClientId")
                    .WithArgs(new IntArgParser("clientID", 0, int.MaxValue, -1, true))
                    .HandleWith(args =>
                    {
                        var clientId = (int) args[0];
                        var client  = api.GetInternalServer().Clients[clientId];
                        if (client == null)
                        {
                            return TextCommandResult.Error($"[MDQB] Client {clientId} is not in the queue.");
                        }

                        if (client.State != EnumClientState.Queued)
                        {
                            return TextCommandResult.Error($"[MDQB] Client {clientId} is not in the queue.");
                        }
                        
                        _ticketManager.IssueTicket(client.SentPlayerUid, TimeSpan.MaxValue);
                        return TextCommandResult.Success($"[MDQB] Issued single use bypass ticket for client {client.PlayerName} ({clientId}) with no expiry.");   
                    })
                .EndSubCommand()
                .BeginSubCommand("byPlayerUid")
                    .WithArgs(new StringArgParser("playerUid", true))
                    .HandleWith(args =>
                    {
                        var playerUid = (string) args[0];
                        var playerData = api.PlayerData.GetPlayerDataByUid(playerUid);
                        if (playerData == null)
                        {
                            return TextCommandResult.Error($"[MDQB] Player {playerUid} has not joined the server before.");
                        }

                        var client = api.GetInternalServer().GetClientByUID(playerUid);
                        if (client != null && client.State != EnumClientState.Queued)
                        {
                            return TextCommandResult.Error($"[MDQB] Player {client.PlayerName} ({client.Id}) is online but not in the queue. No need for a bypass ticket.");   
                        }
                        
                        _ticketManager.IssueTicket(playerData.PlayerUID, TimeSpan.FromHours(1));
                        return TextCommandResult.Success($"[MDQB] Issued single use bypass ticket for {playerData.LastKnownPlayername} ({playerData.PlayerUID}) with 1 hour expiry.");   
                    })
                .EndSubCommand()
            .EndSubCommand();
    }
}