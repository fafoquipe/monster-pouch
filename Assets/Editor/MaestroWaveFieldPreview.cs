using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;

// Samples the same width, travel, swept body and two-view art as the running match.
[InitializeOnLoad]
public static class MaestroWaveFieldPreview
{
    static bool open=true,playing=true,red,flipped;
    static float progress;
    static double lastDraw,started=EditorApplication.timeSinceStartup,nextRepaint;
    static MaestroWaveFieldPreview()
    {
        EditorApplication.update+=()=>
        {
            double now=EditorApplication.timeSinceStartup;
            if(now<nextRepaint||!playing||!open||now-lastDraw>.5)return;
            nextRepaint=now+1.0/30;UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        };
    }
    public static void Draw(CharacterAbility ability)
    {
        if(ability?.Definition?.EffectId!=MaestroWaveFlight.EffectId)return;
        lastDraw=EditorApplication.timeSinceStartup;
        open=EditorGUILayout.Foldout(open,BalanceText.L("See wave on the field","Ver ola en el campo"),true);
        if(!open)return;
        int width=Mathf.RoundToInt(AbilityBalance.Value(ability.Definition,"width",3));
        int length=Mathf.RoundToInt(AbilityBalance.Value(ability.Definition,"length",MaestroWaveFlight.DefaultLength));
        float speed=AbilityBalance.Value(ability.Definition,"waveSpeed",MaestroWaveFlight.DefaultSpeed);
        var wave=new MaestroWaveFlight(2,red?1:8,red?1:-1,width,length,speed);
        float duration=wave.TravelSeconds+MaestroWaveFlight.CollapseSeconds;
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button(playing?BalanceText.L("Pause","Pausar"):BalanceText.L("Play","Reproducir")))
        {playing=!playing;started=lastDraw-progress*duration;}
        red=GUILayout.Toggle(red,BalanceText.L("Red side","Lado rojo"),"Button");
        flipped=GUILayout.Toggle(flipped,BalanceText.L("Flip board","Girar tablero"),"Button");
        EditorGUILayout.EndHorizontal();
        if(playing)progress=Mathf.Repeat((float)(lastDraw-started),duration+.4f)/duration;
        EditorGUI.BeginChangeCheck();
        float scrub=EditorGUILayout.Slider(BalanceText.L("Travel / collapse","Recorrido / desvanecimiento"),Mathf.Clamp01(progress),0,1);
        if(EditorGUI.EndChangeCheck()){progress=scrub;playing=false;}
        var field=GUILayoutUtility.GetRect(140,210,GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(field,new Color(.12f,.18f,.20f));
        float tile=Mathf.Min(field.width/(BoardManager.Width+2),field.height/(BoardManager.Height+1));
        float ox=field.center.x-BoardManager.Width*.5f*tile,oy=field.y+.5f*tile;
        for(int x=0;x<BoardManager.Width;x++)for(int y=0;y<BoardManager.Height;y++)
            EditorGUI.DrawRect(new Rect(ox+x*tile,oy+y*tile,tile-1,tile-1),new Color(.22f,.29f,.30f));
        var sprite=Resources.Load<Sprite>(MaestroWaveArt.Resource);
        if(sprite==null){EditorGUI.LabelField(field,BalanceText.L("Wave art is missing","Falta el arte de la ola"));return;}
        var previous=GUI.color;
        for(int x=wave.MinX;x<=wave.MaxX;x++)
        {
            wave.SampleColumn(x,Mathf.Min(progress,1)*duration,out var b,out var section,out float alpha);
            Rect uv=MaestroWaveArt.Uv(section,wave.Forward,flipped,!flipped);
            // GUI coordinates are sorted after board flip; invert its UV axes back for DrawTexture.
            if(flipped)b=new Rect(BoardManager.Width-1-b.xMax,BoardManager.Height-1-b.yMax,b.width,b.height);
            if(uv.width<0)uv=new Rect(uv.x+uv.width,uv.y,-uv.width,uv.height);
            if(uv.height<0)uv=new Rect(uv.x,uv.y+uv.height,uv.width,-uv.height);
            GUI.color=new Color(1,1,1,alpha);
            GUI.DrawTextureWithTexCoords(new Rect(ox+(b.xMin+.5f)*tile,oy+(b.yMin+.5f)*tile,b.width*tile,b.height*tile),sprite.texture,uv,true);
        }
        GUI.color=previous;
        EditorGUILayout.LabelField(BalanceText.L("Travel time","Tiempo de recorrido")+" · "+wave.TravelSeconds.ToString("0.0")+" s");
        EditorGUILayout.HelpBox(BalanceText.L("Width and length are independent. The board edge clips the wave without changing the saved width.",
            "Ancho y largo son independientes. El borde recorta la ola sin cambiar el ancho guardado."),MessageType.None);
    }
}
