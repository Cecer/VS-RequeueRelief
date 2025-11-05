using Vintagestory.API.Common;

namespace ModDownloadQueueBypass;

public class Config
{
    public const string ConfigFileName = "moddownloadqueuebypass.json";

    /// <summary>
    /// <see cref="ModDownloadQueueAPIEventHandler._quickDisconnectThreshold"/>
    /// Set to zero to disable. 
    /// </summary>
    /// <para>Defaults to 3 seconds</para>
    public double QuickDisconnectThresholdInSeconds = 3;

    /// <summary>
    /// <see cref="ModDownloadQueueAPIEventHandler._expireTicketsAfter"/>
    /// This is the maximum amount of time that a slot will be reserved for a player.
    /// This duration should ideally be lang enough to allow for clients to download all the required mods and rejoin the server.
    /// <para>Defaults to 90 seconds</para> 
    /// <remarks>This setting will potentially be replaced in the future by a system to detect a successful mod list immediately.</remarks>
    /// </summary>
    public double QueueBypassTicketExpiry = 15;

    public static Config Load(ICoreAPI api) => api.LoadModConfig<Config>(ConfigFileName) ?? new Config();
    public static void Save(ICoreAPI api, Config config) => api.StoreModConfig(config, ConfigFileName);
}