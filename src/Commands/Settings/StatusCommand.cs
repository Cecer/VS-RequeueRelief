using System.Linq;
using System.Text;
using QueueAPI;
using RequeueRelief.CommandAPI;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RequeueRelief.Commands.Settings;

internal class StatusCommand(Config config, BypassTicketManager ticketManager, RequeueReliefHandler handler, ICoreServerAPI api) : Command("status")
{
    protected override string? Description { get; init; } = "View the status of the queue.";

    protected override string? RequiredPrivilege { get; init; } = config.Privileges.StatusSummary;

    protected override TextCommandResult Handle(TextCommandCallingArgs args)
    {
        var showDetails = args.Caller.HasPrivilege(config.Privileges.StatusDetailed);

        var output = new StringBuilder();
        output.AppendLine("[Queue Status]");

        AppendJoinedPlayers(output, showDetails);
        AppendQueuedPlayers(output, showDetails);
        AppendBypassTickets(output, showDetails);

        return TextCommandResult.Success(output.ToString());
    }

    private void AppendJoinedPlayers(StringBuilder output, bool showDetails)
    {
        output.Append("Joined players: ").AppendLine(handler.WorldPopulation.ToString());
        if (showDetails)
        {
            foreach (var client in api.GetInternalServer().Clients.Values.Where(c => c.State != EnumClientState.Queued))
            {
                output.Append("  [").Append(client.PlayerName).Append("] ").AppendLine(client.SentPlayerUid);
            }
        }
    }

    private void AppendQueuedPlayers(StringBuilder output, bool showDetails)
    {
        output.Append("Queued players: ").AppendLine(handler.Queue.QueuePopulation.ToString());
        if (showDetails)
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
    }

    private void AppendBypassTickets(StringBuilder output, bool showDetails)
    {
        output.Append("Bypass tickets: ").AppendLine(ticketManager.ActiveTicketCount.ToString());
        if (showDetails)
        {
            foreach (var ticket in ticketManager.GetPlayersWithTicket())
            {
                output.Append("  ").AppendLine(ticket.PlayerId);
            }
        }
    }
}