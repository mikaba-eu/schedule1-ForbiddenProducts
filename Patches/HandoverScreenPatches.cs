using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace ForbiddenProducts.Patches;

internal static class HandoverScreenUiHelpers
{
	public static void ApplyForbiddenUiState(HandoverScreen screen, Customer customer)
	{
		// Match the existing UX: disable Done and show an error message.
		if (screen.DoneButton != null)
		{
			screen.DoneButton.interactable = false;
		}

		if (screen.ErrorLabel != null)
		{
			screen.ErrorLabel.text = $"{customer.NPC?.FirstName ?? "Customer"} refuses this product.";
			screen.ErrorLabel.enabled = true;
		}
	}
}

[HarmonyPatch(typeof(HandoverScreenDetailPanel), nameof(HandoverScreenDetailPanel.Open))]
internal static class HandoverScreenDetailPanel_Open_Patch
{
	private const string WantsColor = "#A6E3A1";
	private const string RefusesColor = "#FF6B6B";

	private static void Postfix(HandoverScreenDetailPanel __instance, Customer customer)
	{
		if (__instance == null || customer == null)
		{
			return;
		}

		if (__instance.EffectsLabel == null)
		{
			return;
		}

		var lines = BuildWantsRefusesLines(customer);
		if (lines.Count == 0)
		{
			return;
		}

		// The panel can be reopened/reused without fully resetting, and in some environments
		// Harmony patches can be applied more than once. Make the append idempotent.
		var baseText = StripExistingWantsRefuses(__instance.EffectsLabel.text);
		if (!string.IsNullOrEmpty(baseText))
		{
			baseText += "\n\n";
		}

		__instance.EffectsLabel.text = baseText + string.Join("\n", lines);

		// The original method positions the container based on its current size. Rebuild layout after changing text.
		if (__instance.LayoutGroup != null)
		{
			__instance.LayoutGroup.CalculateLayoutInputHorizontal();
			__instance.LayoutGroup.CalculateLayoutInputVertical();
			LayoutRebuilder.ForceRebuildLayoutImmediate(__instance.LayoutGroup.GetComponent<RectTransform>());

			var fitter = __instance.LayoutGroup.GetComponent<ContentSizeFitter>();
			if (fitter != null)
			{
				fitter.SetLayoutVertical();
			}
		}

		if (__instance.Container != null)
		{
			__instance.Container.anchoredPosition = new Vector2(0f, (0f - __instance.Container.sizeDelta.y) / 2f);
		}
	}

	private static List<string> BuildWantsRefusesLines(Customer customer)
	{
		var forbidden = ForbiddenProductsService.GetForbidden(customer);

		var lines = new List<string>();

		// "Wants" (best affinities, excluding forbidden types).
		var affinities = customer.CustomerData?.DefaultAffinityData?.ProductAffinities;
		if (affinities != null && affinities.Count > 0)
		{
			// IL2CPP uses Il2CppSystem.Collections.Generic.List<T> which does not play nicely with LINQ.
			// Keep it simple and deterministic.
			var bestTypes = new EDrugType[3];
			var bestScores = new float[3] { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity };

			for (var i = 0; i < affinities.Count; i++)
			{
				var a = affinities[i];
				if (!IsImplementedDrugType(a.DrugType))
				{
					continue;
				}

				if (ContainsType(forbidden, a.DrugType))
				{
					continue;
				}

				var score = a.Affinity;
				if (score <= 0.45f)
				{
					continue;
				}

				// Insert into top-3 (descending).
				for (var slot = 0; slot < 3; slot++)
				{
					if (!(score > bestScores[slot]))
					{
						continue;
					}

					for (var j = 2; j > slot; j--)
					{
						bestScores[j] = bestScores[j - 1];
						bestTypes[j] = bestTypes[j - 1];
					}

					bestScores[slot] = score;
					bestTypes[slot] = a.DrugType;
					break;
				}
			}

			var top = new List<string>(3);
			for (var i = 0; i < 3; i++)
			{
				if (float.IsNegativeInfinity(bestScores[i]))
				{
					break;
				}

				top.Add(bestTypes[i].ToString());
			}

			if (top.Count > 0)
			{
				lines.Add($"<color={WantsColor}><b>Wants:</b> {string.Join(", ", top)}</color>");
			}
		}

		// "Refuses" (hard-blocked by this mod).
		if (forbidden.Count > 0)
		{
			var list = new List<string>();
			foreach (var x in forbidden)
			{
				if (!IsImplementedDrugType(x))
				{
					continue;
				}

				list.Add(x.ToString());
			}
			list.Sort(StringComparer.OrdinalIgnoreCase);
			if (list.Count > 0)
			{
				lines.Add($"<color={RefusesColor}><b>Refuses:</b> {string.Join(", ", list)}</color>");
			}
		}

		return lines;
	}

	private static bool ContainsType(IReadOnlyCollection<EDrugType> forbidden, EDrugType type)
	{
		foreach (var t in forbidden)
		{
			if (t == type)
			{
				return true;
			}
		}

		return false;
	}

	private static string StripExistingWantsRefuses(string? text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}

		var lines = text.Split('\n');
		var kept = new List<string>(lines.Length);
		for (var i = 0; i < lines.Length; i++)
		{
			var line = lines[i].TrimEnd('\r');
			if (line.Contains("<b>Wants:</b>", StringComparison.Ordinal) ||
			    line.Contains("<b>Refuses:</b>", StringComparison.Ordinal))
			{
				continue;
			}

			kept.Add(line);
		}

		// Trim trailing empty lines so we can add a clean "\n\n" before our section.
		for (var i = kept.Count - 1; i >= 0; i--)
		{
			if (!string.IsNullOrWhiteSpace(kept[i]))
			{
				break;
			}

			kept.RemoveAt(i);
		}

		return string.Join("\n", kept);
	}

	private static bool IsImplementedDrugType(EDrugType type)
	{
		return type == EDrugType.Marijuana ||
		       type == EDrugType.Methamphetamine ||
		       type == EDrugType.Cocaine ||
		       type == EDrugType.Shrooms;
	}
}

[HarmonyPatch(typeof(HandoverScreen), "GetError")]
internal static class HandoverScreen_GetError_Patch
{
#if IL2CPP
	private static void Postfix(HandoverScreen __instance, ref string err, ref bool __result)
	{
		if (__instance == null || __result)
		{
			return;
		}

		if (__instance.Mode != HandoverScreen.EMode.Sample && __instance.Mode != HandoverScreen.EMode.Offer)
		{
			return;
		}

		var customer = __instance.CurrentCustomer;
		if (customer == null)
		{
			return;
		}

		var slots = __instance.CustomerSlots;
		if (slots == null)
		{
			return;
		}

		for (var i = 0; i < slots.Length; i++)
		{
			var slot = slots[i];
			var item = slot?.ItemInstance;
			if (item == null)
			{
				continue;
			}

			var def = ForbiddenProductsService.GetProductDefinition(item);
			if (def == null && ForbiddenProductsConfig.ShouldDebugNpc(customer.NPC?.ID))
			{
				ForbiddenProductsDebugLog.WarnThrottled(
					$"fp:geterror:defnull:{customer.NPC?.ID}:{item.ID}",
					1.0f,
					$"ForbiddenProducts[GetError]: npcId='{customer.NPC?.ID}' itemId='{item.ID}' def=null (item.DefinitionType='{item.Definition?.GetType().FullName ?? "null"}').");
			}

			if (ForbiddenProductsService.IsForbidden(customer, def))
			{
				err = $"{customer.NPC?.FirstName ?? "Customer"} refuses this product.";
				__result = true;
				if (ForbiddenProductsConfig.ShouldDebugNpc(customer.NPC?.ID))
				{
					ForbiddenProductsDebugLog.WarnThrottled(
						$"fp:geterror:forbidden:{customer.NPC?.ID}:{item.ID}",
						0.5f,
						$"ForbiddenProducts[GetError]: npcId='{customer.NPC?.ID}' itemId='{item.ID}' -> FORBIDDEN (defId='{def?.ID ?? "null"}').");
				}
				return;
			}
		}

		if (ForbiddenProductsConfig.ShouldDebugNpc(customer.NPC?.ID))
		{
			ForbiddenProductsDebugLog.MsgThrottled(
				$"fp:geterror:ok:{customer.NPC?.ID}",
				1.0f,
				$"ForbiddenProducts[GetError]: npcId='{customer.NPC?.ID}' -> ok (no forbidden items detected).");
		}
	}
#else
	private static void Postfix(HandoverScreen __instance, ref string err, ref bool __result, ItemSlot[] ___CustomerSlots)
	{
		if (__instance == null || __result)
		{
			return;
		}

		if (__instance.Mode != HandoverScreen.EMode.Sample && __instance.Mode != HandoverScreen.EMode.Offer)
		{
			return;
		}

		var customer = __instance.CurrentCustomer;
		if (customer == null || ___CustomerSlots == null || ___CustomerSlots.Length == 0)
		{
			return;
		}

		for (var i = 0; i < ___CustomerSlots.Length; i++)
		{
			var slot = ___CustomerSlots[i];
			var item = slot?.ItemInstance;
			if (item == null)
			{
				continue;
			}

			var def = ForbiddenProductsService.GetProductDefinition(item);
			if (ForbiddenProductsService.IsForbidden(customer, def))
			{
				err = $"{customer.NPC?.FirstName ?? "Customer"} refuses this product.";
				__result = true;
				return;
			}
		}
	}
#endif
}

[HarmonyPatch(typeof(HandoverScreen), nameof(HandoverScreen.DonePressed))]
internal static class HandoverScreen_DonePressed_Patch
{
	private static bool Prefix(HandoverScreen __instance)
	{
		if (__instance == null)
		{
			return true;
		}

		if (__instance.Mode != HandoverScreen.EMode.Sample && __instance.Mode != HandoverScreen.EMode.Offer)
		{
			return true;
		}

		var customer = __instance.CurrentCustomer;
		if (customer == null)
		{
			return true;
		}

#if IL2CPP
		var slots = __instance.CustomerSlots;
		if (slots == null)
		{
			return true;
		}

		for (var i = 0; i < slots.Length; i++)
		{
			var item = slots[i]?.ItemInstance;
			if (item == null)
			{
				continue;
			}

			var def = ForbiddenProductsService.GetProductDefinition(item);
			if (def == null && ForbiddenProductsConfig.ShouldDebugNpc(customer.NPC?.ID))
			{
				ForbiddenProductsDebugLog.WarnThrottled(
					$"fp:donepressed:defnull:{customer.NPC?.ID}:{item.ID}",
					1.0f,
					$"ForbiddenProducts[DonePressed]: npcId='{customer.NPC?.ID}' itemId='{item.ID}' def=null (item.DefinitionType='{item.Definition?.GetType().FullName ?? "null"}').");
			}
			if (!ForbiddenProductsService.IsForbidden(customer, def))
			{
				continue;
			}

			HandoverScreenUiHelpers.ApplyForbiddenUiState(__instance, customer);
			if (ForbiddenProductsConfig.ShouldDebugNpc(customer.NPC?.ID))
			{
				ForbiddenProductsDebugLog.WarnThrottled(
					$"fp:donepressed:blocked:{customer.NPC?.ID}:{item.ID}",
					0.5f,
					$"ForbiddenProducts[DonePressed]: blocked finalize npcId='{customer.NPC?.ID}' itemId='{item.ID}' (defId='{def?.ID ?? "null"}').");
			}
			return false;
		}
#else
		// Mono already has other gates; keep this path simple.
		// (We still allow original to run if nothing forbidden is detected.)
#endif

		return true;
	}
}

[HarmonyPatch(typeof(HandoverScreen), "UpdateDoneButton")]
internal static class HandoverScreen_UpdateDoneButton_Patch
{
#if IL2CPP
	private static void Postfix(HandoverScreen __instance)
	{
		if (__instance == null)
		{
			return;
		}

		var customer = __instance.CurrentCustomer;
		if (customer == null)
		{
			return;
		}

		var slots = __instance.CustomerSlots;
		if (slots == null)
		{
			return;
		}

		for (var i = 0; i < slots.Length; i++)
		{
			var item = slots[i]?.ItemInstance;
			if (item == null)
			{
				continue;
			}

			var def = ForbiddenProductsService.GetProductDefinition(item);
			if (def == null && ForbiddenProductsConfig.ShouldDebugNpc(customer.NPC?.ID))
			{
				ForbiddenProductsDebugLog.WarnThrottled(
					$"fp:updatedone:defnull:{customer.NPC?.ID}:{item.ID}",
					1.0f,
					$"ForbiddenProducts[UpdateDoneButton]: npcId='{customer.NPC?.ID}' itemId='{item.ID}' def=null (item.DefinitionType='{item.Definition?.GetType().FullName ?? "null"}').");
			}
			if (!ForbiddenProductsService.IsForbidden(customer, def))
			{
				continue;
			}

			HandoverScreenUiHelpers.ApplyForbiddenUiState(__instance, customer);
			if (ForbiddenProductsConfig.ShouldDebugNpc(customer.NPC?.ID))
			{
				ForbiddenProductsDebugLog.WarnThrottled(
					$"fp:updatedone:forbidden:{customer.NPC?.ID}:{item.ID}",
					0.5f,
					$"ForbiddenProducts[UpdateDoneButton]: npcId='{customer.NPC?.ID}' itemId='{item.ID}' -> FORBIDDEN (forcing Done disabled, defId='{def?.ID ?? "null"}').");
			}
			return;
		}

		if (ForbiddenProductsConfig.ShouldDebugNpc(customer.NPC?.ID))
		{
			ForbiddenProductsDebugLog.MsgThrottled(
				$"fp:updatedone:ok:{customer.NPC?.ID}",
				1.0f,
				$"ForbiddenProducts[UpdateDoneButton]: npcId='{customer.NPC?.ID}' -> ok (no forbidden items detected).");
	}
}

[HarmonyPatch(typeof(HandoverScreen), "UpdateSuccessChance")]
internal static class HandoverScreen_UpdateSuccessChance_Patch
{
	private static void Postfix(HandoverScreen __instance)
	{
		if (__instance == null)
		{
			return;
		}

		if (__instance.Mode != HandoverScreen.EMode.Sample && __instance.Mode != HandoverScreen.EMode.Offer)
		{
			return;
		}

		var customer = __instance.CurrentCustomer;
		if (customer == null)
		{
			return;
		}

		var slots = __instance.CustomerSlots;
		if (slots == null)
		{
			return;
		}

		for (var i = 0; i < slots.Length; i++)
		{
			var item = slots[i]?.ItemInstance;
			if (item == null)
			{
				continue;
			}

			var def = ForbiddenProductsService.GetProductDefinition(item);
			if (!ForbiddenProductsService.IsForbidden(customer, def))
			{
				continue;
			}

			// If we're blocking the deal/sample anyway, make the displayed chance reflect that.
			if (__instance.SuccessLabel != null)
			{
				__instance.SuccessLabel.text = "0% chance of success";
				if (__instance.SuccessColorMap != null)
				{
					__instance.SuccessLabel.color = __instance.SuccessColorMap.Evaluate(0f);
				}
				__instance.SuccessLabel.enabled = true;
			}

			if (ForbiddenProductsConfig.ShouldDebugNpc(customer.NPC?.ID))
			{
				ForbiddenProductsDebugLog.WarnThrottled(
					$"fp:updatesuccess:forced0:{customer.NPC?.ID}:{item.ID}",
					0.5f,
					$"ForbiddenProducts[UpdateSuccessChance]: npcId='{customer.NPC?.ID}' itemId='{item.ID}' -> FORBIDDEN (forcing 0% label).");
			}

			return;
		}
	}
}
#else
	private static void Postfix(HandoverScreen __instance, ItemSlot[] ___CustomerSlots)
	{
		if (__instance == null)
		{
			return;
		}

		var customer = __instance.CurrentCustomer;
		if (customer == null || ___CustomerSlots == null || ___CustomerSlots.Length == 0)
		{
			return;
		}

		for (var i = 0; i < ___CustomerSlots.Length; i++)
		{
			var slot = ___CustomerSlots[i];
			var item = slot?.ItemInstance;
			if (item == null)
			{
				continue;
			}

			var def = ForbiddenProductsService.GetProductDefinition(item);
			if (!ForbiddenProductsService.IsForbidden(customer, def))
			{
				continue;
			}

				HandoverScreenUiHelpers.ApplyForbiddenUiState(__instance, customer);
				return;
			}
		}
	#endif
}
