using QueueAPI;
using RequeueRelief.Commands;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RequeueRelief;

public class RequeueReliefModSystem : ModSystem
{
    private Config _config = null!;
    private RequeueReliefHandler _handler = null!;
    private BypassTicketManager _ticketManager = null!;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        _config = Config.Load(api);
        _ticketManager = new BypassTicketManager(api);
        _handler = new RequeueReliefHandler(api.GetInternalServer(), _ticketManager, _config);
        api.ModLoader.GetModSystem<QueueAPIModSystem>().Handler = _handler;

        new RootCommand(_config, _ticketManager, _handler, api).Register(api.ChatCommands);
    }
}