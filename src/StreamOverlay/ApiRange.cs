namespace StreamOverlay;

/// <summary>The game range the snapshot's reflection was checked against. SMAPI manifests carry a minimum only, so the
/// upper end is enforced here.</summary>
internal static class ApiRange
{
    public const int TestedGameMajor = 1;
    public const int TestedGameMinor = 6;
    public const int TestedGameMinPatch = 14;
    public const string TestedGame = "1.6.14 or a later 1.6.x";

    public static bool IsTestedGame(int major, int minor, int patch) =>
        major == TestedGameMajor && minor == TestedGameMinor && patch >= TestedGameMinPatch;
}
