using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace ForbiddenProducts;

internal static class ForbiddenProductsDebugLog
{
	// Key -> last log time (realtimeSinceStartup).
	private static readonly Dictionary<string, float> LastLogAt = new(StringComparer.Ordinal);

	public static void MsgThrottled(string key, float seconds, string message)
	{
		if (!ShouldLogNow(key, seconds))
		{
			return;
		}

		MelonLogger.Msg(message);
	}

	public static void WarnThrottled(string key, float seconds, string message)
	{
		if (!ShouldLogNow(key, seconds))
		{
			return;
		}

		MelonLogger.Warning(message);
	}

	private static bool ShouldLogNow(string key, float seconds)
	{
		try
		{
			var now = Time.realtimeSinceStartup;
			if (LastLogAt.TryGetValue(key, out var last) && now - last < seconds)
			{
				return false;
			}

			LastLogAt[key] = now;
			return true;
		}
		catch
		{
			// If UnityEngine.Time is not available for some reason, don't block logging.
			return true;
		}
	}
}

