using System.Collections;
using System.Linq;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
public sealed class StyleRevisionPlayTests
{
 [UnityTest]public IEnumerator MapAndLobbyButtonsPersistIndependentlyAndHotelRemainsVisible()
 {
  string[] keys={CardTableTheme.PreferenceKey,CardTableTheme.LobbyPreferenceKey,"monster-pouch.language"};
  var existed=keys.Select(PlayerPrefs.HasKey).ToArray();var values=keys.Select(PlayerPrefs.GetString).ToArray();bool language=GameText.Spanish;
  try {
   CardTableTheme.Select(TableStyle.Default);CardTableTheme.SelectLobby(TableStyle.CampoReal);GameText.SetLanguage(false);
   yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
   var ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;ui.ShowLobbySkins();yield return null;
   ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="styles-map-tab").onClick.Invoke();yield return null;
   ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="use-style-GrandCasaHotel").onClick.Invoke();
   yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.2f);
   Assert.AreEqual(TableStyle.GrandCasaHotel,CardTableTheme.Current);Assert.AreEqual(TableStyle.CampoReal,CardTableTheme.Lobby);
   Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t=>t.text=="MAP"));
   var arena=Object.FindFirstObjectByType<CardTableArena>();Assert.IsTrue(arena.LoungeEnabled);
   var sprites=Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None);
   foreach(string name in new[]{"table-frame","table-surface","clown-tower","shuffler-cart"}) {
    var sprite=sprites.Single(s=>s.name==name);Assert.IsNotNull(sprite.sprite);Assert.IsTrue(sprite.enabled&&sprite.gameObject.activeInHierarchy,name);
   }
   var hotel=sprites.Single(s=>s.name=="shuffler-cart").transform;
   while(hotel.parent!=null&&hotel.name!="grand-cas-hotel")hotel=hotel.parent;
   Assert.AreEqual("grand-cas-hotel",hotel.name);hotel.gameObject.SetActive(false);
   arena.SetTheme(TableStyle.GrandCasaHotel);Assert.IsTrue(hotel.gameObject.activeInHierarchy);
   ui.ShowLobbySkins();yield return null;
   ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="use-style-Default").onClick.Invoke();
   yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.2f);
   Assert.AreEqual(TableStyle.GrandCasaHotel,CardTableTheme.Current);Assert.AreEqual(TableStyle.Default,CardTableTheme.Lobby);
   yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
   Assert.IsTrue(Object.FindFirstObjectByType<CardTableArena>().LoungeEnabled);Assert.AreEqual(TableStyle.Default,CardTableTheme.Lobby);
   foreach(var id in new[]{"classic","lounge","royal"}) {
    var skin=Resources.Load<LobbyFurnitureDefinition>("MonsterPouch/Styles/Furniture/"+id);Assert.AreEqual(1,skin.Saturation);
   }
   GameText.SetLanguage(true);Assert.AreEqual("MALETA",GameText.Resolve("BRIEF"));
   Assert.AreEqual("MALETA 2",GameText.Resolve("BRIEF 2"));
   Assert.AreEqual("Maleta 1",GameText.Resolve("Brief 1"));
   Assert.AreEqual("QUITAR DE LA MALETA",GameText.Resolve("REMOVE FROM BRIEF"));
   Assert.AreEqual("Necesitas una copia en la Maleta",GameText.Resolve("A copy in the Brief is required"));
   Assert.AreEqual("Espera a que se abra la Maleta.",GameText.Resolve("Wait for the Brief to open."));
   Assert.AreEqual("La Maleta se abre",GameText.Resolve("El Brief se abre"));
   Assert.AreEqual("A la Maleta",GameText.Resolve("Al Brief"));
   Assert.AreEqual("Debriefing",GameText.Resolve("Debriefing"));
   GameText.SetLanguage(false);Assert.AreEqual("MAP",GameText.Resolve("MAPA"));Assert.AreEqual("BRIEF",GameText.Resolve("MAZO"));
   Assert.AreEqual("BRIEF",GameText.Resolve("MALETA"));Assert.AreEqual("Brief 1",GameText.Resolve("Brief 1"));
  }
  finally {
   GameText.SetLanguage(language);
   for(int i=0;i<keys.Length;i++){if(existed[i])PlayerPrefs.SetString(keys[i],values[i]);else PlayerPrefs.DeleteKey(keys[i]);}
   PlayerPrefs.Save();
  }
 }
}

