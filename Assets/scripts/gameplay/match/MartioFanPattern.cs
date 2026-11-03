using System;
using System.Collections.Generic;

namespace MonsterPouch.Gameplay.Match
{
    public readonly struct MartioFanGroup
    {
        public readonly int Side, Range;
        public readonly ProjectileGridPoint Heading;
        public MartioFanGroup(int side, ProjectileGridPoint heading, int range)
        { Side = side; Heading = heading; Range = range; }
    }

    public static class MartioFanPattern
    {
        public const string RulesSignature = "martio-fan-v1-q1e6-q65536-interior-terminal-v1";
        // Baked sin(0..90 degrees), rounded to millionths. No platform trig in combat.
        static readonly int[] Sin = { 0, 17452, 34899, 52336, 69756, 87156, 104528, 121869, 139173, 156434, 173648, 190809, 207912, 224951, 241922, 258819, 275637, 292372, 309017, 325568, 342020, 358368, 374607, 390731, 406737, 422618, 438371, 453990, 469472, 484810, 500000, 515038, 529919, 544639, 559193, 573576, 587785, 601815, 615661, 629320, 642788, 656059, 669131, 681998, 694658, 707107, 719340, 731354, 743145, 754710, 766044, 777146, 788011, 798636, 809017, 819152, 829038, 838671, 848048, 857167, 866025, 874620, 882948, 891007, 898794, 906308, 913545, 920505, 927184, 933580, 939693, 945519, 951057, 956305, 961262, 965926, 970296, 974370, 978148, 981627, 984808, 987688, 990268, 992546, 994522, 996195, 997564, 998630, 999391, 999848, 1000000 };

        public static IReadOnlyList<MartioFanGroup> Groups(ProjectileGridPoint heading,
            int centralRange, bool fan, int lateralRange = 3, int angleDegrees = 30)
        {
            var result = new List<MartioFanGroup> { new MartioFanGroup(0, heading, centralRange) };
            if (fan) {
                lateralRange = (int)MartioBalanceValues.Normalize("martio-moons", "lateralTravelRange", lateralRange);
                angleDegrees = (int)MartioBalanceValues.Normalize("martio-moons", "fanAngleDegrees", angleDegrees);
                result.Add(new MartioFanGroup(-1, Rotate(heading, -angleDegrees), lateralRange));
                result.Add(new MartioFanGroup(1, Rotate(heading, angleDegrees), lateralRange));
            }
            return result.AsReadOnly();
        }

        public static ProjectileGridPoint Rotate(ProjectileGridPoint heading, int degrees)
        {
            long scale = Math.Max(Math.Abs((long)heading.X), Math.Abs((long)heading.Y));
            if (scale == 0 || scale > 65536) throw new ArgumentOutOfRangeException(nameof(heading));
            if (degrees < -89 || degrees > 89) throw new ArgumentOutOfRangeException(nameof(degrees));
            if (degrees == 0) return heading;
            int sine = Sin[Math.Abs(degrees)] * Math.Sign(degrees), cosine = Sin[90 - Math.Abs(degrees)];
            long x = (long)heading.X * cosine - (long)heading.Y * sine;
            long y = (long)heading.X * sine + (long)heading.Y * cosine;
            long maximum = Math.Max(Math.Abs(x), Math.Abs(y));
            // Quantize only the heading, then keep the established Chebyshev travel basis.
            return new ProjectileGridPoint(RoundRatio(x * 65536, maximum), RoundRatio(y * 65536, maximum));
        }

        static int RoundRatio(long numerator, long denominator)
        {
            long absolute = Math.Abs(numerator), result = absolute / denominator;
            long remainder = absolute % denominator;
            if (2 * remainder > denominator || (2 * remainder == denominator && (result & 1) != 0)) result++;
            return checked((int)(Math.Sign(numerator) * result));
        }
    }

    // Tile centers are integers; the visible board terminates half a tile outside them.
    public readonly struct MartioFlightBounds
    {
        public readonly int Width, Height;
        public MartioFlightBounds(int width, int height)
        {
            if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width));
            Width = width; Height = height;
        }
        public bool Contains(ProjectileGridPoint cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;
        public bool Contains(MartioFlightPoint p) => p.X >= -.5 && p.Y >= -.5 && p.X <= Width - .5 && p.Y <= Height - .5;
        public double ExitFraction(MartioFlightPoint a, MartioFlightPoint b)
        {
            if (!Contains(a)) return 0;
            double fraction = 1;
            if (b.X < -.5) fraction = Math.Min(fraction, (-.5 - a.X) / (b.X - a.X));
            if (b.X > Width - .5) fraction = Math.Min(fraction, (Width - .5 - a.X) / (b.X - a.X));
            if (b.Y < -.5) fraction = Math.Min(fraction, (-.5 - a.Y) / (b.Y - a.Y));
            if (b.Y > Height - .5) fraction = Math.Min(fraction, (Height - .5 - a.Y) / (b.Y - a.Y));
            return Math.Max(0, fraction);
        }
    }
}
