using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RequeueRelief;

// ReSharper disable FieldCanBeMadeReadOnly.Global
public class Config
{
    private const string ConfigFileName = "requeuerelief.json";

    public readonly PrivilegesConfig Privileges = new();

    public readonly TimingsConfig Timings = new();

    public static Config Load(ICoreAPI api) => api.LoadModConfig<Config>(ConfigFileName) ?? new Config();
    public static void Save(ICoreAPI api, Config config) => api.StoreModConfig(config, ConfigFileName);

    public class PrivilegesConfig
    {
        [ConfigDescription("""
                           The privilege required to view the basic status of the queue.

                           Default: chat
                           """)]
        public readonly string StatusSummary = Privilege.chat;

        [ConfigDescription("""
                           The privilege required to view the detailed status of the queue (including the names of players in the queue).

                           Default: readlists
                           """)]
        public readonly string StatusDetailed = Privilege.readlists;
        [ConfigDescription("""
                           The privilege required to manage the configuration of the queue.

                           Default: controlserver
                           """)]
        public readonly string Configure = Privilege.controlserver;

        [ConfigDescription("""
                           The privilege required to reset the queue. Resetting the queue will kick all players in the queue from the server.

                           Default: kick
                           """)]
        public readonly string Reset = Privilege.kick;


        [ConfigDescription("""
                           The privilege required to issue a single use queue bypass to a player manually.

                           Default: controlserver. 
                           """)]
        public readonly string FastPass = Privilege.controlserver;

        public PrivilegesConfig() { }
    }

    public class TimingsConfig
    {
        // An arbitrary value. I want to avoid extreme values.
        private const double TenYears = 315360000.0;

        [ConfigDescription("""
                           How long after getting through the queue (if any) before the player is assumed to have joined successfully. If a player disconnects before this threshold, they are treated as a failed join. Set to zero to disable failed join detection.
                           This duration should ideally be lang enough to allow for clients to download all the required mods and rejoin the server.

                           Default: 3
                           """)]
        [ConfigValidRange(0, TenYears)]
        [ConfigUnits("second", "seconds")]
        public double FailedJoinThresholdSeconds = 3.0;

        [ConfigDescription("""
                           How long a slot is reserved for a player after a failed join.
                           Set to zero to disable reserved slots for failed joins.

                           Default: 90
                           """)]
        [ConfigValidRange(0, TenYears)]
        [ConfigUnits("second", "seconds")]
        public double FailedJoinTicketTTL = 90;

        [ConfigDescription("""
                           How long a slot is reserved for a player after they crash (excluding failed joins).
                           Set to zero to disable reserved slots for crashed players.

                           Default: 30
                           """)]
        [ConfigValidRange(0, TenYears)]
        [ConfigUnits("second", "seconds")]
        public double CrashTicketTTL = 30;

        [ConfigDescription("""
                           How long a slot is reserved for a player after they timed out.
                           Set to zero to disable reserved slots for player timeouts.

                           Default: 45
                           """)]
        [ConfigValidRange(0, TenYears)]
        [ConfigUnits("second", "seconds")]
        public double TimeoutTicketTTL = 45;

        [ConfigDescription("""
                           How long a slot is reserved for a player after they quit normally.
                           Set to zero to disable reserved slots for quitting players.

                           Default: 10
                           """)]
        [ConfigValidRange(0, TenYears)]
        [ConfigUnits("second", "seconds")]
        public double QuitTicketTTL = 10;

        public TimingsConfig() { }
    }
}