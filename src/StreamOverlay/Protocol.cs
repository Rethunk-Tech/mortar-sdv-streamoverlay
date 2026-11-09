using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace StreamOverlay;

/// <summary>Request parsing and authentication, kept away from the listener so tests need no game or SMAPI.</summary>
internal static class Protocol
{
    /// <summary>Compare tokens without leaking the matching prefix length through timing.</summary>
    internal static bool TokenMatches(string expected, string? provided) =>
        provided != null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided));

    /// <summary>Read one newline-terminated line of at most <paramref name="max"/> bytes; null when the peer stops
    /// early or sends more.</summary>
    internal static async Task<string?> ReadLine(NetworkStream stream, int max, CancellationToken cancel)
    {
        var bytes = new List<byte>();
        var one = new byte[1];
        while (await stream.ReadAsync(one, cancel) == 1)
        {
            if (one[0] == (byte)'\n')
                return Encoding.UTF8.GetString([.. bytes]).TrimEnd('\r');
            if (bytes.Count >= max)
                return null;
            bytes.Add(one[0]);
        }
        return null;
    }
}
