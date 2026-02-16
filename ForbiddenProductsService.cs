using System.Collections.Generic;

namespace ForbiddenProducts;

public static class ForbiddenProductsService
{
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

	public static ProductDefinition? GetProductDefinition(ProductItemInstance? productItem)
	{
		if (productItem == null)
		{
			return null;
		}

		// productItem.ID is the definition ID (see ItemInstance ctor); this avoids brittle IL2CPP casts.
		var fromRegistry = Registry.GetItem<ProductDefinition>(productItem.ID);
		return fromRegistry ?? (productItem.Definition as ProductDefinition);
	}

	public static ProductDefinition? GetProductDefinition(ItemInstance? item)
	{
		if (item == null)
		{
			return null;
		}

		// Prefer strongly-typed instances when possible.
		if (item is ProductItemInstance pi)
		{
			return GetProductDefinition(pi);
		}

		// ItemInstance.Definition already does Registry.GetItem(ID) internally.
		var fromDef = item.Definition as ProductDefinition;
		if (fromDef != null)
		{
			return fromDef;
		}

		if (string.IsNullOrEmpty(item.ID))
		{
			return null;
		}

		// Fallback: try generic registry cast, then non-generic cast.
		return Registry.GetItem<ProductDefinition>(item.ID) ?? (Registry.GetItem(item.ID) as ProductDefinition);
	}

	public static IReadOnlyCollection<EDrugType> GetForbidden(Customer customer)
	{
		if (customer == null || customer.NPC == null)
		{
			return ForbiddenProductsConfig.GetForbiddenForNpcId(null);
		}

		return ForbiddenProductsConfig.GetForbiddenForNpcId(customer.NPC.ID);
	}

	public static bool IsForbidden(Customer customer, ProductDefinition? product)
	{
		if (product == null)
		{
			return false;
		}

		var forbidden = GetForbidden(customer);
		if (forbidden.Count == 0)
		{
			return false;
		}

		// Any overlapping drug type makes the whole product forbidden.
		for (var i = 0; i < product.DrugTypes.Count; i++)
		{
			if (ContainsType(forbidden, product.DrugTypes[i].DrugType))
			{
				return true;
			}
		}

		return false;
	}

	public static bool ContainsForbidden(Customer customer, IEnumerable<ItemInstance> items)
	{
		foreach (var item in items)
		{
			var def = GetProductDefinition(item);
			if (def != null && IsForbidden(customer, def))
			{
				return true;
			}
		}

		return false;
	}

#if IL2CPP
	public static bool ContainsForbidden(Customer customer, Il2CppSystem.Collections.Generic.List<ItemInstance> items)
	{
		if (items == null || items.Count == 0)
		{
			return false;
		}

		for (var i = 0; i < items.Count; i++)
		{
			var item = items[i];
			var def = GetProductDefinition(item);
			if (def != null && IsForbidden(customer, def))
			{
				return true;
			}
		}

		return false;
	}
#endif

	public static bool ContainsForbiddenProductType(Customer customer, ProductDefinition product, EDrugType type)
	{
		if (!IsForbidden(customer, product))
		{
			return false;
		}

		var forbidden = GetForbidden(customer);
		return ContainsType(forbidden, type);
	}
}
