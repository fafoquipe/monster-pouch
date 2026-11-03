using System;
using System.Collections.Generic;
using System.Linq;

namespace MonsterPouch.Gameplay.Match
{
    public static class EronyaSelection
    {
        public static IOrderedEnumerable<T> Nearest<T>(IEnumerable<T> candidates,Func<T,int> distance,
            Func<T,decimal> fraction,Func<T,int> slot,Func<T,string> identity) =>
            candidates.OrderBy(distance).ThenBy(fraction).ThenBy(slot).ThenBy(identity,StringComparer.Ordinal);
        public static T MostInjured<T>(IEnumerable<T> candidates,Func<T,int> distance,
            Func<T,decimal> fraction,Func<T,int> slot,Func<T,string> identity) =>
            candidates.OrderBy(fraction).ThenBy(distance).ThenBy(slot).ThenBy(identity,StringComparer.Ordinal).FirstOrDefault();
        public static T HighestBasicDamage<T>(IEnumerable<T> candidates,Func<T,decimal> damage,
            Func<T,int> distance,Func<T,decimal> fraction,Func<T,int> slot,Func<T,string> identity) =>
            candidates.OrderByDescending(damage).ThenBy(distance).ThenBy(fraction).ThenBy(slot)
                .ThenBy(identity,StringComparer.Ordinal).FirstOrDefault();
        public static decimal RawBasicDamage(decimal attack,decimal flatBonus,decimal multiplier,
            decimal timedFlatBonus,decimal timedMultiplier,bool timedActive) =>
            Math.Max(0m,(attack+flatBonus+(timedActive?timedFlatBonus:0m))*
                (multiplier+(timedActive?timedMultiplier-1m:0m)));
        public static int OwnerRow(int row,int height,bool rotated) => rotated?height-1-row:row;
        public static int OwnerColumn(int column,int width,bool rotated) => rotated?width-1-column:column;
    }
}
