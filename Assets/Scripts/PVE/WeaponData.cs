using UnityEngine;

[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Game/WeaponData")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public float damage;
    public float range;
}

