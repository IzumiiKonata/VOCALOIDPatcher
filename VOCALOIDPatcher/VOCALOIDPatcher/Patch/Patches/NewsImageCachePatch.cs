using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using HarmonyLib;
using Yamaha.VOCALOID;

namespace VOCALOIDPatcher.Patch.Patches;

#if !NET6_0
public class NewsImageCachePatch : PatchBase
{
    public override string PatchName        => "NewsImageCachePatch";
    public override Type   TargetClass      => typeof(NewsPresenter);
    public override string TargetMethodName => "SetPage";
    public override Type[] ArgumentTypes    => new[] { typeof(int) };

    private static readonly AccessTools.FieldRef<NewsPresenter, Dictionary<string, BitmapImage>>?
        Cache = CreateCacheRef();

    [HarmonyPostfix]
    private static void Postfix(NewsPresenter __instance, ref Task<List<NewsDetailViewModel>> __result)
    {
        if (Cache != null)
            __result = PopulateAsync(__instance, __result);
    }

    private static async Task<List<NewsDetailViewModel>> PopulateAsync(
        NewsPresenter presenter,
        Task<List<NewsDetailViewModel>> pending)
    {
        var pages = await pending.ConfigureAwait(true);
        var cache = Cache!(presenter);

        foreach (var page in pages)
        {
            int count = Math.Min(page.Detail.Images.Count, page.Images.Count);
            for (int i = 0; i < count; i++)
            {
                string key = page.Detail.Images[i];
                var image = page.Images[i];
                if (!cache.ContainsKey(key))
                {
                    if (image.CanFreeze)
                        image.Freeze();
                    cache[key] = image;
                }
            }
        }

        while (cache.Count > 64)
        {
            using var enumerator = cache.Keys.GetEnumerator();
            if (!enumerator.MoveNext())
                break;
            cache.Remove(enumerator.Current);
        }

        return pages;
    }

    private static AccessTools.FieldRef<NewsPresenter, Dictionary<string, BitmapImage>>? CreateCacheRef()
    {
        try
        {
            return AccessTools.FieldRefAccess<NewsPresenter, Dictionary<string, BitmapImage>>("imageCaches");
        }
        catch
        {
            return null;
        }
    }
}
#endif
