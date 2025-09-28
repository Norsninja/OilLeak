using UnityEngine;
using UnityEngine.UI;
/*
Radial Layout Group by Just a Pixel (Danny Goodayle) - http://www.justapixel.co.uk
Copyright (c) 2015
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/
public class RadialLayout : LayoutGroup {
    [Header("Layout Configuration")]
    public float fDistance = 100f;

    [Header("Circle Settings")]
    [Range(0f, 360f)]
    public float StartAngle = 180f;  // Default to π (left side)

    [Tooltip("Use full circle (360) or partial arc")]
    [Range(0f, 360f)]
    public float ArcSize = 360f;  // Full circle by default

    [Tooltip("Clockwise arrangement (right, like clock hands: 12→1→2→3)")]
    public bool Clockwise = true;

    [Tooltip("Skip the last position to avoid overlap at full circle")]
    public bool AvoidOverlap = true;  // For full circle, don't put items at both 0° and 360°

    protected override void OnEnable() { base.OnEnable(); CalculateRadial(); }

    public override void SetLayoutHorizontal()
    {
    }

    public override void SetLayoutVertical()
    {
    }

    public override void CalculateLayoutInputVertical()
    {
        CalculateRadial();
    }

    public override void CalculateLayoutInputHorizontal()
    {
        CalculateRadial();
    }

    #if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        CalculateRadial();
    }
    #endif

    /// <summary>
    /// Called when children are added or removed - ensures layout updates automatically
    /// </summary>
    protected override void OnTransformChildrenChanged()
    {
        base.OnTransformChildrenChanged();
        CalculateRadial();
    }

    void CalculateRadial()
    {
        m_Tracker.Clear();

        int childCount = transform.childCount;
        if (childCount == 0)
            return;

        // Calculate angle between items based on TOTAL possible items, not current count
        // This creates clustering behavior when you have fewer items
        float angleStep;

        // Try to get total item count from the catalog service
        int totalPossibleItems = 0;
        if (GameCore.ItemLookup != null)
        {
            totalPossibleItems = GameCore.ItemLookup.TotalItemCount;
        }

        // If we have catalog info and want clustering behavior
        if (totalPossibleItems > 0 && ArcSize >= 360f && AvoidOverlap)
        {
            // Use fixed spacing based on total possible items
            // This makes items cluster together when you have fewer than max
            angleStep = 360f / totalPossibleItems;
            Debug.Log($"[RadialLayout] Using catalog-based spacing: {angleStep}° (total possible: {totalPossibleItems})");
        }
        else if (ArcSize >= 360f && AvoidOverlap && childCount > 1)
        {
            // Fallback: distribute evenly based on current count
            angleStep = 360f / childCount;
        }
        else if (childCount > 1)
        {
            // For partial arc or when overlap is allowed
            angleStep = ArcSize / (childCount - 1);
        }
        else
        {
            // Single item - place at start angle
            angleStep = 0;
        }

        // Apply direction: In Unity UI with Cos/Sin positioning:
        // When Clockwise is true, we want items to go right (12→1→2→3)
        // This requires POSITIVE angleStep due to how Unity's coordinate system works
        // So we negate when Clockwise is FALSE to go counter-clockwise
        if (!Clockwise)
        {
            angleStep = -angleStep;
        }

        // Position each child
        for (int i = 0; i < childCount; i++)
        {
            RectTransform child = (RectTransform)transform.GetChild(i);
            if (child != null && child.gameObject.activeSelf)  // Only position active children
            {
                // Add elements to tracker to prevent manual editing
                m_Tracker.Add(this, child,
                    DrivenTransformProperties.Anchors |
                    DrivenTransformProperties.AnchoredPosition |
                    DrivenTransformProperties.Pivot);

                // Calculate angle for this item
                float currentAngle = StartAngle + (angleStep * i);

                // Convert to radians and calculate position
                float angleRad = currentAngle * Mathf.Deg2Rad;
                Vector3 vPos = new Vector3(
                    Mathf.Cos(angleRad),
                    Mathf.Sin(angleRad),
                    0
                );

                child.localPosition = vPos * fDistance;

                // Force center alignment
                child.anchorMin = child.anchorMax = child.pivot = new Vector2(0.5f, 0.5f);
            }
        }
    }

    // Helper method to recalculate when items change
    public void RefreshLayout()
    {
        CalculateRadial();
    }
}