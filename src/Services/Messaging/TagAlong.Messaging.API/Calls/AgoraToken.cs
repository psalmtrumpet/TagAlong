using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace TagAlong.Messaging.API.Calls;

/// <summary>
/// Agora RTC token (AccessToken2, "007…"): lets one person join one call
/// channel to talk, for a limited time. Signed with the project's App
/// Certificate, which never leaves the server. Same format as Agora's own
/// token builders (RtcTokenBuilder2.BuildTokenWithUid, publisher role).
/// </summary>
public static class AgoraToken
{
    private const ushort RtcService = 1;
    private const ushort JoinChannel = 1, PublishAudio = 2, PublishVideo = 3, PublishData = 4;

    /// <param name="expireSeconds">How long the token (and its privileges) last, from now.</param>
    public static string BuildRtc(string appId, string appCertificate, string channel, uint uid, uint expireSeconds,
        uint? issuedAt = null, uint? salt = null)
    {
        var issue = issuedAt ?? (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var s = salt ?? (uint)RandomNumberGenerator.GetInt32(1, 99999999);

        // Service: RTC, its privileges (sorted by key), the channel and the uid as text
        var service = new MemoryStream();
        U16(service, RtcService);
        U16(service, 4);
        foreach (var key in new[] { JoinChannel, PublishAudio, PublishVideo, PublishData })
        {
            U16(service, key);
            U32(service, expireSeconds);
        }
        Str(service, Encoding.UTF8.GetBytes(channel));
        Str(service, uid == 0 ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(uid.ToString()));

        var info = new MemoryStream();
        Str(info, Encoding.UTF8.GetBytes(appId));
        U32(info, issue);
        U32(info, expireSeconds);
        U32(info, s);
        U16(info, 1); // one service
        service.Position = 0;
        service.CopyTo(info);
        var signingInfo = info.ToArray();

        // signing = HMAC(issue, certificate) → HMAC(salt, ·); signature = HMAC(signing, info)
        var signing = HMACSHA256.HashData(LeU32(issue), Encoding.UTF8.GetBytes(appCertificate));
        signing = HMACSHA256.HashData(LeU32(s), signing);
        var signature = HMACSHA256.HashData(signing, signingInfo);

        var content = new MemoryStream();
        Str(content, signature);
        content.Write(signingInfo);

        var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(content.ToArray());
        return "007" + Convert.ToBase64String(compressed.ToArray());
    }

    private static byte[] LeU32(uint v) => BitConverter.IsLittleEndian ? BitConverter.GetBytes(v) : BitConverter.GetBytes(v).Reverse().ToArray();

    private static void U16(Stream s, ushort v)
    {
        s.WriteByte((byte)v);
        s.WriteByte((byte)(v >> 8));
    }

    private static void U32(Stream s, uint v) => s.Write(LeU32(v));

    private static void Str(Stream s, byte[] bytes)
    {
        U16(s, (ushort)bytes.Length);
        s.Write(bytes);
    }
}
