using UnityEngine;

public sealed class FpsWorldWeaponRig : MonoBehaviour
{
    [SerializeField] private GameObject[] primaryModels;
    [SerializeField] private GameObject[] secondaryModels;

    public void Configure(GameObject[] primary, GameObject[] secondary)
    {
        primaryModels = primary;
        secondaryModels = secondary;
        SetWeapon(WeaponSlot.Primary, 0);
    }

    public void SetWeapon(WeaponSlot slot, int index)
    {
        SetGroup(primaryModels, slot == WeaponSlot.Primary, index);
        SetGroup(secondaryModels, slot == WeaponSlot.Secondary, index);
    }

    private static void SetGroup(GameObject[] models, bool selected, int index)
    {
        if (models == null) return;
        for (int i = 0; i < models.Length; i++)
            if (models[i] != null) models[i].SetActive(selected && i == index);
    }
}
