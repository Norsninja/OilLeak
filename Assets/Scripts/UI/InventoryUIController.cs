using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class InventoryUIController : MonoBehaviour
{
    public InventoryController inventoryController; // Reference to InventoryController
    public InventoryState inventoryState; // Reference to InventoryState
    public TextMeshProUGUI equippedItemText; // UI Text to display the equipped item and ammo
    public GameObject inventoryPanel; // The inventory panel GameObject
    public GameObject inventoryItemButtonPrefab; // Prefab for the inventory item buttons

    // Start is called before the first frame update
    void Start()
    {
        // Initially set the inventory panel to be inactive
        inventoryPanel.SetActive(false);
        UpdateEquippedItemDisplay();
    }

    // Update is called once per frame
    void Update()
    {
        UpdateEquippedItemDisplay();
        HandleInventoryToggle();
    }

    void UpdateEquippedItemDisplay()
    {
        string equippedItemName = inventoryState.equippedItem;
        int equippedItemCount = inventoryController.GetItemCount(equippedItemName);
        equippedItemText.text = $"{equippedItemName}: {equippedItemCount}";
    }

    void HandleInventoryToggle()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            PopulateInventoryPanel();
            inventoryPanel.SetActive(true);
        }
        else if (Input.GetKeyUp(KeyCode.Q))
        {
            inventoryPanel.SetActive(false);
        }
    }

    void PopulateInventoryPanel()
    {
        // Clear previous items
        foreach (Transform child in inventoryPanel.transform)
        {
            Destroy(child.gameObject);
        }

        // Populate the panel
        foreach (KeyValuePair<string, InventoryState.InventoryItem> entry in inventoryState.inventory)
        {
            GameObject newButton = Instantiate(inventoryItemButtonPrefab, inventoryPanel.transform);
            TextMeshProUGUI buttonText = newButton.GetComponentInChildren<TextMeshProUGUI>();
            buttonText.text = $"{entry.Key} ({entry.Value.count})";

            Button buttonComponent = newButton.GetComponent<Button>();
            string itemName = entry.Key; // Capture variable for the closure
            buttonComponent.onClick.AddListener(() => EquipSelectedItem(itemName));

            // Highlight the equipped item
            if (entry.Key == inventoryState.equippedItem)
            {
                // Apply highlight (You can set a color or add an icon to indicate this)
                buttonText.color = Color.yellow;
            }
        }
    }

    public void EquipSelectedItem(string itemName)
    {
        inventoryController.EquipItem(itemName);
        UpdateEquippedItemDisplay();
        PopulateInventoryPanel(); // To refresh the highlights
    }
}
