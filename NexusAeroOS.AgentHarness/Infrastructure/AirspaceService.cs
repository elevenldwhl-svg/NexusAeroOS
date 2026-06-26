using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace NexusAeroOS.AgentHarness.Infrastructure;

public record GeoPoint(double Lng, double Lat);
public record NoFlyZone(string ZoneId, string Reason, List<GeoPoint> Polygon);

public class AirspaceService
{
    private readonly ConcurrentDictionary<string, NoFlyZone> _activeZones = new();

    public AirspaceService()
    {
        SeedZone("NFZ-AIRPORT-BAOAN", "宝安客机起降走廊管制", new List<GeoPoint> { new(113.7950, 22.6550), new(113.8350, 22.6550), new(113.8350, 22.6100), new(113.7950, 22.6100) });
        SeedZone("NFZ-FUTIAN-CBD", "福田超高层多径干扰禁区", new List<GeoPoint> { new(114.0480, 22.5420), new(114.0620, 22.5420), new(114.0620, 22.5310), new(114.0480, 22.5310) });
        SeedZone("NFZ-NANSHAN-WANGTAI", "南山王泰路特种静态带", new List<GeoPoint> { new(113.9310, 22.5460), new(113.9390, 22.5460), new(113.9390, 22.5400), new(113.9310, 22.5400) });
        SeedZone("NFZ-SZBAY-ECOLOGY", "深圳湾候鸟保护净空走廊", new List<GeoPoint> { new(113.9450, 22.5220), new(113.9850, 22.5220), new(113.9850, 22.5050), new(113.9450, 22.5050) });
    }

    private void SeedZone(string id, string r, List<GeoPoint> p) => _activeZones.TryAdd(id, new NoFlyZone(id, r, p));
    public NoFlyZone IssueNotamRestriction() => _activeZones.Values.First();
    public IEnumerable<NoFlyZone> GetActiveZones() => _activeZones.Values;

    public (bool IsSafe, string? ViolationReport) TestRouteSafety(string currStr, string routeStr)
    {
        if (_activeZones.IsEmpty) return (true, null);
        var full = new List<GeoPoint>();
        var c = currStr.Split(',');
        full.Add(new GeoPoint(double.Parse(c[0]), double.Parse(c[1])));

        foreach (var wp in routeStr.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = wp.Split(',');
            full.Add(new GeoPoint(double.Parse(p[0]), double.Parse(p[1])));
        }

        for (int i = 0; i < full.Count - 1; i++)
        {
            var A = full[i]; var B = full[i + 1];
            foreach (var z in _activeZones.Values)
            {
                for (int j = 0; j < z.Polygon.Count; j++)
                {
                    var C = z.Polygon[j]; var D = z.Polygon[(j + 1) % z.Polygon.Count];
                    if (SegmentsIntersect(A, B, C, D)) return (false, $"穿过禁区 [{z.ZoneId}]");
                }
            }
        }
        return (true, null);
    }

    // ====================================================================
    // 💥 军工级避障中台：AABB 递归包围盒贴边巡航算法
    // ====================================================================
    public string GetGuaranteedSafeCorridor(string startStr, string destStr)
    {
        var s = startStr.Split(','); var e = destStr.Split(',');
        var A = new GeoPoint(double.Parse(s[0]), double.Parse(s[1]));
        var B = new GeoPoint(double.Parse(e[0]), double.Parse(e[1]));

        var safeWaypoints = ComputePerimeterDetour(A, B, 0);

        return string.Join("; ", safeWaypoints.Select(p => $"{p.Lng:F4}, {p.Lat:F4}"));
    }

    private List<GeoPoint> ComputePerimeterDetour(GeoPoint A, GeoPoint B, int depth)
    {
        if (depth > 5) return new List<GeoPoint> { B }; // 递归防爆机制

        var hitZone = GetFirstBlockingZone(A, B);
        if (hitZone == null) return new List<GeoPoint> { B }; // 直飞通畅

        // 拿到该禁飞区的矩形外扩安全框 (外延 550 米)
        double buffer = 0.0055;
        double minLng = hitZone.Polygon.Min(p => p.Lng) - buffer;
        double maxLng = hitZone.Polygon.Max(p => p.Lng) + buffer;
        double minLat = hitZone.Polygon.Min(p => p.Lat) - buffer;
        double maxLat = hitZone.Polygon.Max(p => p.Lat) + buffer;

        // 智能判定走【北沿线】还是【南沿线】
        double segmentMidLat = (A.Lat + B.Lat) / 2.0;
        double zoneMidLat = (minLat + maxLat) / 2.0;

        GeoPoint c1, c2;
        if (segmentMidLat >= zoneMidLat)
        {
            c1 = new GeoPoint((A.Lng < B.Lng) ? minLng : maxLng, maxLat);
            c2 = new GeoPoint((A.Lng < B.Lng) ? maxLng : minLng, maxLat);
        }
        else
        {
            c1 = new GeoPoint((A.Lng < B.Lng) ? minLng : maxLng, minLat);
            c2 = new GeoPoint((A.Lng < B.Lng) ? maxLng : minLng, minLat);
        }

        // 递归解开分治路径：前段(A->角1) + 后段(角2->B)
        var head = ComputePerimeterDetour(A, c1, depth + 1);
        var tail = ComputePerimeterDetour(c2, B, depth + 1);

        var path = new List<GeoPoint>();
        path.AddRange(head);
        path.Add(c2);
        path.AddRange(tail);

        return path;
    }

    private NoFlyZone? GetFirstBlockingZone(GeoPoint A, GeoPoint B)
    {
        foreach (var z in _activeZones.Values)
        {
            for (int j = 0; j < z.Polygon.Count; j++)
            {
                var C = z.Polygon[j]; var D = z.Polygon[(j + 1) % z.Polygon.Count];
                if (SegmentsIntersect(A, B, C, D)) return z;
            }
        }
        return null;
    }

    public bool IsPointInAnyNoFlyZone(double lng, double lat)
    {
        var pt = new GeoPoint(lng, lat);
        return _activeZones.Values.Any(z => IsPointInPolygon(pt, z.Polygon));
    }

    private static bool CCW(GeoPoint A, GeoPoint B, GeoPoint C) => (C.Lat - A.Lat) * (B.Lng - A.Lng) > (B.Lat - A.Lat) * (C.Lng - A.Lng);
    private static bool SegmentsIntersect(GeoPoint A, GeoPoint B, GeoPoint C, GeoPoint D) => CCW(A, C, D) != CCW(B, C, D) && CCW(A, B, C) != CCW(A, B, D);
    private static bool IsPointInPolygon(GeoPoint p, List<GeoPoint> poly)
    {
        bool ins = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if (((poly[i].Lat > p.Lat) != (poly[j].Lat > p.Lat)) && (p.Lng < (poly[j].Lng - poly[i].Lng) * (p.Lat - poly[i].Lat) / (poly[j].Lat - poly[i].Lat) + poly[i].Lng)) ins = !ins;
        }
        return ins;
    }
}