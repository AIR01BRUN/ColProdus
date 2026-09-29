using System;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Type unique utilisé partout pour représenter un item et sa quantité
/// (inventaires, besoins, méthodes, procédures, transferts, marchés...).
/// </summary>
public struct Items : IBufferElementData, IEquatable<Items>
{
    public FixedString64Bytes ItemId;
    public int Quantity;

    public Items(FixedString64Bytes itemId, int quantity)
    {
        ItemId = itemId;
        Quantity = quantity;
    }

    public Items(string itemId, int quantity)
    {
        ItemId = new FixedString64Bytes(itemId);
        Quantity = quantity;
    }

    /// <summary>Emplacement d'inventaire vide.</summary>
    public static Items None => new Items("None", -1);

    /// <summary>Vrai si aucun item n'est renseigné.</summary>
    public bool IsEmpty => ItemId.Length == 0;

    public bool Equals(Items other) => ItemId.Equals(other.ItemId) && Quantity == other.Quantity;

    public override bool Equals(object obj) => obj is Items other && Equals(other);

    public override int GetHashCode() => ItemId.GetHashCode();

    public override string ToString() => $"{ItemId} x{Quantity}";

    public static bool operator ==(Items left, Items right) => left.Equals(right);

    public static bool operator !=(Items left, Items right) => !left.Equals(right);
}