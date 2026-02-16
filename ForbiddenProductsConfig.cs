using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;

namespace ForbiddenProducts;

public static class ForbiddenProductsConfig
{
	private const string DefaultResourceName = "ForbiddenProducts.customer_forbidden_products.default.json";

	private static readonly object LockObj = new();
	private static readonly Dictionary<string, HashSet<EDrugType>> ForbiddenByNpcId =
		new(StringComparer.OrdinalIgnoreCase);
	private static readonly HashSet<EDrugType> Empty = new();

	public static int ConfiguredCustomersCount
	{
		get
		{
			lock (LockObj)
			{
				return ForbiddenByNpcId.Count;
			}
		}
	}

	public static string ConfigPath =>
		Path.Combine(MelonEnvironment.UserDataDirectory, "ForbiddenProducts", "customer_forbidden_products.json");

	public static string DebugFlagPath =>
		Path.Combine(MelonEnvironment.UserDataDirectory, "ForbiddenProducts", "forbidden_products.debug");

	public static string DebugNpcIdsPath =>
		Path.Combine(MelonEnvironment.UserDataDirectory, "ForbiddenProducts", "forbidden_products.debug_npcs.txt");

	public static bool DebugEnabled => File.Exists(DebugFlagPath);

	public static bool ShouldDebugNpc(string? npcId)
	{
		if (!DebugEnabled)
		{
			return false;
		}

		// If no filter file exists, log for all NPCs when debug is enabled.
		if (!File.Exists(DebugNpcIdsPath))
		{
			return true;
		}

		try
		{
			var lines = File.ReadAllLines(DebugNpcIdsPath);
			var any = false;
			foreach (var line in lines)
			{
				var s = line?.Trim();
				if (string.IsNullOrEmpty(s) || s.StartsWith("#"))
				{
					continue;
				}

				any = true;
				if (!string.IsNullOrEmpty(npcId) && string.Equals(npcId.Trim(), s, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			// Empty/only comments => debug all.
			return !any;
		}
		catch
		{
			// If reading fails, fall back to debug all when enabled.
			return true;
		}
	}

	public static IReadOnlyCollection<EDrugType> GetForbiddenForNpcId(string? npcId)
	{
		if (string.IsNullOrWhiteSpace(npcId))
		{
			return Empty;
		}

		lock (LockObj)
		{
			return ForbiddenByNpcId.TryGetValue(npcId.Trim(), out var set) ? set : Empty;
		}
	}

	public static void LoadOrCreateDefault()
	{
		lock (LockObj)
		{
			ForbiddenByNpcId.Clear();
		}

		try
		{
			var dir = Path.GetDirectoryName(ConfigPath);
			if (!string.IsNullOrEmpty(dir))
			{
				Directory.CreateDirectory(dir);
			}

			if (!File.Exists(ConfigPath))
			{
				WriteDefaultConfig(ConfigPath);
				MelonLogger.Warning($"ForbiddenProducts: created default config at '{ConfigPath}'.");
			}

			var json = File.ReadAllText(ConfigPath);
			var raw = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json)
				?? new Dictionary<string, List<string>>();

			var normalized = new Dictionary<string, HashSet<EDrugType>>(StringComparer.OrdinalIgnoreCase);
			foreach (var (npcId, list) in raw)
			{
				if (string.IsNullOrWhiteSpace(npcId) || list == null || list.Count == 0)
				{
					continue;
				}

				var set = new HashSet<EDrugType>();
				foreach (var s in list)
				{
					if (string.IsNullOrWhiteSpace(s))
					{
						continue;
					}

					if (!Enum.TryParse(s.Trim(), ignoreCase: true, out EDrugType parsed))
					{
						MelonLogger.Warning($"ForbiddenProducts: unknown EDrugType '{s}' for npcId '{npcId}'.");
						continue;
					}

					set.Add(parsed);
				}

				if (set.Count > 0)
				{
					normalized[npcId.Trim()] = set;
				}
			}

			lock (LockObj)
			{
				foreach (var (npcId, set) in normalized)
				{
					ForbiddenByNpcId[npcId] = set;
				}
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Error($"ForbiddenProducts: failed to load config '{ConfigPath}': {ex}");
		}
	}

	private static void WriteDefaultConfig(string path)
	{
		var asm = typeof(ForbiddenProductsMod).Assembly;
		using var stream = asm.GetManifestResourceStream(DefaultResourceName);
		if (stream == null)
		{
			throw new InvalidOperationException(
				$"Embedded default config resource '{DefaultResourceName}' not found. " +
				"Ensure the csproj embeds src/ForbiddenProducts/customer_forbidden_products.proposal.json.");
		}

		using var reader = new StreamReader(stream);
		var json = reader.ReadToEnd();

		// Keep keys stable but sort them to reduce diff-noise if users edit later.
		var obj = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json)
			?? new Dictionary<string, List<string>>();
		var sorted = obj
			.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

		var output = JsonConvert.SerializeObject(sorted, Formatting.Indented);
		File.WriteAllText(path, output);
	}
}
