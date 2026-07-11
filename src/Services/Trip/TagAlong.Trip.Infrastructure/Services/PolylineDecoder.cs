using NetTopologySuite.Geometries;

namespace TagAlong.Trip.Infrastructure.Services;

public static class PolylineDecoder
{
    private static readonly GeometryFactory _factory = new(new PrecisionModel(), 4326);

    // Decode a Google encoded polyline string into an NTS LineString.
    // Each coordinate is (longitude, latitude) to match NTS convention (X=lon, Y=lat).
    public static LineString Decode(string encoded)
    {
        var coords = new List<Coordinate>();
        int index = 0;
        int lat = 0, lng = 0;

        try
        {
            while (index < encoded.Length)
            {
                lat += DecodeChunk(encoded, ref index);
                lng += DecodeChunk(encoded, ref index);
                coords.Add(new Coordinate(lng / 1e5, lat / 1e5)); // X=lon, Y=lat
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Invalid encoded polyline at index {index}", ex);
        }

        if (coords.Count < 2)
            throw new InvalidOperationException("Encoded polyline has fewer than 2 points");

        return _factory.CreateLineString(coords.ToArray());
    }

    private static int DecodeChunk(string encoded, ref int index)
    {
        int result = 0, shift = 0, b;
        do
        {
            b = encoded[index++] - 63;
            result |= (b & 0x1F) << shift;
            shift += 5;
        } while (b >= 0x20);

        return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
    }

    // Simplify a LineString by keeping every Nth point, capped at maxPoints.
    // Always keeps first and last point.
    public static LineString Simplify(LineString line, int maxPoints = 200)
    {
        var coords = line.Coordinates;
        if (coords.Length <= maxPoints)
            return line;

        int step = (int)Math.Ceiling((double)(coords.Length - 2) / (maxPoints - 2));
        var kept = new List<Coordinate> { coords[0] };

        for (int i = step; i < coords.Length - 1; i += step)
            kept.Add(coords[i]);

        kept.Add(coords[^1]);
        return _factory.CreateLineString(kept.ToArray());
    }
}
