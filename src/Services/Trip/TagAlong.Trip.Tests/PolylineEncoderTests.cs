using TagAlong.Trip.Infrastructure.Services;
using Xunit;

namespace TagAlong.Trip.Tests;

public class PolylineEncoderTests
{
    [Fact]
    public void Encode_GoogleExample_MatchesGoogle()
    {
        // Google's documented example
        var encoded = PolylineEncoder.Encode(new[] { (38.5, -120.2), (40.7, -120.95), (43.252, -126.453) });
        Assert.Equal("_p~iF~ps|U_ulLnnqC_mqNvxq`@", encoded);
    }

    [Fact]
    public void Encode_ThenDecode_RoundTrips()
    {
        var points = new[] { (6.48888, 3.38144), (6.5764, 3.39701), (6.60175, 3.35136) };
        var line = PolylineDecoder.Decode(PolylineEncoder.Encode(points));
        Assert.Equal(3, line.NumPoints);
        for (var i = 0; i < points.Length; i++)
        {
            Assert.Equal(points[i].Item1, line.Coordinates[i].Y, 5);
            Assert.Equal(points[i].Item2, line.Coordinates[i].X, 5);
        }
    }
}
