using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;
using static Line.Framework.Graphics.WindowsRenderer;

public static class GeometryClipper
{
    private struct ClipVertex
    {
        public Vector2 Position;
        public RgbaFloat Color;
        public Vector2 UV;
    }

    // ===================== PUBLIC =====================

    public static VertexPositionColor[][] ClipTriangleByQuad(
        VertexPositionColor[] triangle,
        VertexPositionColor[] quad
    )
    {
        if (triangle == null || triangle.Length != 3)
            throw new ArgumentException("triangle must be 3 vertices");
        if (quad == null || quad.Length != 4)
            throw new ArgumentException("quad must be 4 vertices");

        var poly = new List<ClipVertex>(3);
        for (int i = 0; i < 3; i++)
            poly.Add(ToClip(triangle[i]));

        var clipPoly = new List<Vector2>(4);
        for (int i = 0; i < 4; i++)
            clipPoly.Add(quad[i].Position);

        var clipped = ClipByConvexPolygon(poly, clipPoly);

        if (clipped.Count < 3)
            return Array.Empty<VertexPositionColor[]>();

        var tris = TriangulateFan(clipped);

        var result = new VertexPositionColor[tris.Count / 3][];

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new VertexPositionColor[3];
            for (int j = 0; j < 3; j++)
                result[i][j] = ToVPC(tris[i * 3 + j]);
        }

        return result;
    }

    // ===================== CLIP =====================

    private static List<ClipVertex> ClipByConvexPolygon(
        List<ClipVertex> polygon,
        List<Vector2> clipPolygon
    )
    {
        if (polygon.Count < 3 || clipPolygon.Count < 3)
            return new List<ClipVertex>();

        var clip = EnsureCCW(clipPolygon);
        var output = new List<ClipVertex>(polygon);

        for (int i = 0; i < clip.Count; i++)
        {
            Vector2 a = clip[i];
            Vector2 b = clip[(i + 1) % clip.Count];

            output = ClipByEdge(output, a, b);

            if (output.Count < 3)
                return new List<ClipVertex>();
        }

        return Clean(output);
    }

    private static List<ClipVertex> ClipByEdge(List<ClipVertex> poly, Vector2 a, Vector2 b)
    {
        var output = new List<ClipVertex>();
        if (poly.Count == 0)
            return output;

        for (int i = 0; i < poly.Count; i++)
        {
            var cur = poly[i];
            var next = poly[(i + 1) % poly.Count];

            bool curIn = Inside(cur.Position, a, b);
            bool nextIn = Inside(next.Position, a, b);

            if (curIn && nextIn)
            {
                output.Add(cur);
                output.Add(next);
            }
            else if (curIn && !nextIn)
            {
                output.Add(cur);
                output.Add(Intersect(cur, next, a, b));
            }
            else if (!curIn && nextIn)
            {
                output.Add(Intersect(cur, next, a, b));
                output.Add(next);
            }
        }

        return output;
    }

    private static bool Inside(Vector2 p, Vector2 a, Vector2 b)
    {
        return Cross(b - a, p - a) >= -1e-5f;
    }

    private static ClipVertex Intersect(ClipVertex p1, ClipVertex p2, Vector2 a, Vector2 b)
    {
        Vector2 r = p2.Position - p1.Position;
        Vector2 s = b - a;

        float rxs = Cross(r, s);

        if (Math.Abs(rxs) < 1e-6f)
        {
            return p1;
        }

        float t = Cross(a - p1.Position, s) / rxs;
        t = Math.Clamp(t, 0f, 1f);

        Vector2 pos = p1.Position + r * t;

        RgbaFloat col = new RgbaFloat(
            p1.Color.R + (p2.Color.R - p1.Color.R) * t,
            p1.Color.G + (p2.Color.G - p1.Color.G) * t,
            p1.Color.B + (p2.Color.B - p1.Color.B) * t,
            p1.Color.A + (p2.Color.A - p1.Color.A) * t
        );

        Vector2 uv = p1.UV + (p2.UV - p1.UV) * t;

        return new ClipVertex
        {
            Position = pos,
            Color = col,
            UV = uv,
        };
    }

    // ===================== TRIANGULATE (SAFE FAN) =====================

    private static List<ClipVertex> TriangulateFan(List<ClipVertex> poly)
    {
        var result = new List<ClipVertex>();
        if (poly.Count < 3)
            return result;

        var p = Clean(poly);
        if (p.Count < 3)
            return result;

        var baseV = p[0];

        for (int i = 1; i < p.Count - 1; i++)
        {
            result.Add(baseV);
            result.Add(p[i]);
            result.Add(p[i + 1]);
        }

        return result;
    }

    // ===================== CLEAN =====================

    private static List<ClipVertex> Clean(List<ClipVertex> input)
    {
        var outList = new List<ClipVertex>();

        for (int i = 0; i < input.Count; i++)
        {
            var cur = input[i];
            var prev = input[(i - 1 + input.Count) % input.Count];

            if ((cur.Position - prev.Position).LengthSquared() < 1e-10f)
                continue;

            outList.Add(cur);
        }

        if (outList.Count < 3)
            return outList;

        var final = new List<ClipVertex>();

        int n = outList.Count;

        for (int i = 0; i < n; i++)
        {
            var a = outList[(i - 1 + n) % n].Position;
            var b = outList[i].Position;
            var c = outList[(i + 1) % n].Position;

            if (Math.Abs(Cross(b - a, c - a)) < 1e-10f)
                continue;

            final.Add(outList[i]);
        }

        return final.Count >= 3 ? final : outList;
    }

    // ===================== UTIL =====================

    private static List<Vector2> EnsureCCW(List<Vector2> v)
    {
        float area = 0;

        for (int i = 0; i < v.Count; i++)
        {
            var a = v[i];
            var b = v[(i + 1) % v.Count];
            area += a.X * b.Y - b.X * a.Y;
        }

        if (area < 0)
        {
            var r = new List<Vector2>(v);
            r.Reverse();
            return r;
        }

        return new List<Vector2>(v);
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    private static ClipVertex ToClip(VertexPositionColor v) =>
        new ClipVertex
        {
            Position = v.Position,
            Color = v.Color,
            UV = v.UV,
        };

    private static VertexPositionColor ToVPC(ClipVertex v) =>
        new VertexPositionColor(v.Position, v.Color, v.UV);
}
