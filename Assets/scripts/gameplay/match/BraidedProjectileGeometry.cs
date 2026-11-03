using System;
using System.Collections.Generic;

namespace MonsterPouch.Gameplay.Match
{
    public readonly struct ProjectileGridPoint : IEquatable<ProjectileGridPoint>
    {
        public readonly int X, Y;
        public ProjectileGridPoint(int x, int y) { X = x; Y = y; }
        public bool Equals(ProjectileGridPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object other) => other is ProjectileGridPoint point && Equals(point);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
        public override string ToString() => "(" + X + ", " + Y + ")";
    }

    public enum BraidedProjectile { Center, FirstMoon, SecondMoon }

    public readonly struct ProjectileCellCrossing
    {
        public readonly ProjectileGridPoint Cell;
        // The line touches only the cell's corner; the eventual collision policy
        // must explicitly decide whether a zero-area touch can hit an occupant.
        public readonly bool CornerOnly;
        public ProjectileCellCrossing(ProjectileGridPoint cell, bool cornerOnly)
        { Cell = cell; CornerOnly = cornerOnly; }
    }

    // Geometry only: no roster entry, target references, speed, damage or hit policy.
    // This is dormant until the character's remaining rules have been approved.
    public static class BraidedProjectileGeometry
    {
        public static ProjectileGridPoint Node(ProjectileGridPoint origin, ProjectileGridPoint forward,
            int step, BraidedProjectile projectile, int amplitude, int firstMoonSide)
        {
            ValidateHeading(forward);
            if (step < 0) throw new ArgumentOutOfRangeException(nameof(step));
            if (amplitude < 0) throw new ArgumentOutOfRangeException(nameof(amplitude));
            if (firstMoonSide != -1 && firstMoonSide != 1) throw new ArgumentOutOfRangeException(nameof(firstMoonSide));
            if (projectile < BraidedProjectile.Center || projectile > BraidedProjectile.SecondMoon)
                throw new ArgumentOutOfRangeException(nameof(projectile));
            int lateral = 0;
            if (projectile != BraidedProjectile.Center && step > 0 && step % 2 == 0)
            {
                int side = (step / 2) % 2 == 1 ? firstMoonSide : -firstMoonSide;
                if (projectile == BraidedProjectile.SecondMoon) side = -side;
                lateral = checked(side * amplitude);
            }
            // Rotate the grid heading by 90 degrees. A diagonal keeps its own
            // lateral path; it never collapses into the central target's tile.
            return new ProjectileGridPoint(
                checked(origin.X + forward.X * step - forward.Y * lateral),
                checked(origin.Y + forward.Y * step + forward.X * lateral));
        }

        public static IReadOnlyList<ProjectileCellCrossing> StepCrossings(ProjectileGridPoint origin,
            ProjectileGridPoint forward, int step, BraidedProjectile projectile, int amplitude, int firstMoonSide)
        {
            if (step < 1) throw new ArgumentOutOfRangeException(nameof(step));
            return TraceSegment(Node(origin, forward, step - 1, projectile, amplitude, firstMoonSide),
                Node(origin, forward, step, projectile, amplitude, firstMoonSide));
        }

        // Cell centers have integer coordinates; boundaries lie at half integers.
        // Exact integer comparisons avoid float/tick-dependent skipped cells.
        // Includes the starting cell and labels corner-only contacts separately.
        public static IReadOnlyList<ProjectileCellCrossing> TraceSegment(ProjectileGridPoint start, ProjectileGridPoint end)
        {
            long dx = Math.Abs((long)end.X - start.X), dy = Math.Abs((long)end.Y - start.Y);
            // A game board is tiny. Reject unbounded caller input before allocating
            // or multiplying; this is a technical resource limit, not attack range.
            if (dx > 65536 || dy > 65536) throw new ArgumentOutOfRangeException(nameof(end));
            int sx = Math.Sign((long)end.X - start.X), sy = Math.Sign((long)end.Y - start.Y);
            long ix = 0, iy = 0;
            int x = start.X, y = start.Y;
            var cells = new List<ProjectileCellCrossing> { new ProjectileCellCrossing(start, false) };
            while (ix < dx || iy < dy)
            {
                long xBoundary = (2 * ix + 1) * dy;
                long yBoundary = (2 * iy + 1) * dx;
                if (ix < dx && (iy == dy || xBoundary < yBoundary))
                { x += sx; ix++; }
                else if (iy < dy && (ix == dx || yBoundary < xBoundary))
                { y += sy; iy++; }
                else
                {
                    cells.Add(new ProjectileCellCrossing(new ProjectileGridPoint(x + sx, y), true));
                    cells.Add(new ProjectileCellCrossing(new ProjectileGridPoint(x, y + sy), true));
                    x += sx; y += sy; ix++; iy++;
                }
                cells.Add(new ProjectileCellCrossing(new ProjectileGridPoint(x, y), false));
            }
            return cells;
        }

        static void ValidateHeading(ProjectileGridPoint forward)
        {
            if (forward.X < -1 || forward.X > 1 || forward.Y < -1 || forward.Y > 1 ||
                forward.X == 0 && forward.Y == 0)
                throw new ArgumentException("A heading must be one of the eight neighboring grid directions.", nameof(forward));
        }
    }
}
