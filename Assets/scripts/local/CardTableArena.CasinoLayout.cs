using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class CardTableArena
    {
        // Reference frame: (50,84)-(394,558), inner board: (96,126)-(350,517).
        // Every theme uses the same expanded 6x10 grid. Styling must not move
        // logical cell centres underneath actors or change their tile-relative size.
        Transform boardAnchor;
        Vector3 boardCenter;
        readonly Dictionary<Transform,TablePose> tablePoses=new Dictionary<Transform,TablePose>();
        readonly struct TablePose
        {
            public readonly Vector3 Position,Scale;
            public TablePose(Transform item){Position=item.position;Scale=item.localScale;}
        }

        void ApplyCasinoBoardWidth(TableStyle style)
        {
            float expansion=HorizontalExpansion;
            Vector2 cells=new Vector2(OriginalCellSize.x*expansion,OriginalCellSize.y);
            mapper.Configure(board,boardAnchor,cells,new Vector2(-(BoardManager.Width-1)*cells.x*.5f,
                -(BoardManager.Height-1)*cells.y*.5f));
            foreach(var entry in tablePoses)
            {
                if(entry.Key==null)continue;
                var pose=entry.Value;var position=pose.Position;var scale=pose.Scale;
                position.x=boardCenter.x+(position.x-boardCenter.x)*expansion;
                scale.x*=expansion;
                entry.Key.position=position;entry.Key.localScale=scale;
            }
        }
    }
}
