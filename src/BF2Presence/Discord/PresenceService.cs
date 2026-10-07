using DiscordRPC;

namespace BF2Presence.Discord;

public sealed class PresenceService : IDisposable
{
    private readonly DiscordRpcClient _client;
    private PresenceModel? _last;

    public PresenceService(string clientId)
    {
        _client = new DiscordRpcClient(clientId);
        _client.OnReady += (_, e) => Console.WriteLine($"[discord] connected as {e.User.Username}");
        _client.OnConnectionFailed += (_, _) => Console.WriteLine("[discord] could not connect - is the Discord desktop app running?");
        _client.Initialize();
    }

    public void Update(PresenceModel model)
    {
        if (model == _last) return;
        _last = model;

        var presence = new RichPresence
        {
            Details = model.Details,
            State = model.State,
            Assets = new Assets
            {
                LargeImageKey = model.LargeImageKey,
                LargeImageText = model.LargeImageText,
                SmallImageKey = model.SmallImageKey,
                SmallImageText = model.SmallImageText,
            },
        };

        if (model.StartTimeUtc is { } start)
            presence.Timestamps = new Timestamps(start);

        _client.SetPresence(presence);
    }

    public void Clear()
    {
        if (_last is null) return;
        _last = null;
        _client.ClearPresence();
    }

    public void Dispose()
    {
        _client.ClearPresence();
        _client.Dispose();
    }
}
