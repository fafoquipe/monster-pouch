using System;
using System.Collections.Generic;

namespace MonsterPouch.Gameplay.Match
{
    public readonly struct MartioSegmentCrossing
    {
        public readonly ProjectileGridPoint Cell;
        public readonly double Fraction;
        public readonly bool CornerOnly;
        public MartioSegmentCrossing(ProjectileGridPoint cell, double fraction, bool cornerOnly)
        { Cell = cell; Fraction = fraction; CornerOnly = cornerOnly; }
    }

    // The original braid is authored once. This basis maps it onto ANY target heading.
    // One forward step is one Chebyshev board step, preserving all eight original directions.
    public static class MartioFlightGeometry
    {
        const double Epsilon = 1e-10;

        public static MartioFlightPoint Node(ProjectileGridPoint origin, ProjectileGridPoint heading,
            int step, BraidedProjectile projectile)
        {
            long scale = Math.Max(Math.Abs((long)heading.X), Math.Abs((long)heading.Y));
            if (scale == 0 || scale > 65536) throw new ArgumentOutOfRangeException(nameof(heading));
            var canonical = BraidedProjectileGeometry.Node(new ProjectileGridPoint(0, 0),
                new ProjectileGridPoint(0, 1), step, projectile, 1, 1);
            double dx = heading.X / (double)scale, dy = heading.Y / (double)scale;
            return new MartioFlightPoint(origin.X + dx * canonical.Y + dy * canonical.X,
                origin.Y + dy * canonical.Y - dx * canonical.X);
        }

        // Half-open cells give every exact position one owner, also along a grid boundary.
        public static ProjectileGridPoint OccupiedCell(MartioFlightPoint position)
        {
            Validate(position);
            return new ProjectileGridPoint((int)Math.Floor(position.X + .5), (int)Math.Floor(position.Y + .5));
        }

        public static IReadOnlyList<MartioSegmentCrossing> TraceSegment(MartioFlightPoint start, MartioFlightPoint end)
        {
            Validate(start); Validate(end);
            double dx = end.X - start.X, dy = end.Y - start.Y;
            // Every braid segment is <= 2 cells on either axis; reject unrelated large scans.
            if (Math.Abs(dx) > 2 + Epsilon || Math.Abs(dy) > 2 + Epsilon)
                throw new ArgumentOutOfRangeException(nameof(end));
            if (Math.Abs(dx) < Epsilon && Math.Abs(dy) < Epsilon)
                return new[] { new MartioSegmentCrossing(OccupiedCell(start), 0, false) };
            int minX = (int)Math.Floor(Math.Min(start.X, end.X) - .5);
            int maxX = (int)Math.Ceiling(Math.Max(start.X, end.X) + .5);
            int minY = (int)Math.Floor(Math.Min(start.Y, end.Y) - .5);
            int maxY = (int)Math.Ceiling(Math.Max(start.Y, end.Y) + .5);
            var crossings = new List<MartioSegmentCrossing>();
            for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
            {
                double enter = 0, exit = 1;
                if (!Clip(start.X, dx, x, ref enter, ref exit) || !Clip(start.Y, dy, y, ref enter, ref exit)) continue;
                bool corner = exit - enter <= Epsilon;
                if (!corner)
                {
                    double midpoint = (enter + exit) * .5;
                    var owner = OccupiedCell(new MartioFlightPoint(start.X + dx * midpoint, start.Y + dy * midpoint));
                    // A line exactly on a shared boundary cannot occupy both adjacent cells.
                    if (owner.X != x || owner.Y != y) continue;
                }
                crossings.Add(new MartioSegmentCrossing(new ProjectileGridPoint(x, y), Math.Max(0, enter), corner));
            }
            crossings.Sort((a, b) => {
                int c = a.Fraction.CompareTo(b.Fraction);
                if (c == 0) c = b.CornerOnly.CompareTo(a.CornerOnly);
                if (c == 0) c = a.Cell.Y.CompareTo(b.Cell.Y);
                return c != 0 ? c : a.Cell.X.CompareTo(b.Cell.X);
            });
            return crossings.AsReadOnly();
        }

        static bool Clip(double start, double change, int cell, ref double enter, ref double exit)
        {
            if (Math.Abs(change) < Epsilon) return start >= cell - .5 && start <= cell + .5;
            double a = (cell - .5 - start) / change, b = (cell + .5 - start) / change;
            enter = Math.Max(enter, Math.Min(a, b)); exit = Math.Min(exit, Math.Max(a, b));
            return enter <= exit + Epsilon;
        }

        static void Validate(MartioFlightPoint point)
        {
            if (double.IsNaN(point.X) || double.IsNaN(point.Y) || double.IsInfinity(point.X) || double.IsInfinity(point.Y) ||
                Math.Abs(point.X) > 1000000 || Math.Abs(point.Y) > 1000000)
                throw new ArgumentOutOfRangeException(nameof(point));
        }
    }

    public static class MartioAttackLaunch
    {
        public static bool CanStartAttack(MartioPrototypeConfig config, ProjectileGridPoint origin, ProjectileGridPoint target)
        {
            if (config == null || config.AttackRange == null || !config.AttackRange.IsDefined || config.AttackRange.Value < 1) return false;
            long dx = Math.Abs((long)target.X - origin.X), dy = Math.Abs((long)target.Y - origin.Y);
            return (dx > 0 || dy > 0) && dx <= config.AttackRange.Value && dy <= config.AttackRange.Value;
        }

        public static MartioProjectileSimulation Create(MartioPrototypeConfig config, ProjectileGridPoint origin,
            ProjectileGridPoint targetAtRelease, bool explodes, bool pierces, bool upgradedMoons)
        {
            if (!CanStartAttack(config, origin, targetAtRelease)) throw new InvalidOperationException("El rival debe estar dentro del alcance de inicio.");
            if (!config.ProjectileTravelRange.IsDefined || !config.ProjectileForwardStepsPerSecond.IsDefined)
                throw new InvalidOperationException("El recorrido y la velocidad de vuelo deben estar definidos explícitamente.");
            var heading = new ProjectileGridPoint(checked(targetAtRelease.X - origin.X), checked(targetAtRelease.Y - origin.Y));
            // Store only a heading. Never store the target's identity, cell or later position.
            return new MartioProjectileSimulation(config, origin, heading, config.ProjectileTravelRange.Value,
                config.ProjectileForwardStepsPerSecond.Value, explodes, pierces, upgradedMoons, config.RepeatHits, config.CornerContacts);
        }
    }
}
