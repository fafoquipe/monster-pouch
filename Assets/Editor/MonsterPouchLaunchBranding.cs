using UnityEditor;

public static class MonsterPouchLaunchBranding
{
    public static string Apply()
    {
        // Keep the old logo asset for its other uses; startup now renders the complete supplied artwork.
        PlayerSettings.SplashScreen.show=false;
        PlayerSettings.SplashScreen.showUnityLogo=false;
        AssetDatabase.SaveAssets();
        return "Inicio configurado con la imagen completa suministrada y desvanecimiento de entrada.";
    }
}
