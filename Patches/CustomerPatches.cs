using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine.Events;

#if IL2CPP
using ItemInstanceList = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.ItemFramework.ItemInstance>;
using ProductQtyList = Il2CppSystem.Collections.Generic.List<Il2CppSystem.Tuple<Il2CppScheduleOne.Product.ProductDefinition, int>>;
#else
using ItemInstanceList = System.Collections.Generic.List<ScheduleOne.ItemFramework.ItemInstance>;
using ProductQtyList = System.Collections.Generic.List<System.Tuple<ScheduleOne.Product.ProductDefinition, int>>;
#endif

namespace ForbiddenProducts.Patches;

internal static class CustomerPatches
{
	// Marker class for this file.
}

[HarmonyPatch(typeof(Customer), "GetOrderableProductsWithQuantities")]
internal static class Customer_GetOrderableProductsWithQuantities_Patch
{
	private static void Postfix(Customer __instance, Dealer dealer, ref ProductQtyList __result)
	{
		if (__instance == null || __result == null || __result.Count == 0)
		{
			return;
		}

		var forbidden = ForbiddenProductsService.GetForbidden(__instance);
		if (forbidden.Count == 0)
		{
			return;
		}

		// IL2CPP uses Il2CppSystem collections and delegates, so keep removal logic
		// index-based and avoid RemoveAll/predicate marshaling.
		for (var i = __result.Count - 1; i >= 0; i--)
		{
			var t = __result[i];
			if (t == null)
			{
				continue;
			}

			var def = t.Item1;
			if (def != null && ForbiddenProductsService.IsForbidden(__instance, def))
			{
				__result.RemoveAt(i);
			}
		}
	}
}

[HarmonyPatch(typeof(Customer), nameof(Customer.OfferDealItems))]
internal static class Customer_OfferDealItems_Patch
{
	private static bool Prefix(Customer __instance, ItemInstanceList items, bool offeredByPlayer, ref bool accepted)
	{
		// Let the original method handle its own guard clauses.
		if (__instance?.CurrentContract == null || items == null || items.Count == 0)
		{
			return true;
		}

		if (!ForbiddenProductsService.ContainsForbidden(__instance, items))
		{
			return true;
		}

		accepted = false;
		__instance.CustomerRejectedDeal(offeredByPlayer);
		return false;
	}
}

[HarmonyPatch(typeof(Customer), "GetSampleSuccess")]
internal static class Customer_GetSampleSuccess_Patch
{
	private static void Postfix(Customer __instance, ItemInstanceList items, float price, ref float __result)
	{
		if (__instance == null || items == null || items.Count == 0)
		{
			return;
		}

		// The server consumes items[0] in RpcLogic___ProcessSampleServerSide_3704012609.
		// Keep UI feedback aligned with that.
		if (items[0] is not ProductItemInstance pi)
		{
			return;
		}

		if (ForbiddenProductsService.IsForbidden(__instance, ForbiddenProductsService.GetProductDefinition(pi)))
		{
			__result = 0f;
		}
	}
}

[HarmonyPatch(typeof(Customer), "SampleConsumed")]
internal static class Customer_SampleConsumed_Patch
{
	private static readonly MethodInfo? SampleWasInsufficientMethod =
		AccessTools.Method(typeof(Customer), "SampleWasInsufficient");

#if IL2CPP
	private static bool Prefix(Customer __instance)
	{
		if (__instance == null)
		{
			return true;
		}

		ProductItemInstance? consumedSample = __instance.consumedSample;
		if (consumedSample == null)
		{
			return true;
		}

		var def = ForbiddenProductsService.GetProductDefinition(consumedSample);
		if (!ForbiddenProductsService.IsForbidden(__instance, def))
		{
			return true;
		}

			// Replicate the relevant parts of Customer.SampleConsumed(), but force the "insufficient" path
			// even in tutorial / guarantee-first-sample cases.
			try
			{
				// In IL2CPP the UnityAction type is not a managed Delegate, so use RemoveAllListeners instead.
				__instance.NPC?.Behaviour?.ConsumeProductBehaviour?.onConsumeDone?.RemoveAllListeners();

				__instance.NPC?.Behaviour?.GenericDialogueBehaviour?.Enable_Server();

				__instance.SampleWasInsufficient();

				// Track rejection count like the original insufficient path.
				var db = Il2CppScheduleOne.DevUtilities.NetworkSingleton<VariableDatabase>.Instance;
				var value = db.GetValue<float>("SampleRejectionCount");
			db.SetVariableValue("SampleRejectionCount", (value + 1f).ToString());
		}
		catch (Exception ex)
		{
			MelonLogger.Error($"ForbiddenProducts: failed forcing insufficient sample for '{__instance.NPC?.ID}': {ex}");
		}
		finally
		{
			__instance.consumedSample = null!;
			__instance.Invoke("EndWait", 1.5f);
		}

		return false;
	}
#else
	private static bool Prefix(Customer __instance, ref ProductItemInstance? ___consumedSample)
	{
		if (__instance == null || ___consumedSample == null)
		{
			return true;
		}

		var def = ForbiddenProductsService.GetProductDefinition(___consumedSample);
		if (!ForbiddenProductsService.IsForbidden(__instance, def))
		{
			return true;
		}

			// Replicate the relevant parts of Customer.SampleConsumed(), but force the "insufficient" path
			// even in tutorial / guarantee-first-sample cases.
			try
			{
				__instance.NPC?.Behaviour?.ConsumeProductBehaviour?.onConsumeDone?.RemoveAllListeners();

				__instance.NPC.Behaviour.GenericDialogueBehaviour.Enable_Server();

				SampleWasInsufficientMethod?.Invoke(__instance, Array.Empty<object>());

			// Track rejection count like the original insufficient path.
				var db = ScheduleOne.DevUtilities.NetworkSingleton<VariableDatabase>.Instance;
				var value = db.GetValue<float>("SampleRejectionCount");
				db.SetVariableValue("SampleRejectionCount", (value + 1f).ToString());
			}
		catch (Exception ex)
		{
			MelonLogger.Error($"ForbiddenProducts: failed forcing insufficient sample for '{__instance.NPC?.ID}': {ex}");
		}
		finally
		{
			___consumedSample = null;
			__instance.Invoke("EndWait", 1.5f);
		}

		return false;
	}
#endif
}

[HarmonyPatch(typeof(Customer), "EvaluateCounteroffer")]
internal static class Customer_EvaluateCounteroffer_Patch
{
	private static bool Prefix(Customer __instance, ProductDefinition product, int quantity, float price, ref bool __result)
	{
		if (__instance == null || product == null)
		{
			return true;
		}

		if (!ForbiddenProductsService.IsForbidden(__instance, product))
		{
			return true;
		}

		__result = false;
		return false;
	}
}

[HarmonyPatch(typeof(Customer), nameof(Customer.GetOfferSuccessChance))]
internal static class Customer_GetOfferSuccessChance_Patch
{
	private static void Postfix(Customer __instance, ItemInstanceList items, float askingPrice, ref float __result)
	{
		if (__instance == null || items == null || items.Count == 0)
		{
			return;
		}

		var before = __result;
		var contains = ForbiddenProductsService.ContainsForbidden(__instance, items);
		if (contains)
		{
			__result = 0f;
		}

		if (ForbiddenProductsConfig.ShouldDebugNpc(__instance.NPC?.ID))
		{
			MelonLogger.Msg(
				$"ForbiddenProducts[GetOfferSuccessChance]: npcId='{__instance.NPC?.ID}' askingPrice={askingPrice} " +
				$"containsForbidden={contains} result={before:0.###}->{__result:0.###} items={items.Count}");
			for (var i = 0; i < items.Count; i++)
			{
				var id = items[i]?.ID ?? "<null>";
				MelonLogger.Msg($"ForbiddenProducts[GetOfferSuccessChance]:   item[{i}] id='{id}'");
			}
		}
	}
}

[HarmonyPatch(typeof(Customer), nameof(Customer.Load))]
internal static class Customer_Load_Patch
{
#if IL2CPP
	private static void Postfix(Customer __instance, CustomerSaveData data)
	{
		if (__instance == null)
		{
			return;
		}

		ContractInfo? offeredContractInfo = __instance.offeredContractInfo;
		if (offeredContractInfo == null)
		{
			return;
		}

		if (offeredContractInfo.Products?.entries == null || offeredContractInfo.Products.entries.Count == 0)
		{
			return;
		}

		var def = Registry.GetItem<ProductDefinition>(offeredContractInfo.Products.entries[0].ProductID);
		if (def == null)
		{
			return;
		}

		if (!ForbiddenProductsService.IsForbidden(__instance, def))
		{
			return;
		}

		MelonLogger.Warning(
			$"ForbiddenProducts: clearing forbidden offered contract for npcId '{__instance.NPC?.ID}' (product '{def.ID}').");
		__instance.offeredContractInfo = null!;
	}
#else
	private static void Postfix(Customer __instance, CustomerSaveData data, ref ContractInfo? ___offeredContractInfo)
	{
		if (__instance == null || ___offeredContractInfo == null)
		{
			return;
		}

		if (___offeredContractInfo.Products?.entries == null || ___offeredContractInfo.Products.entries.Count == 0)
		{
			return;
		}

		var def = Registry.GetItem<ProductDefinition>(___offeredContractInfo.Products.entries[0].ProductID);
		if (def == null)
		{
			return;
		}

		if (!ForbiddenProductsService.IsForbidden(__instance, def))
		{
			return;
		}

		MelonLogger.Warning(
			$"ForbiddenProducts: clearing forbidden offered contract for npcId '{__instance.NPC?.ID}' (product '{def.ID}').");
		___offeredContractInfo = null;
	}
#endif
}
