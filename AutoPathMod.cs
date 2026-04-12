using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace AutoPath;

[ModInitializer(nameof(Initialize))]
public static class AutoPathMod
{
    internal static Harmony? Harmony;

    public static void Initialize()
    {
        Harmony = new Harmony("com.jadistanbelly.autopath");
        Harmony.PatchAll(typeof(AutoPathMod).Assembly);
    }
}
