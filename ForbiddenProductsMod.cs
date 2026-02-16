using System.Reflection;
using HarmonyLib;
using MelonLoader;

[assembly: MelonInfo(typeof(ForbiddenProducts.ForbiddenProductsMod), "Forbidden Products", "0.1.0", "MiKiBa")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ForbiddenProducts;

public sealed class ForbiddenProductsMod : MelonMod
{
	internal const string HarmonyId = "forbiddenproducts";

	public override void OnInitializeMelon()
	{
		ForbiddenProductsConfig.LoadOrCreateDefault();

		var harmony = new HarmonyLib.Harmony(HarmonyId);
		harmony.PatchAll(Assembly.GetExecutingAssembly());

		MelonLogger.Msg($"ForbiddenProducts enabled (configured customers: {ForbiddenProductsConfig.ConfiguredCustomersCount}).");
		if (ForbiddenProductsConfig.DebugEnabled)
		{
			MelonLogger.Warning($"ForbiddenProducts: debug enabled via '{ForbiddenProductsConfig.DebugFlagPath}'.");
		}
	}
}
