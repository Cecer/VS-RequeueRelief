using System;
using System.Collections.Generic;
using Vintagestory.API.Config;
using Vintagestory.Server;

namespace RequeueRelief;

class ClientDisconnectReprocessor(Config config)
{
    // Cache the localised version of the crash disconnect message.
    private readonly string _crashReason = Lang.Get("The Players client crashed");

    /// <summary>
    /// Holds the time each client was accepted into the game. Entries are removed from this when they disconnect.
    /// </summary>
    private readonly IDictionary<int, DateTimeOffset> _acceptedTimes = new Dictionary<int, DateTimeOffset>();

    public event Action<ClientDisconnectEventData>? OnClientDisconnect;

    public void HandleClientAccepted(ConnectedClient client)
    {
        _acceptedTimes.Add(client.Id, DateTimeOffset.UtcNow);
    }

    public void HandleClientDisconnect(ConnectedClient client, string? othersReason, string? theirReason)
    {
        var wasFailedJoin = false;

        if (_acceptedTimes.Remove(client.Id, out var acceptedAt))
        {
            var sessionSeconds = (DateTimeOffset.UtcNow - acceptedAt).TotalSeconds;
            wasFailedJoin = sessionSeconds < config.Timings.FailedJoinThresholdSeconds;
        }

        ClientDisconnectEventData.DisconnectCause cause;
        if (othersReason == "Threw an exception at the server" || othersReason == _crashReason)
        {
            cause = ClientDisconnectEventData.DisconnectCause.Crash;
        }
        else if (othersReason == "Lost connection/disconnected")
        {
            // Potentially timed out. Or maybe they just closed the window. It's not possible to tell.
            cause = ClientDisconnectEventData.DisconnectCause.Timeout;
        }
        else if (othersReason == "Lost connection")
        {
            // This is different from "Lost connection/disconnected". Maybe. Not sure honestly.
            cause = ClientDisconnectEventData.DisconnectCause.Kicked;
        }
        else if (string.IsNullOrEmpty(theirReason) && othersReason == null)
        {
            // I guess? These are really not super clear.
            cause = ClientDisconnectEventData.DisconnectCause.Quit;
        }
        else
        {
            cause = ClientDisconnectEventData.DisconnectCause.Kicked;
        }

        var data = new ClientDisconnectEventData(client, wasFailedJoin, cause);
        OnClientDisconnect?.Invoke(data);
    }

    internal struct ClientDisconnectEventData
    {
        public readonly ConnectedClient Client;
        public readonly bool WasFailedJoin;
        public readonly DisconnectCause Cause;

        public ClientDisconnectEventData(ConnectedClient client, bool wasFailedJoin, DisconnectCause cause)
        {
            Client = client;
            WasFailedJoin = wasFailedJoin;
            Cause = cause;
        }

        internal enum DisconnectCause
        {
            Quit,
            Kicked,
            Crash,
            Timeout
        }
    }

    public void Reset()
    {
        _acceptedTimes.Clear();
    }
}