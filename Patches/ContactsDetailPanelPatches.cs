using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ForbiddenProducts.Patches;

[HarmonyPatch(typeof(ContactsDetailPanel), nameof(ContactsDetailPanel.Open))]
internal static class ContactsDetailPanel_Open_Patch
{
	private const string WantsColor = "#A6E3A1";
	private const string RefusesColor = "#FF6B6B";

	private static void Postfix(ContactsDetailPanel __instance, NPC npc)
	{
		if (__instance == null || npc == null)
		{
			return;
		}

		var customer = npc.GetComponent<Customer>();
		if (customer == null)
		{
			return;
		}

		// Only touch the UI when the game decided to show the Properties section.
		if (__instance.PropertiesContainer == null || __instance.PropertiesLabel == null)
		{
			return;
		}

		if (!__instance.PropertiesContainer.gameObject.activeSelf)
		{
			return;
		}

		var forbidden = ForbiddenProductsConfig.GetForbiddenForNpcId(npc.ID);

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

		if (lines.Count == 0)
		{
			return;
		}

		// The panel can be reopened/reused without resetting the label (or multiple hooks can
		// cause Open() to run more than once), so ensure we don't accumulate duplicates.
		var baseText = StripExistingWantsRefuses(__instance.PropertiesLabel.text);
		if (!string.IsNullOrEmpty(baseText))
		{
			baseText += "\n\n";
		}

		__instance.PropertiesLabel.text = baseText + string.Join("\n", lines);
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
			if (line.Contains($"<color={WantsColor}><b>Wants:</b>", StringComparison.Ordinal) ||
			    line.Contains($"<color={RefusesColor}><b>Refuses:</b>", StringComparison.Ordinal))
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
