using System.Net.Sockets;
using System.Reflection;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace StreamOverlay;

public sealed class ModEntry : Mod
{
    private OverlayServer? server;
    private IModHelper? helper;
    private readonly HashSet<string> readErrors = [];
    private int shutdown;

    public override void Entry(IModHelper modHelper)
    {
        this.helper = modHelper;
        if (OverlayConfig.Read(modHelper.DirectoryPath, this.Monitor) is not { } config)
            return;

        string? raw = GameVersion(modHelper);
        if (raw == null || !SemanticVersion.TryParse(raw, out ISemanticVersion? version)
            || !ApiRange.IsTestedGame(version.MajorVersion, version.MinorVersion, version.PatchVersion))
        {
            this.Monitor.Log($"Stream overlay is off: Stardew Valley {raw ?? "unknown"} is outside the verified range {ApiRange.TestedGame}.", LogLevel.Warn);
            return;
        }

        var overlay = new OverlayServer(config.OverlayPort, config.OverlayToken!);
        try
        {
            overlay.Start();
        }
        catch (SocketException ex)
        {
            overlay.Dispose();
            this.Monitor.Log($"Stream overlay is off: could not bind 127.0.0.1:{config.OverlayPort}: {ex.Message}", LogLevel.Error);
            return;
        }

        this.server = overlay;
        modHelper.Events.GameLoop.OneSecondUpdateTicked += this.Update;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => this.Shutdown();
        this.Monitor.Log($"Stream overlay listening on 127.0.0.1:{overlay.Port}/state.", LogLevel.Info);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            this.Shutdown();
        base.Dispose(disposing);
    }

    private void Update(object? sender, OneSecondUpdateTickedEventArgs e)
    {
        if (this.server == null || this.helper == null)
            return;

        if (!Context.IsWorldReady)
        {
            this.server.SetNotInGame();
            return;
        }

        this.server.PublishSnapshot(
            () => ReadSnapshot(this.helper),
            this.readErrors,
            message => this.Monitor.Log(message, LogLevel.Error));
    }

    private void Shutdown()
    {
        if (Interlocked.Exchange(ref this.shutdown, 1) != 0)
            return;

        if (this.helper != null)
            this.helper.Events.GameLoop.OneSecondUpdateTicked -= this.Update;
        this.helper = null;
        this.server?.Dispose();
        this.server = null;
    }

    // SMAPI keeps Constants.GameVersion internal and refuses reflection into its own types, so the version comes from
    // the game's Game1.version, which SMAPI lets a mod reflect on.
    private static string? GameVersion(IModHelper helper) =>
        GameType() is { } game1 ? helper.Reflection.GetField<string>(game1, "version", false)?.GetValue() : null;

    private static Type? GameType() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "Stardew Valley", StringComparison.OrdinalIgnoreCase))
            ?.GetType("StardewValley.Game1");

    // SMAPI's reflection helper cannot bind a static property getter, so Game1's static properties are read directly.
    private static T? StaticProperty<T>(Type type, string name) =>
        (T?)(type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMemberException(type.FullName, name)).GetValue(null);

    private static OverlaySnapshot? ReadSnapshot(IModHelper helper)
    {
        if (GameType() is not { } game1)
            return null;

        object? player = StaticProperty<object>(game1, "player");
        object? location = StaticProperty<object>(game1, "currentLocation");
        if (player == null || location == null)
            return null;

        object? weather = helper.Reflection.GetMethod(location, "GetWeather", false)?.Invoke<object>([]);
        string weatherName = weather == null
            ? "unknown"
            : helper.Reflection.GetProperty<string>(weather, "Weather", false)?.GetValue() ?? "unknown";
        var skillLevels = new Dictionary<string, int>();
        var getSkillLevel = helper.Reflection.GetMethod(player, "GetSkillLevel", true);
        foreach ((string name, int index) in new[] { ("farming", 0), ("fishing", 1), ("foraging", 2), ("mining", 3), ("combat", 4), ("luck", 5) })
            skillLevels[name] = getSkillLevel.Invoke<int>(index);

        int day = helper.Reflection.GetField<int>(game1, "dayOfMonth", true).GetValue();
        (string Name, int Hearts)? friend = OverlayDerived.Friend(
            helper.Reflection.GetProperty<string>(player, "spouse", false)?.GetValue(),
            FriendshipHearts(helper, player));
        object? pet = helper.Reflection.GetMethod(player, "getPet", false)?.Invoke<object>([]);
        object? bowl = pet == null ? null : helper.Reflection.GetMethod(pet, "GetPetBowl", false)?.Invoke<object>([]);

        return new OverlaySnapshot(
            true,
            helper.Reflection.GetProperty<string>(location, "Name", true).GetValue(),
            helper.Reflection.GetProperty<string>(player, "Name", true).GetValue(),
            StaticProperty<string>(game1, "currentSeason") ?? "unknown",
            day,
            helper.Reflection.GetField<int>(game1, "year", true).GetValue(),
            helper.Reflection.GetField<int>(game1, "timeOfDay", true).GetValue(),
            helper.Reflection.GetProperty<int>(player, "Money", true).GetValue(),
            weatherName,
            helper.Reflection.GetField<int>(player, "health", true).GetValue(),
            helper.Reflection.GetField<int>(player, "maxHealth", true).GetValue(),
            helper.Reflection.GetProperty<float>(player, "Stamina", true).GetValue(),
            helper.Reflection.GetProperty<int>(player, "MaxStamina", true).GetValue(),
            skillLevels,
            OverlayDerived.DayOfWeekName(day),
            OverlayDerived.DaysLeftInSeason(day),
            friend?.Name,
            friend?.Hearts,
            pet == null ? null : helper.Reflection.GetProperty<string>(pet, "Name", false)?.GetValue(),
            pet == null ? null : NetValue<string>(helper, pet, "petType"),
            bowl == null ? null : NetValue<bool>(helper, bowl, "watered"));
    }

    // friendshipData is a Netcode dictionary whose Keys is a struct, which SMAPI's reflection helper cannot bind.
    private static IEnumerable<(string Name, int Hearts)> FriendshipHearts(IModHelper helper, object player)
    {
        object? data = helper.Reflection.GetField<object>(player, "friendshipData", false)?.GetValue();
        var keys = data?.GetType().GetProperty("Keys")?.GetValue(data) as IEnumerable<string>;
        var hearts = helper.Reflection.GetMethod(player, "getFriendshipHeartLevelForNPC", false);
        if (keys == null || hearts == null)
            yield break;
        foreach (string name in keys)
            yield return (name, hearts.Invoke<int>(name));
    }

    private static T? NetValue<T>(IModHelper helper, object owner, string field)
    {
        object? net = helper.Reflection.GetField<object>(owner, field, false)?.GetValue();
        return net == null ? default : (T?)net.GetType().GetProperty("Value")?.GetValue(net);
    }
}
