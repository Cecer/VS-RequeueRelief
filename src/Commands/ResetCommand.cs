using RequeueRelief.CommandAPI;
using Vintagestory.API.Common;

namespace RequeueRelief.Commands;

internal class ResetCommand(Config config, BypassTicketManager ticketManager, RequeueReliefHandler queueHandler) : Command("reset")
{
    protected override string? RequiredPrivilege { get; init; } = config.Privileges.Reset;

    protected override Command[] SubCommands { get; init; } =
    [
        new Queue(queueHandler),
        new Tickets(ticketManager)
    ];

    private class Queue(RequeueReliefHandler queueHandler) : Command("queue")
    {
        protected override string? Description { get; init; } = "Kicks all players currently in the queue.";

        protected override TextCommandResult Handle(TextCommandCallingArgs args)
        {
            var queueSize = queueHandler.Queue.QueuePopulation;
            queueHandler.Queue.RemoveAll("Queue reset via command");
            return TextCommandResult.Success($"[RequeueRelief] Kicked {queueSize} queued players.");
        }
    }

    private class Tickets(BypassTicketManager ticketManager) : Command("tickets")
    {
        protected override string? Description { get; init; } = "Invalidate all active bypass tickets.";

        protected override TextCommandResult Handle(TextCommandCallingArgs args)
        {
            var ticketCount = ticketManager.ActiveTicketCount;
            ticketManager.Reset();
            return TextCommandResult.Success($"[RequeueRelief] Invalidated {ticketCount} bypass tickets.");
        }
    }
}