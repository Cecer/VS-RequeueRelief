using RequeueRelief.CommandAPI;
using RequeueRelief.Commands.Settings;
using Vintagestory.API.Server;

namespace RequeueRelief.Commands;


internal class RootCommand(Config config, BypassTicketManager ticketManager, RequeueReliefHandler queueHandler, ICoreServerAPI api) : Command("requeuerelief")
{
    protected override string[] Aliases { get; init; } = ["queue"];

    protected override string? Description { get; init; } = "View and configure Requeue Relief";

    protected override string? RequiredPrivilege { get; init; } = Privilege.chat;

    protected override Command[] SubCommands { get; init; } =
    [
        new StatusCommand(config, ticketManager, queueHandler, api),
        new GetCommand(config),
        new SetCommand(config, queueHandler, api),
        new ReloadCommand(config, api),

        new ResetCommand(config, ticketManager, queueHandler),
        new FastPassCommand(config, ticketManager, api)
    ];
}