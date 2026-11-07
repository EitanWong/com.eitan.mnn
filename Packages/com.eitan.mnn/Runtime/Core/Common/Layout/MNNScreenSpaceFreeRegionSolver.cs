using System.Collections.Generic;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>
    /// Reusable bounded search for an axis-aligned empty region. Coordinates may be
    /// pixels or logical units. Buffers are retained; no allocations after construction.
    /// </summary>
    public sealed class MNNScreenSpaceFreeRegionSolver
    {
        private const int Capacity = 64;
        private readonly List<Rect> _regions = new List<Rect>(Capacity);
        private readonly List<Rect> _next = new List<Rect>(Capacity);
        private Vector2 _fallbackCenter;

        public Rect LargestRegion
        {
            get
            {
                Rect best = new Rect(_fallbackCenter, Vector2.zero);
                foreach (Rect region in _regions)
                    if (Area(region) > Area(best)) best = region;
                return best;
            }
        }

        public void Reset(Rect viewport)
        {
            _regions.Clear();
            _next.Clear();
            _fallbackCenter = IsFinite(viewport) ? viewport.center : Vector2.zero;
            if (IsUsable(viewport)) _regions.Add(viewport);
        }

        public void Exclude(Rect occupied, float padding = 0f)
        {
            if (!IsUsable(occupied)) return;
            padding = float.IsNaN(padding) || float.IsInfinity(padding) ? 0f : Mathf.Max(0f, padding);
            occupied = Rect.MinMaxRect(occupied.xMin - padding, occupied.yMin - padding,
                occupied.xMax + padding, occupied.yMax + padding);
            if (!IsFinite(occupied)) return;
            _next.Clear();
            foreach (Rect region in _regions)
            {
                if (!region.Overlaps(occupied)) { Add(region); continue; }
                Add(Rect.MinMaxRect(region.xMin, region.yMin, Mathf.Min(region.xMax, occupied.xMin), region.yMax));
                Add(Rect.MinMaxRect(Mathf.Max(region.xMin, occupied.xMax), region.yMin, region.xMax, region.yMax));
                Add(Rect.MinMaxRect(region.xMin, region.yMin, region.xMax, Mathf.Min(region.yMax, occupied.yMin)));
                Add(Rect.MinMaxRect(region.xMin, Mathf.Max(region.yMin, occupied.yMax), region.xMax, region.yMax));
            }
            _regions.Clear();
            _regions.AddRange(_next);
        }

        private void Add(Rect candidate)
        {
            if (!IsUsable(candidate)) return;
            // Discard contained duplicates. Limit candidates on fragmented screens;
            // discarded candidates cannot compromise the validity of retained regions.
            for (int i = _next.Count - 1; i >= 0; --i)
            {
                Rect existing = _next[i];
                if (Contains(existing, candidate)) return;
                if (Contains(candidate, existing)) _next.RemoveAt(i);
            }
            if (_next.Count < Capacity) { _next.Add(candidate); return; }
            int smallest = 0;
            for (int i = 1; i < _next.Count; ++i)
                if (Area(_next[i]) < Area(_next[smallest])) smallest = i;
            if (Area(candidate) > Area(_next[smallest])) _next[smallest] = candidate;
        }

        private static double Area(Rect rect) => (double)rect.width * rect.height;
        private static bool Contains(Rect outer, Rect inner) => outer.xMin <= inner.xMin && outer.yMin <= inner.yMin &&
            outer.xMax >= inner.xMax && outer.yMax >= inner.yMax;
        private static bool IsUsable(Rect rect) => IsFinite(rect) && rect.width > 0f && rect.height > 0f;
        private static bool IsFinite(Rect rect) => Finite(rect.x) && Finite(rect.y) && Finite(rect.width) && Finite(rect.height) &&
            Finite(rect.xMax) && Finite(rect.yMax);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
