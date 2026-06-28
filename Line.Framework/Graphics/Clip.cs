using System;
using System.Collections.Generic;
using System.Numerics;
using Line.Framework;
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

    // ---------- 公开入口 ----------
    public static VertexPositionColor[][] ClipTriangleByQuad(
        VertexPositionColor[] triangle,
        VertexPositionColor[] quad)
    {
        if (triangle == null || triangle.Length != 3)
            throw new ArgumentException("三角形必须包含3个顶点");
        if (quad == null || quad.Length != 4)
            throw new ArgumentException("四边形必须包含4个顶点");

        var poly = new List<ClipVertex>(3);
        foreach (var v in triangle)
            poly.Add(ConvertToClipVertex(v));

        var clipPositions = new List<Vector2>(4);
        foreach (var v in quad)
            clipPositions.Add(v.Position);

        var clipped = ClipByConvexPolygon(poly, clipPositions);
        if (clipped.Count < 3)
            return Array.Empty<VertexPositionColor[]>();

        var triangulated = Triangulate(clipped);
        if (triangulated.Count < 3)
            return Array.Empty<VertexPositionColor[]>();

        int triCount = triangulated.Count / 3;
        var result = new VertexPositionColor[triCount][];
        for (int i = 0; i < triCount; i++)
        {
            result[i] = new VertexPositionColor[3];
            for (int j = 0; j < 3; j++)
            {
                var src = triangulated[i * 3 + j];
                result[i][j] = ConvertToVertexPositionColor(src);
            }
        }
        return result;
    }

    // ---------- 转换函数 ----------
    private static ClipVertex ConvertToClipVertex(VertexPositionColor v) =>
        new ClipVertex { Position = v.Position, Color = v.Color, UV = v.UV };

    private static VertexPositionColor ConvertToVertexPositionColor(ClipVertex v) =>
        new VertexPositionColor(v.Position, v.Color, v.UV);

    // ---------- 裁剪核心 ----------
    private static List<ClipVertex> ClipByConvexPolygon(
        List<ClipVertex> polygon,
        List<Vector2> clipPolygon)
    {
        if (polygon.Count < 3 || clipPolygon.Count < 3)
            return new List<ClipVertex>();

        var clipVerts = EnsureCounterClockwise(clipPolygon);
        var result = new List<ClipVertex>(polygon);

        for (int i = 0; i < clipVerts.Count; i++)
        {
            Vector2 edgeStart = clipVerts[i];
            Vector2 edgeEnd = clipVerts[(i + 1) % clipVerts.Count];
            result = ClipByEdge(result, edgeStart, edgeEnd);
            if (result.Count == 0) break;
        }
        return result;
    }

    private static List<ClipVertex> ClipByEdge(
        List<ClipVertex> polygon,
        Vector2 edgeStart,
        Vector2 edgeEnd)
    {
        var output = new List<ClipVertex>();
        if (polygon.Count == 0) return output;

        for (int i = 0; i < polygon.Count; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Count];

            bool curInside = IsInside(current.Position, edgeStart, edgeEnd);
            bool nextInside = IsInside(next.Position, edgeStart, edgeEnd);

            if (curInside)
            {
                output.Add(current);
                if (!nextInside)
                    output.Add(ComputeIntersection(current, next, edgeStart, edgeEnd));
            }
            else if (nextInside)
            {
                output.Add(ComputeIntersection(current, next, edgeStart, edgeEnd));
            }
        }
        return output;
    }

    private static bool IsInside(Vector2 point, Vector2 a, Vector2 b)
    {
        float cross = (b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X);
        return cross >= -1e-6f; // 浮点容差
    }

    private static ClipVertex ComputeIntersection(
        ClipVertex p1, ClipVertex p2,
        Vector2 a, Vector2 b)
    {
        Vector2 p1p2 = p2.Position - p1.Position;
        Vector2 ab = b - a;
        float denom = p1p2.X * (-ab.Y) - p1p2.Y * (-ab.X);
        float t = 0;
        if (Math.Abs(denom) > 1e-6f)
        {
            Vector2 aMinusP1 = a - p1.Position;
            Vector2 aMinusB = a - b;
            float cross1 = aMinusP1.X * aMinusB.Y - aMinusP1.Y * aMinusB.X;
            float cross2 = p1p2.X * aMinusB.Y - p1p2.Y * aMinusB.X;
            t = cross1 / cross2;
        }
        t = Math.Clamp(t, 0, 1);

        // 位置插值
        Vector2 pos = p1.Position + t * p1p2;

        // 颜色插值
        RgbaFloat color = new RgbaFloat(
            p1.Color.R + t * (p2.Color.R - p1.Color.R),
            p1.Color.G + t * (p2.Color.G - p1.Color.G),
            p1.Color.B + t * (p2.Color.B - p1.Color.B),
            p1.Color.A + t * (p2.Color.A - p1.Color.A)
        );

        Vector2 uv = p1.UV + t * (p2.UV - p1.UV);

        return new ClipVertex { Position = pos, Color = color, UV = uv };
    }

    private static List<Vector2> EnsureCounterClockwise(List<Vector2> vertices)
    {
        float area = 0;
        int n = vertices.Count;
        for (int i = 0; i < n; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % n];
            area += (a.X * b.Y - b.X * a.Y);
        }
        if (area < 0)
        {
            var reversed = new List<Vector2>(vertices);
            reversed.Reverse();
            return reversed;
        }
        return vertices;
    }

    // ==================== 稳定的三角剖分 ====================
    private static List<ClipVertex> Triangulate(List<ClipVertex> polygon)
    {
        var result = new List<ClipVertex>();
        if (polygon.Count < 3) return result;

        // 复制一份顶点
        var vertices = new List<ClipVertex>(polygon);
        int n = vertices.Count;

        // 判断顶点顺序（用于凸性判断）
        float area = 0;
        for (int i = 0; i < n; i++)
        {
            var a = vertices[i].Position;
            var b = vertices[(i + 1) % n].Position;
            area += (a.X * b.Y - b.X * a.Y);
        }
        bool clockwise = area < 0;

        // 安全计数器，防止无限循环
        int maxIter = vertices.Count * 200;
        int iter = 0;

        int index = 0;
        while (vertices.Count >= 3 && iter < maxIter)
        {
            iter++;
            int count = vertices.Count;
            int prev = (index - 1 + count) % count;
            int next = (index + 1) % count;

            if (IsEar(vertices, prev, index, next, clockwise))
            {
                // 记录三角形
                result.Add(vertices[prev]);
                result.Add(vertices[index]);
                result.Add(vertices[next]);

                // 移除当前顶点
                vertices.RemoveAt(index);

                // 索引回退到前一个顶点（避免跳过候选）
                if (vertices.Count > 0)
                    index = Math.Min(prev, vertices.Count - 1);
                else
                    index = 0;
            }
            else
            {
                // 当前顶点不是耳，移到下一个
                index = (index + 1) % vertices.Count;
            }
        }

        // 如果循环因超时退出，说明多边形可能退化，返回空
        if (iter >= maxIter)
        {
            Log.Debug($"[Triangulate] 达到最大迭代次数，多边形顶点数 {polygon.Count}，可能为自交或退化。");
            return new List<ClipVertex>();
        }

        return result;
    }

    private static bool IsEar(
        List<ClipVertex> polygon,
        int prev, int curr, int next,
        bool clockwise)
    {
        var p = polygon[prev].Position;
        var c = polygon[curr].Position;
        var n = polygon[next].Position;

        // 1. 凸性检查 (叉积)
        float cross = (c.X - p.X) * (n.Y - p.Y) - (c.Y - p.Y) * (n.X - p.X);
        if (clockwise) cross = -cross;

        // 浮点容差，防止共线误判
        if (cross <= 1e-8f) return false;

        // 2. 检查三角形内是否包含其他顶点
        for (int i = 0; i < polygon.Count; i++)
        {
            if (i == prev || i == curr || i == next) continue;
            var test = polygon[i].Position;
            if (PointInTriangleStrict(test, p, c, n))
                return false;
        }
        return true;
    }

    // 严格的点在三角形内（不包含边界）
    private static bool PointInTriangleStrict(Vector2 pt, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(pt, a, b);
        float d2 = Sign(pt, b, c);
        float d3 = Sign(pt, c, a);
        // 所有符号必须相同（全正或全负），且不能为0（边界）
        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        // 如果既有正又有负，则在外；如果全正或全负，则在内部（不包括边界）
        return !(hasNeg && hasPos) && (d1 != 0 && d2 != 0 && d3 != 0);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
    }
}