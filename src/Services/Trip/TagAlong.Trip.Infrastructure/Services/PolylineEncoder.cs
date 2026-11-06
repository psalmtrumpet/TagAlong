using System.Text;

namespace TagAlong.Trip.Infrastructure.Services;

/// <summary>Google's encoded polyline format (precision 5); the reverse of <see cref="PolylineDecoder"/>.</summary>
public static class PolylineEncoder
{
    public static string Encode(IEnumerable<(double Lat, double Lng)> points)
    {
        var sb = new StringBuilder();
        long lastLat = 0, lastLng = 0;
        foreach (var (lat, lng) in points)
        {
            long la = (long)Math.Round(lat * 1e5), lo = (long)Math.Round(lng * 1e5);
            Write(sb, la - lastLat);
            Write(sb, lo - lastLng);
            lastLat = la;
            lastLng = lo;
        }
        return sb.ToString();
    }

    private static void Write(StringBuilder sb, long v)
    {
        v = v < 0 ? ~(v << 1) : v << 1;
        while (v >= 0x20)
        {
            sb.Append((char)((0x20 | (v & 0x1f)) + 63));
            v >>= 5;
        }
        sb.Append((char)(v + 63));
    }
}
