using HarmonyLib;

namespace ZoominoesArchipelago.Patches;

/// Strips the "Unlocked!" banner from the stinger when we borrow it for a toast.
///
/// ItemToast reuses UnlockStinger because it is the game's own arrival animation, but
/// the prefab's `tmp Unlock Text` child is a large fixed banner reading
/// unlockview.title. It is true of a collection unlock and wrong of an item being
/// sent to another player, and it crowds out the line that does carry the message.
///
/// Hidden per instance rather than by overriding unlockview.title, which the
/// collection's own UnlockView shares.
[HarmonyPatch(typeof(UnlockStinger), nameof(UnlockStinger.Show))]
public static class ToastStingerPatch
{
    private const string BannerChild = "tmp Unlock Text";

    public static void Postfix(UnlockStinger __instance, EntityData entityData)
    {
        if (!ApEntityFactory.IsApId(entityData?.id)) return;

        var banner = __instance.transform.Find(BannerChild);
        if (banner != null)
        {
            banner.gameObject.SetActive(false);
            return;
        }

        Plugin.Logger.LogWarning(
            $"Stinger has no '{BannerChild}' child — the banner will show on toasts");
    }
}
