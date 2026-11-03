using System;
using System.Collections.Generic;

namespace MonsterPouch.Gameplay.Match
{
    // Implementations must inspect the CURRENT occupant, never a reservation or launch target.
    // Identity must remain stable for one combat. Null means no eligible enemy in that cell.
    public interface IMartioOccupancy
    {
        string EnemyOccupying(ProjectileGridPoint cell);
    }

    public enum MartioDamageKind { Impact, AdjacentExplosion }

    public enum MartioFlightEventKind { Impact, RangeEnd, BoardExit }

    public readonly struct MartioFlightEvent
    {
        public readonly BraidedProjectile Projectile;
        public readonly MartioFlightEventKind Kind;
        public readonly MartioFlightPoint Position;
        public readonly ProjectileGridPoint Cell;
        public readonly double Progress;
        public readonly bool StopsProjectile;
        public MartioFlightEvent(BraidedProjectile projectile, MartioFlightEventKind kind,
            MartioFlightPoint position, ProjectileGridPoint cell, double progress, bool stopsProjectile)
        { Projectile = projectile; Kind = kind; Position = position; Cell = cell;
          Progress = progress; StopsProjectile = stopsProjectile; }
    }

    public readonly struct MartioDamageEvent
    {
        public readonly BraidedProjectile Projectile;
        public readonly MartioDamageKind Kind;
        public readonly ProjectileGridPoint ImpactCell, RecipientCell;
        public readonly string EnemyId;
        public readonly int Damage;
        public readonly double Progress;
        public MartioDamageEvent(BraidedProjectile projectile, MartioDamageKind kind,
            ProjectileGridPoint impactCell, ProjectileGridPoint recipientCell, string enemyId, int damage, double progress)
        { Projectile = projectile; Kind = kind; ImpactCell = impactCell; RecipientCell = recipientCell;
          EnemyId = enemyId; Damage = damage; Progress = progress; }
    }

    public readonly struct MartioFlightPoint
    {
        public readonly double X, Y;
        public MartioFlightPoint(double x, double y) { X = x; Y = y; }
    }

    // One isolated volley. No turn scheduler, UnitDefinition, purchase or roster integration.
    // Advance only at authoritative combat ticks after movement has established real occupancy.
    public sealed class MartioProjectileSimulation
    {
        sealed class Lane
        {
            public BraidedProjectile Projectile;
            public ProjectileGridPoint Cell;
            public bool HasCell, Stopped;
            public double StoppedAt, EndAt;
            public string ContactEnemy;
            public int ImpactDamage, ExplosionDamage;
            public readonly HashSet<string> HitEnemies = new HashSet<string>(StringComparer.Ordinal);
        }
        sealed class Entry
        {
            public int Lane;
            public ProjectileGridPoint Cell;
            public double Progress;
            public bool Corner;
            public bool Terminal;
            public MartioFlightEventKind Kind;
        }

        readonly ProjectileGridPoint origin, forward;
        readonly int range;
        readonly double speed;
        readonly bool explodes, pierces;
        readonly Func<ProjectileGridPoint,IEnumerable<ProjectileGridPoint>> explosionCells;
        readonly MartioRepeatHitPolicy repeats;
        readonly Lane[] lanes = new Lane[3];
        readonly List<Entry> entries = new List<Entry>();
        int nextEntry;
        bool advancing;
        public double Progress { get; private set; }
        public bool Complete { get; private set; }
        public int Range => range;

        public MartioProjectileSimulation(MartioPrototypeConfig damage, ProjectileGridPoint origin,
            ProjectileGridPoint forward, int range, double forwardStepsPerSecond, bool explodes, bool pierces,
            bool upgradedMoons, MartioRepeatHitPolicy repeatPolicy, MartioCornerPolicy cornerPolicy,
            MartioFlightBounds? bounds = null, Func<ProjectileGridPoint,IEnumerable<ProjectileGridPoint>> explosionCells = null)
        {
            if (damage == null) throw new ArgumentNullException(nameof(damage));
            damage.ValidateDamage();
            // Technical allocation ceiling only; no implicit gameplay range is assigned.
            if (range < 1 || range > 4096) throw new ArgumentOutOfRangeException(nameof(range));
            if (double.IsNaN(forwardStepsPerSecond) || double.IsInfinity(forwardStepsPerSecond) || forwardStepsPerSecond <= 0)
                throw new ArgumentOutOfRangeException(nameof(forwardStepsPerSecond));
            if (cornerPolicy != MartioCornerPolicy.InteriorOnly && cornerPolicy != MartioCornerPolicy.IncludeCornerTouches)
                throw new InvalidOperationException("La política de esquinas debe ser explícita; sigue pendiente de aprobación.");
            if (pierces && repeatPolicy != MartioRepeatHitPolicy.OncePerEnemy)
                throw new InvalidOperationException("Atravesar requiere la regla aprobada: un impacto directo por enemigo y proyectil.");
            this.origin = origin; this.forward = forward; this.range = range;
            this.explosionCells = explosionCells;
            if (bounds.HasValue && !bounds.Value.Contains(origin)) throw new ArgumentOutOfRangeException(nameof(origin));
            speed = forwardStepsPerSecond; this.explodes = explodes; this.pierces = pierces; repeats = repeatPolicy;
            for (int i = 0; i < lanes.Length; i++)
            {
                var projectile = (BraidedProjectile)i;
                lanes[i] = new Lane { Projectile = projectile,
                    ImpactDamage = damage.ImpactDamage(projectile, upgradedMoons), ExplosionDamage = damage.ExplosionDamage(projectile) };
                ProjectileGridPoint? previous = null;
                double terminalProgress = range;
                var terminalKind = MartioFlightEventKind.RangeEnd;
                for (int step = 1; step <= range; step++)
                {
                    var a = MartioFlightGeometry.Node(origin, forward, step - 1, projectile);
                    var b = MartioFlightGeometry.Node(origin, forward, step, projectile);
                    double fraction = bounds.HasValue ? bounds.Value.ExitFraction(a, b) : 1;
                    bool exited = fraction < 1;
                    if (exited) {
                        b = new MartioFlightPoint(a.X + (b.X - a.X) * fraction, a.Y + (b.Y - a.Y) * fraction);
                        terminalProgress = step - 1 + fraction;
                        terminalKind = MartioFlightEventKind.BoardExit;
                    }
                    foreach (var crossing in MartioFlightGeometry.TraceSegment(a, b))
                    {
                        if (bounds.HasValue && !bounds.Value.Contains(crossing.Cell)) continue;
                        if (crossing.CornerOnly && cornerPolicy == MartioCornerPolicy.InteriorOnly) continue;
                        if (!crossing.CornerOnly && previous.HasValue && previous.Value.Equals(crossing.Cell)) continue;
                        entries.Add(new Entry { Lane = i, Cell = crossing.Cell, Corner = crossing.CornerOnly, Progress = step - 1 + crossing.Fraction * fraction });
                        if (!crossing.CornerOnly) previous = crossing.Cell;
                    }
                    if (exited) break;
                }
                entries.Add(new Entry { Lane = i, Terminal = true, Progress = terminalProgress, Kind = terminalKind });
                lanes[i].EndAt = terminalProgress;
            }
            entries.Sort((a, b) => {
                int order = a.Progress.CompareTo(b.Progress);
                if (order == 0) order = a.Lane.CompareTo(b.Lane);
                if (order == 0) order = a.Terminal.CompareTo(b.Terminal);
                if (order == 0) order = b.Corner.CompareTo(a.Corner);
                if (order == 0) order = a.Cell.Y.CompareTo(b.Cell.Y);
                return order != 0 ? order : a.Cell.X.CompareTo(b.Cell.X);
            });
        }

        public bool IsActive(BraidedProjectile projectile) => !Complete && !GetLane(projectile).Stopped;
        Lane GetLane(BraidedProjectile projectile)
        {
            if (projectile < BraidedProjectile.Center || projectile > BraidedProjectile.SecondMoon)
                throw new ArgumentOutOfRangeException(nameof(projectile));
            return lanes[(int)projectile];
        }
        public MartioFlightPoint Position(BraidedProjectile projectile)
            => PositionAt(projectile, Progress);

        // Rendering may interpolate within completed simulation travel, never predict ahead.
        public MartioFlightPoint PositionAt(BraidedProjectile projectile, double progress)
        {
            var lane = GetLane(projectile);
            double p = Math.Max(0, Math.Min(Progress, progress));
            if (lane.Stopped) p = Math.Min(p, lane.StoppedAt);
            return PathPosition(projectile, p);
        }

        MartioFlightPoint PathPosition(BraidedProjectile projectile, double p)
        {
            int step = Math.Min(range - 1, (int)Math.Floor(p));
            double t = p - step;
            var a = MartioFlightGeometry.Node(origin, forward, step, projectile);
            var b = MartioFlightGeometry.Node(origin, forward, step + 1, projectile);
            return new MartioFlightPoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }

        public void Advance(double deltaSeconds, IMartioOccupancy occupancy, Action<MartioDamageEvent> applyDamage,
            Action<MartioFlightEvent> flightEvent = null)
        {
            if (occupancy == null) throw new ArgumentNullException(nameof(occupancy));
            if (applyDamage == null) throw new ArgumentNullException(nameof(applyDamage));
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (advancing) throw new InvalidOperationException("No recursive advancement during an impact.");
            if (Complete || deltaSeconds == 0) return;
            advancing = true;
            try
            {
                double destination = Math.Min(range, Progress + deltaSeconds * speed);
                // Detect an enemy entering/leaving the cell while a slow projectile stays there.
                foreach (var lane in lanes)
                    if (lane.HasCell && !lane.Stopped) Contact(lane, lane.Cell, Progress, false, occupancy, applyDamage, flightEvent);
                while (nextEntry < entries.Count && entries[nextEntry].Progress <= destination + 1e-10)
                {
                    var entry = entries[nextEntry++];
                    var lane = lanes[entry.Lane];
                    if (lane.Stopped) continue;
                    if (entry.Terminal) {
                        lane.Stopped = true; lane.StoppedAt = entry.Progress;
                        flightEvent?.Invoke(new MartioFlightEvent(lane.Projectile, entry.Kind,
                            PathPosition(lane.Projectile, entry.Progress), lane.Cell, entry.Progress, true));
                        continue;
                    }
                    if (!entry.Corner)
                    {
                        if (!lane.HasCell || !lane.Cell.Equals(entry.Cell)) lane.ContactEnemy = null;
                        lane.Cell = entry.Cell; lane.HasCell = true;
                    }
                    Contact(lane, entry.Cell, entry.Progress, entry.Corner, occupancy, applyDamage, flightEvent);
                }
                Progress = destination;
                Complete = Progress >= range || (lanes[0].Stopped && lanes[1].Stopped && lanes[2].Stopped);
            }
            finally { advancing = false; }
        }

        void Contact(Lane lane, ProjectileGridPoint cell, double progress, bool corner,
            IMartioOccupancy occupancy, Action<MartioDamageEvent> applyDamage, Action<MartioFlightEvent> flightEvent)
        {
            string enemy = occupancy.EnemyOccupying(cell);
            bool sameContact = !corner && lane.ContactEnemy == enemy;
            if (!corner) lane.ContactEnemy = enemy;
            if (string.IsNullOrEmpty(enemy) || sameContact) return;
            if (repeats == MartioRepeatHitPolicy.OncePerEnemy && lane.HitEnemies.Contains(enemy)) return;
            lane.HitEnemies.Add(enemy);
            // Mark before callbacks; each of the three projectiles stops independently.
            bool terminalImpact = !pierces || Math.Abs(progress - lane.EndAt) <= 1e-10;
            if (terminalImpact) { lane.Stopped = true; lane.StoppedAt = progress; }
            flightEvent?.Invoke(new MartioFlightEvent(lane.Projectile, MartioFlightEventKind.Impact,
                PathPosition(lane.Projectile, progress), cell, progress, terminalImpact));
            applyDamage(new MartioDamageEvent(lane.Projectile, MartioDamageKind.Impact, cell, cell, enemy, lane.ImpactDamage, progress));
            if (!explodes) return;
            if (explosionCells != null)
            {
                var hit = new HashSet<string>(StringComparer.Ordinal) { enemy };
                foreach (var adjacent in explosionCells(cell))
                {
                    if (adjacent.X==cell.X && adjacent.Y==cell.Y) continue;
                    string neighbor=occupancy.EnemyOccupying(adjacent);
                    if (!string.IsNullOrEmpty(neighbor) && hit.Add(neighbor))
                        applyDamage(new MartioDamageEvent(lane.Projectile,MartioDamageKind.AdjacentExplosion,
                            cell,adjacent,neighbor,lane.ExplosionDamage,progress));
                }
                return;
            }
            // Approved 2026-10-25: the central cell receives ONLY impact damage.
            for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0) continue;
                var adjacent = new ProjectileGridPoint(cell.X + x, cell.Y + y);
                string neighbor = occupancy.EnemyOccupying(adjacent);
                if (!string.IsNullOrEmpty(neighbor))
                    applyDamage(new MartioDamageEvent(lane.Projectile, MartioDamageKind.AdjacentExplosion,
                        cell, adjacent, neighbor, lane.ExplosionDamage, progress));
            }
        }
    }
}
