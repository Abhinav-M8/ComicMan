using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class ComicPanel : MonoBehaviour
{
    // ============================================================
    // PANEL IDENTITY
    // ============================================================

    [Header("Panel Identity")]

    // Human-readable panel name.
    [SerializeField] private string panelID = "Panel_01";


    // ============================================================
    // PAGE ASSIGNMENT
    // ============================================================

    [Header("Page Assignment")]

    // 0 = left page.
    // 1 = right page.
    [SerializeField, Range(0, 1)]
    private int pageIndex;


    // ============================================================
    // SLOT
    // ============================================================

    [Header("Fixed Slot")]

    // Current slot position inside the page.
    [SerializeField] private int slotIndex;


    // ============================================================
    // PANEL RULES
    // ============================================================

    [Header("Panel Rules")]

    // Whether this panel is allowed to be moved in Artist Mode.
    [SerializeField] private bool isMovable = true;


    // ============================================================
    // HOP POINTS
    // ============================================================

    [Header("Arrival Points")]

    // Locations where ComicMan can arrive when entering this panel.
    [SerializeField] private Transform[] hopPoints;


    // ============================================================
    // PUBLIC PROPERTIES
    // ============================================================

    public string PanelID => panelID;

    public int PageIndex => pageIndex;

    public int SlotIndex => slotIndex;

    public bool IsMovable => isMovable;


    // ============================================================
    // INTERNAL REFERENCES
    // ============================================================

    private BoxCollider2D panelCollider;


    // ============================================================
    // AWAKE
    // ============================================================

    private void Awake()
    {
        // Get the panel's collider.
        panelCollider = GetComponent<BoxCollider2D>();

        // Panels act as trigger areas for panel detection.
        panelCollider.isTrigger = true;
    }


    // ============================================================
    // ON VALIDATE
    // Keeps collider configuration correct in the Inspector.
    // ============================================================

    private void OnValidate()
    {
        panelCollider = GetComponent<BoxCollider2D>();

        if (panelCollider != null)
            panelCollider.isTrigger = true;
    }


    // ============================================================
    // SET PAGE INDEX
    // Changes which page owns this panel.
    // ============================================================

    public void SetPageIndex(int page)
    {
        pageIndex = Mathf.Clamp(page, 0, 1);
    }


    // ============================================================
    // SET SLOT INDEX
    // Changes the panel's logical slot.
    // ============================================================

    public void SetSlotIndex(int slot)
    {
        slotIndex = Mathf.Max(0, slot);
    }


    // ============================================================
    // GET CLOSEST HOP POINT
    // Finds the hop point closest to a specified position.
    // ============================================================

    public Transform GetClosestHopPoint(Vector2 position)
    {
        Transform closest = null;

        float closestDistance = float.MaxValue;

        if (hopPoints == null)
            return null;

        foreach (Transform point in hopPoints)
        {
            if (point == null)
                continue;

            float distance =
                ((Vector2)point.position - position).sqrMagnitude;

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = point;
            }
        }

        return closest;
    }


    // ============================================================
    // GET WORLD BOUNDS
    // Returns the panel collider's world-space bounds.
    // ============================================================

    public Bounds GetWorldBounds()
    {
        if (panelCollider == null)
            panelCollider = GetComponent<BoxCollider2D>();

        return panelCollider.bounds;
    }


    // ============================================================
    // GIZMOS
    // Displays panel and hop-point information in Scene view.
    // ============================================================

    private void OnDrawGizmosSelected()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();

        if (col != null)
        {
            Gizmos.color = Color.cyan;

            Gizmos.DrawWireCube(
                transform.TransformPoint(col.offset),
                col.size
            );
        }

        if (hopPoints == null)
            return;

        Gizmos.color = Color.yellow;

        foreach (Transform point in hopPoints)
        {
            if (point != null)
                Gizmos.DrawWireSphere(point.position, 0.2f);
        }
    }
}