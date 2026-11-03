using System.Collections;
using System.IO;
using System.Linq;
using MonsterPouch.Local;
using UnityEngine;
using Object=UnityEngine.Object;
public static class MonsterPouchRevisionReview
{
 public static string Cabinets()
 {
  var ui=Object.FindFirstObjectByType<LocalGameUI>();
  if(!Application.isPlaying||ui==null)return "Enter Play Mode first";
  ui.StartCoroutine(CabinetShots(ui));return "Capturing the three cabinet headers";
 }
 static IEnumerator CabinetShots(LocalGameUI ui)
 {
  var lobby=CardTableTheme.Lobby;bool enabled=ui.Match.enabled,spanish=GameText.Spanish;
  ui.Match.enabled=false;GameText.SetLanguage(false);ui.Match.Menu();
  try {
   MonsterPouchLayoutPreview.Resize("1080x2340");
   foreach(var style in new[]{TableStyle.Default,TableStyle.CampoReal,TableStyle.GrandCasaHotel}) {
    ui.SelectLobbyStyle(style);yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.3f);
    ui.CloseModal();ui.OpenDecks();yield return Shot(CardTableTheme.MapId(style)+"-brief");
    if(style==TableStyle.GrandCasaHotel){ui.ShowLobbySkins();yield return Shot("lounge-lobby-selector");}
   }
  } finally {GameText.SetLanguage(spanish);CardTableTheme.SelectLobby(lobby);ui.Match.enabled=enabled;ui.CloseModal();ui.OpenDecks();}
  MonsterPouchLocalBridge.Reply("revision-cabinets","3 cabinet headers and lobby selector captured");
 }
 public static string Capture()
 {
  var ui=Object.FindFirstObjectByType<LocalGameUI>();
  if(!Application.isPlaying||ui==null)return "Enter Play Mode first";
  ui.StartCoroutine(Shots(ui));return "Capturing the revised scenes";
 }
 static IEnumerator Shot(string name)
 {
  yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();
  string folder=Path.GetFullPath("Logs/local-validation/revision-20261015");Directory.CreateDirectory(folder);
  ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));yield return new WaitForSecondsRealtime(.25f);
 }
 static IEnumerator Shots(LocalGameUI ui)
 {
  var map=CardTableTheme.Current;var lobby=CardTableTheme.Lobby;
  bool enabled=ui.Match.enabled;ui.Match.enabled=false;
  GameText.SetLanguage(false);
  try {
   MonsterPouchLayoutPreview.Resize("1080x2340");yield return new WaitForSecondsRealtime(.5f);
   foreach(var style in new[]{TableStyle.Default,TableStyle.GrandCasaHotel,TableStyle.CampoReal}) {
    ui.Match.Menu();ui.SelectLobbyStyle(style);yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.2f);
    ui.CloseModal();ui.OpenDecks();yield return Shot(CardTableTheme.MapId(style)+"-brief");
    ui.OpenHome();yield return Shot(CardTableTheme.MapId(style)+"-home");
    ui.ShowMapSkins();yield return Shot(CardTableTheme.MapId(style)+"-styles");
    ui.SelectTableSkin(style);yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.2f);
    MonsterPouchLayoutPreview.Preparation();ui.Match.enabled=false;yield return Shot(CardTableTheme.MapId(style)+"-map");
   }
   MonsterPouchLayoutPreview.Resize("1920x1200");yield return new WaitForSecondsRealtime(.5f);
   ui.Match.Menu();ui.SelectTableSkin(TableStyle.GrandCasaHotel);yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.2f);
   MonsterPouchLayoutPreview.Preparation();ui.Match.enabled=false;yield return Shot("lounge-wide");
   ui.Match.Menu();GameText.SetLanguage(true);ui.ShowMapSkins();yield return Shot("styles-spanish");
  }
  finally {
   GameText.SetLanguage(false);CardTableTheme.Select(map);CardTableTheme.SelectLobby(lobby);
   ui.Match.Menu();ui.Match.enabled=enabled;ui.CloseModal();ui.OpenHome();
   MonsterPouchLayoutPreview.Resize("1080x2340");
  }
  MonsterPouchLocalBridge.Reply("revision-captures","14 screenshots complete; map/lobby restored; language English");
 }
}

