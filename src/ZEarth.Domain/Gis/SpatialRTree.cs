using System;
using System.Collections.Generic;

namespace ZEarth.Domain.Gis
{
    public struct Envelope
    {
        public double MinX; // Min Lon
        public double MinY; // Min Lat
        public double MaxX; // Max Lon
        public double MaxY; // Max Lat

        public Envelope(double minX, double minY, double maxX, double maxY)
        {
            MinX = Math.Min(minX, maxX);
            MinY = Math.Min(minY, maxY);
            MaxX = Math.Max(minX, maxX);
            MaxY = Math.Max(minY, maxY);
        }

        public bool Intersects(Envelope other)
        {
            return !(other.MinX > MaxX || other.MaxX < MinX || other.MinY > MaxY || other.MaxY < MinY);
        }

        public bool Contains(double x, double y)
        {
            return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
        }

        public void ExpandToInclude(Envelope other)
        {
            if (other.MinX < MinX) MinX = other.MinX;
            if (other.MinY < MinY) MinY = other.MinY;
            if (other.MaxX > MaxX) MaxX = other.MaxX;
            if (other.MaxY > MaxY) MaxY = other.MaxY;
        }
    }

    public class SpatialRTreeEntry<T>
    {
        public Envelope Bounds;
        public T Item;

        public SpatialRTreeEntry(Envelope bounds, T item)
        {
            Bounds = bounds;
            Item = item;
        }
    }

    public class SpatialRTree<T>
    {
        private const int MaxEntriesPerNode = 16;
        private readonly Node _root = new();
        private int _count = 0;

        public int Count => _count;

        public void Insert(T item, double minLon, double minLat, double maxLon, double maxLat)
        {
            var bounds = new Envelope(minLon, minLat, maxLon, maxLat);
            var entry = new SpatialRTreeEntry<T>(bounds, item);
            _root.Insert(entry);
            _count++;
        }

        public void InsertPoint(T item, double lon, double lat)
        {
            Insert(item, lon, lat, lon, lat);
        }

        public List<T> Query(double minLon, double minLat, double maxLon, double maxLat)
        {
            var searchBounds = new Envelope(minLon, minLat, maxLon, maxLat);
            var results = new List<T>();
            _root.Query(searchBounds, results);
            return results;
        }

        public void Clear()
        {
            _root.Children.Clear();
            _root.Entries.Clear();
            _count = 0;
        }

        private class Node
        {
            public Envelope Bounds = new(double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
            public List<SpatialRTreeEntry<T>> Entries = new();
            public List<Node> Children = new();

            public bool IsLeaf => Children.Count == 0;

            public void Insert(SpatialRTreeEntry<T> entry)
            {
                Bounds.ExpandToInclude(entry.Bounds);

                if (IsLeaf)
                {
                    Entries.Add(entry);
                    if (Entries.Count > MaxEntriesPerNode)
                    {
                        SplitLeaf();
                    }
                }
                else
                {
                    // Choose best child node (least enlargement)
                    Node bestChild = Children[0];
                    double minEnlargement = double.MaxValue;

                    foreach (var child in Children)
                    {
                        double enlargement = CalculateEnlargement(child.Bounds, entry.Bounds);
                        if (enlargement < minEnlargement)
                        {
                            minEnlargement = enlargement;
                            bestChild = child;
                        }
                    }

                    bestChild.Insert(entry);
                }
            }

            public void Query(Envelope searchBounds, List<T> results)
            {
                if (!Bounds.Intersects(searchBounds))
                    return;

                if (IsLeaf)
                {
                    foreach (var entry in Entries)
                    {
                        if (entry.Bounds.Intersects(searchBounds))
                        {
                            results.Add(entry.Item);
                        }
                    }
                }
                else
                {
                    foreach (var child in Children)
                    {
                        child.Query(searchBounds, results);
                    }
                }
            }

            private void SplitLeaf()
            {
                var n1 = new Node();
                var n2 = new Node();

                int half = Entries.Count / 2;
                for (int i = 0; i < half; i++) n1.Insert(Entries[i]);
                for (int i = half; i < Entries.Count; i++) n2.Insert(Entries[i]);

                Entries.Clear();
                Children.Add(n1);
                Children.Add(n2);
            }

            private static double CalculateEnlargement(Envelope current, Envelope addition)
            {
                double curArea = (current.MaxX - current.MinX) * (current.MaxY - current.MinY);
                double newMinX = Math.Min(current.MinX, addition.MinX);
                double newMinY = Math.Min(current.MinY, addition.MinY);
                double newMaxX = Math.Max(current.MaxX, addition.MaxX);
                double newMaxY = Math.Max(current.MaxY, addition.MaxY);
                double newArea = (newMaxX - newMinX) * (newMaxY - newMinY);
                return Math.Max(0.0, newArea - curArea);
            }
        }
    }
}
