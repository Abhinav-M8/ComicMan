using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls:
/// - Comic panel organisation
/// - Artist Mode panel selection
/// - Panel highlighting
/// - Panel pickup/place/cancel
/// - Current ComicMan panel tracking
/// - Automatic hopping between panels
/// - Page-based panel selection
///
/// IMPORTANT:
/// Selection and movability are separate.
/// A panel can be SELECTED even when it cannot be MOVED.
///
/// Current ComicMan panel:
/// - remains selectable
/// - becomes dynamically immovable
/// - does NOT have its ComicPanel.IsMovable value changed
/// </summary>
public class PanelManager : MonoBehaviour
{
    // ============================================================
    // SINGLETON
    // ============================================================

    public static PanelManager Instance { get; private set; }


    // ============================================================
    // PAGE DATA
    // ============================================================

    /// <summary>
    /// Stores all panels belonging to one book page.
    /// </summary>
    [System.Serializable]
    public class Page
    {
        [Header("Page Area")]
        public BoxCollider2D pageArea;

        [Header("Layout")]
        public int columns = 2;
        public int rows = 2;

        [HideInInspector]
        public List<ComicPanel> panels = new List<ComicPanel>();
    }


    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("Player References")]
    [SerializeField] private Rigidbody2D playerBody;
    [SerializeField] private PlayerController playerController;

    [Header("Panel Reordering")]
    [SerializeField] private PanelReorderer panelReorderer;

    [Header("Pages")]
    [SerializeField] private Page leftPage;
    [SerializeField] private Page rightPage;

    [Header("Panel Layout")]
    [SerializeField] private float horizontalSpacing = 0.5f;
    [SerializeField] private float verticalSpacing = 0.5f;
    [SerializeField] private float pagePadding = 0.1f;

    [Header("Artist Mode")]
    [SerializeField] private bool allowArtistMode = true;

    [Header("Selection Highlight")]
    [SerializeField] private Transform selectionHighlight;
    [SerializeField] private float selectionPadding = 0.1f;

    [Header("Automatic Hop")]
    [SerializeField] private float hopLockDuration = 0.15f;


    // ============================================================
    // PANEL STATE
    // ============================================================

    // The panel ComicMan is physically standing in.
    private ComicPanel currentPanel;

    // The panel currently highlighted in Artist Mode.
    private ComicPanel selectedPanel;

    // The panel chosen by pressing E before pickup.
    private ComicPanel chosenPanel;

    // The panel currently being moved.
    private ComicPanel heldPanel;

    // Contents controller for the held panel.
    private PanelContents heldPanelContents;


    // ============================================================
    // ARTIST MODE STATE
    // ============================================================

    private bool artistMode;


    // ============================================================
    // PANEL PICKUP STATE
    // ============================================================

    private bool carryingPlayer;

    // Player's position relative to the panel before pickup.
    private Vector3 playerOffsetFromPanel;

    // Original panel information used when cancelling.
    private int originalPanelPage;
    private int originalPanelSlot;
    private Vector3 originalPanelPosition;


    // ============================================================
    // PLAYER STATE DURING PANEL MOVEMENT
    // ============================================================

    private RigidbodyType2D originalPlayerBodyType;
    private float originalPlayerGravityScale;
    private Vector2 originalPlayerVelocity;


    // ============================================================
    // AUTOMATIC HOP STATE
    // ============================================================

    private float lastHopTime = -999f;


    // ============================================================
    // UNITY - AWAKE
    // ============================================================

    /// <summary>
    /// Creates the PanelManager singleton and prepares references.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (playerBody == null)
        {
            Debug.LogWarning(
                "[PANEL DEBUG] Player Body is not assigned in PanelManager."
            );
        }

        if (playerController == null && playerBody != null)
        {
            playerController =
                playerBody.GetComponent<PlayerController>();
        }

        if (panelReorderer == null)
        {
            panelReorderer =
                FindFirstObjectByType<PanelReorderer>();
        }
    }


    // ============================================================
    // UNITY - START
    // ============================================================

    /// <summary>
    /// Finds all panels, organises them by page,
    /// lays them out and determines ComicMan's starting panel.
    /// </summary>
    private void Start()
    {
        BuildPageLists();

        LayoutAllPages();

        SetCurrentPanel();

        artistMode = false;

        selectedPanel = null;
        chosenPanel = null;
        heldPanel = null;

        UpdateSelectionVisual();

        Debug.Log(
            "[PANEL DEBUG] PanelManager startup complete."
        );

        PrintAllPanelStatus();
    }


    // ============================================================
    // BUILD PAGE LISTS
    // ============================================================

    /// <summary>
    /// Finds EVERY ComicPanel in the scene and places it
    /// into the correct page list using its PageIndex.
    ///
    /// IMPORTANT:
    /// We NEVER remove the ComicMan panel here.
    /// It must remain selectable.
    /// </summary>
    private void BuildPageLists()
    {
        leftPage.panels.Clear();
        rightPage.panels.Clear();

        ComicPanel[] allPanels =
            FindObjectsByType<ComicPanel>(
                FindObjectsSortMode.None
            );

        foreach (ComicPanel panel in allPanels)
        {
            if (panel == null)
                continue;

            // PageIndex decides which page the panel belongs to.
            if (panel.PageIndex == 0)
            {
                leftPage.panels.Add(panel);
            }
            else
            {
                rightPage.panels.Add(panel);
            }
        }

        // Sort each page by slotIndex.
        leftPage.panels.Sort(
            (a, b) => a.SlotIndex.CompareTo(b.SlotIndex)
        );

        rightPage.panels.Sort(
            (a, b) => a.SlotIndex.CompareTo(b.SlotIndex)
        );

        Debug.Log(
            "[PANEL DEBUG] PAGE LISTS BUILT\n" +
            "Left page panels: " +
            leftPage.panels.Count +
            "\nRight page panels: " +
            rightPage.panels.Count
        );

        PrintPageList("LEFT", leftPage.panels);
        PrintPageList("RIGHT", rightPage.panels);
    }


    // ============================================================
    // PRINT PAGE LIST
    // ============================================================

    /// <summary>
    /// Prints the panels belonging to a page.
    /// Used only for debugging.
    /// </summary>
    private void PrintPageList(
        string pageName,
        List<ComicPanel> panels)
    {
        string result =
            "[PANEL DEBUG] " +
            pageName +
            " PAGE ORDER: ";

        for (int i = 0; i < panels.Count; i++)
        {
            if (panels[i] == null)
                continue;

            result +=
                panels[i].PanelID +
                "(slot " +
                panels[i].SlotIndex +
                ")";

            if (i < panels.Count - 1)
                result += " -> ";
        }

        Debug.Log(result);
    }


    // ============================================================
    // LAYOUT ALL PAGES
    // ============================================================

    /// <summary>
    /// Positions all panels in their assigned page slots.
    /// </summary>
    private void LayoutAllPages()
    {
        LayoutPage(leftPage);
        LayoutPage(rightPage);
    }


    // ============================================================
    // LAYOUT ONE PAGE
    // ============================================================

    /// <summary>
    /// Positions the panels inside one page according to their
    /// slotIndex values.
    ///
    /// PageIndex is NOT changed here.
    /// </summary>
    private void LayoutPage(Page page)
    {
        if (page == null || page.pageArea == null)
            return;

        if (page.panels == null)
            return;

        Bounds bounds = page.pageArea.bounds;

        int columns = Mathf.Max(1, page.columns);
        int rows = Mathf.Max(1, page.rows);

        float usableWidth =
            bounds.size.x - pagePadding * 2f;

        float usableHeight =
            bounds.size.y - pagePadding * 2f;

        float cellWidth =
            usableWidth / columns;

        float cellHeight =
            usableHeight / rows;

        for (int i = 0; i < page.panels.Count; i++)
        {
            ComicPanel panel = page.panels[i];

            if (panel == null)
                continue;

            int slot = panel.SlotIndex;

            int column = slot % columns;
            int row = slot / columns;

            if (row >= rows)
            {
                Debug.LogWarning(
                    "[PANEL DEBUG] Panel " +
                    panel.PanelID +
                    " has slot " +
                    slot +
                    " outside page capacity."
                );

                continue;
            }

            float x =
                bounds.min.x +
                pagePadding +
                cellWidth * (column + 0.5f);

            float y =
                bounds.max.y -
                pagePadding -
                cellHeight * (row + 0.5f);

            panel.transform.position =
                new Vector3(
                    x,
                    y,
                    panel.transform.position.z
                );
        }
    }


    // ============================================================
    // FIND CURRENT PANEL
    // ============================================================

    /// <summary>
    /// Finds which panel currently contains ComicMan.
    /// Uses world bounds rather than the panel list.
    /// </summary>
    private void SetCurrentPanel()
    {
        if (playerBody == null)
        {
            Debug.LogWarning(
                "[PANEL DEBUG] Cannot find current panel: " +
                "Player Body is missing."
            );

            return;
        }

        Vector2 playerPosition = playerBody.position;

        ComicPanel foundPanel = null;

        ComicPanel[] allPanels =
            FindObjectsByType<ComicPanel>(
                FindObjectsSortMode.None
            );

        foreach (ComicPanel panel in allPanels)
        {
            if (panel == null)
                continue;

            Bounds panelBounds =
                panel.GetWorldBounds();

            if (panelBounds.Contains(playerPosition))
            {
                foundPanel = panel;
                break;
            }
        }

        if (foundPanel != null)
        {
            SetCurrentPanel(foundPanel);
        }
        else
        {
            Debug.LogWarning(
                "[PANEL DEBUG] ComicMan is not inside " +
                "any panel."
            );
        }
    }


    // ============================================================
    // SET CURRENT PANEL DIRECTLY
    // ============================================================

    /// <summary>
    /// Sets the panel ComicMan is currently inside.
    ///
    /// This changes ONLY the temporary protection state.
    /// ComicPanel.IsMovable is never modified.
    /// </summary>
    public void SetCurrentPanel(ComicPanel panel)
    {
        if (panel == null)
        {
            Debug.LogWarning(
                "[PANEL DEBUG] SetCurrentPanel received NULL."
            );

            return;
        }

        ComicPanel previousPanel = currentPanel;

        currentPanel = panel;

        Debug.Log(
            "[PANEL DEBUG] CURRENT PANEL CHANGED\n" +
            "Previous: " +
            (
                previousPanel != null
                    ? previousPanel.PanelID
                    : "None"
            ) +
            "\nCurrent: " +
            currentPanel.PanelID +
            "\nCurrent Page: " +
            currentPanel.PageIndex +
            "\nCurrent Slot: " +
            currentPanel.SlotIndex
        );

        PrintPanelMovementStatus(
            previousPanel,
            currentPanel
        );

        // If Artist Mode is active, keep selection valid.
        if (artistMode)
        {
            UpdateSelectionVisual();
        }
    }


    // ============================================================
    // ARTIST MODE
    // ============================================================

    /// <summary>
    /// Enables or disables Artist Mode.
    ///
    /// IMPORTANT:
    /// When entering Artist Mode, the current ComicMan panel
    /// remains selected even if it is immovable.
    ///
    /// We do NOT automatically jump to the first movable panel.
    /// </summary>
    public void SetArtistMode(bool enabled)
    {
        if (!allowArtistMode)
        {
            artistMode = false;
            selectedPanel = null;
            chosenPanel = null;

            UpdateSelectionVisual();

            return;
        }

        artistMode = enabled;

        Debug.Log(
            "[PANEL DEBUG] ARTIST MODE = " +
            artistMode
        );

        if (artistMode)
        {
            // Refresh current panel before selecting.
            SetCurrentPanel();

            // IMPORTANT:
            // Select the current panel regardless of movability.
            //
            // This prevents the old bug where Artist Mode
            // automatically skipped to the first movable panel.
            selectedPanel = currentPanel;

            chosenPanel = null;

            Debug.Log(
                "[PANEL DEBUG] Artist Mode selection = " +
                (
                    selectedPanel != null
                        ? selectedPanel.PanelID
                        : "None"
                )
            );
        }
        else
        {
            // Leaving Artist Mode while holding a panel
            // cancels the movement safely.
            if (heldPanel != null)
            {
                CancelPanelMove();
            }

            chosenPanel = null;

            selectedPanel = null;
        }

        UpdateSelectionVisual();
    }


    // ============================================================
    // ARTIST MODE STATE
    // ============================================================

    /// <summary>
    /// Returns whether Artist Mode is currently active.
    /// </summary>
    public bool IsArtistMode()
    {
        return artistMode;
    }


    // ============================================================
    // UNITY - UPDATE
    // ============================================================

    /// <summary>
    /// Handles Artist Mode keyboard input.
    /// </summary>
    private void Update()
    {
        if (!artistMode)
            return;

        if (Keyboard.current == null)
            return;

        // --------------------------------------------------------
        // ESC = CANCEL PANEL MOVEMENT
        // --------------------------------------------------------

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (heldPanel != null)
            {
                CancelPanelMove();
            }

            return;
        }

        // --------------------------------------------------------
        // E = PICK UP / PLACE
        // --------------------------------------------------------

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            HandleArtistE();
        }

        // --------------------------------------------------------
        // MOVEMENT KEYS = SELECT PANEL
        // --------------------------------------------------------

        HandleSelectionInput();
    }


    // ============================================================
    // ARTIST MODE E
    // ============================================================

    /// <summary>
    /// Handles E while in Artist Mode.
    ///
    /// E does NOT care whether a panel is selectable.
    /// It checks whether the selected panel is movable.
    /// </summary>
    private void HandleArtistE()
    {
        // --------------------------------------------------------
        // IF HOLDING A PANEL -> PLACE IT
        // --------------------------------------------------------

        if (heldPanel != null)
        {
            PlaceHeldPanel();
            return;
        }

        // --------------------------------------------------------
        // NO SELECTED PANEL
        // --------------------------------------------------------

        if (selectedPanel == null)
        {
            Debug.LogWarning(
                "[PANEL DEBUG] E pressed but no panel is selected."
            );

            return;
        }

        chosenPanel = selectedPanel;

        Debug.Log(
            "[PANEL DEBUG] PANEL CHOSEN\n" +
            "Panel: " +
            chosenPanel.PanelID +
            "\nPage: " +
            chosenPanel.PageIndex +
            "\nSlot: " +
            chosenPanel.SlotIndex +
            "\nInspector Movable: " +
            chosenPanel.IsMovable +
            "\nFinal Movable: " +
            IsPanelMovable(chosenPanel)
        );

        // --------------------------------------------------------
        // CURRENT PANEL / LOCKED PANEL
        // --------------------------------------------------------

        if (!IsPanelMovable(chosenPanel))
        {
            Debug.Log(
                "[PANEL DEBUG] PANEL CANNOT BE MOVED: " +
                chosenPanel.PanelID
            );

            return;
        }

        // --------------------------------------------------------
        // PICK UP
        // --------------------------------------------------------

        PickUpSelectedPanel();
    }


    // ============================================================
    // HANDLE SELECTION INPUT
    // ============================================================

    /// <summary>
    /// Reads WASD and arrow keys and moves the selection
    /// inside the selected panel's CURRENT PAGE ONLY.
    ///
    /// IMPORTANT:
    /// This function does NOT filter out immovable panels.
    /// Therefore every panel remains selectable.
    /// </summary>
    private void HandleSelectionInput()
    {
        int directionX = 0;
        int directionY = 0;

        // Horizontal input.
        if (
            Keyboard.current.aKey.wasPressedThisFrame ||
            Keyboard.current.leftArrowKey.wasPressedThisFrame
        )
        {
            directionX = -1;
        }

        if (
            Keyboard.current.dKey.wasPressedThisFrame ||
            Keyboard.current.rightArrowKey.wasPressedThisFrame
        )
        {
            directionX = 1;
        }

        // Vertical input.
        if (
            Keyboard.current.wKey.wasPressedThisFrame ||
            Keyboard.current.upArrowKey.wasPressedThisFrame
        )
        {
            directionY = 1;
        }

        if (
            Keyboard.current.sKey.wasPressedThisFrame ||
            Keyboard.current.downArrowKey.wasPressedThisFrame
        )
        {
            directionY = -1;
        }

        if (directionX == 0 && directionY == 0)
            return;

        NavigateSelection(
            directionX,
            directionY
        );
    }


    // ============================================================
    // NAVIGATE SELECTION
    // ============================================================

    /// <summary>
    /// Moves the selection around the grid of the panel's page.
    ///
    /// CRITICAL RULES:
    /// - Immovable panels are still selectable.
    /// - Selection never crosses to the other page.
    /// - Navigation uses the panel's PageIndex.
    /// - Navigation uses SlotIndex.
    /// </summary>
    private void NavigateSelection(
        int directionX,
        int directionY)
    {
        if (selectedPanel == null)
            return;

        // --------------------------------------------------------
        // DETERMINE WHICH PAGE THE SELECTED PANEL BELONGS TO
        // --------------------------------------------------------

        Page page =
            selectedPanel.PageIndex == 0
                ? leftPage
                : rightPage;

        if (page == null)
            return;

        if (page.panels == null || page.panels.Count == 0)
            return;

        // --------------------------------------------------------
        // CURRENT SLOT
        // --------------------------------------------------------

        int currentSlot =
            selectedPanel.SlotIndex;

        int columns =
            Mathf.Max(1, page.columns);

        int rows =
            Mathf.Max(1, page.rows);

        int currentColumn =
            currentSlot % columns;

        int currentRow =
            currentSlot / columns;

        // --------------------------------------------------------
        // CALCULATE TARGET SLOT
        // --------------------------------------------------------

        int targetColumn =
            currentColumn + directionX;

        int targetRow =
            currentRow + directionY;

        // --------------------------------------------------------
        // HORIZONTAL WRAP
        // --------------------------------------------------------

        if (directionX != 0)
        {
            if (targetColumn < 0)
            {
                targetColumn = columns - 1;
            }
            else if (targetColumn >= columns)
            {
                targetColumn = 0;
            }
        }

        // --------------------------------------------------------
        // VERTICAL WRAP
        // --------------------------------------------------------

        if (directionY != 0)
        {
            if (targetRow < 0)
            {
                targetRow = rows - 1;
            }
            else if (targetRow >= rows)
            {
                targetRow = 0;
            }
        }

        // --------------------------------------------------------
        // TARGET SLOT
        // --------------------------------------------------------

        int targetSlot =
            targetRow * columns +
            targetColumn;

        // --------------------------------------------------------
        // FIND PANEL AT TARGET SLOT
        //
        // IMPORTANT:
        // We DO NOT check IsPanelMovable here.
        // Even an immovable panel must be selectable.
        // --------------------------------------------------------

        ComicPanel targetPanel = null;

        foreach (ComicPanel panel in page.panels)
        {
            if (panel == null)
                continue;

            if (panel.SlotIndex == targetSlot)
            {
                targetPanel = panel;
                break;
            }
        }

        if (targetPanel == null)
        {
            Debug.LogWarning(
                "[PANEL DEBUG] No panel found at target slot " +
                targetSlot +
                " on page " +
                selectedPanel.PageIndex
            );

            return;
        }

        // --------------------------------------------------------
        // SELECT TARGET
        // --------------------------------------------------------

        selectedPanel = targetPanel;

        Debug.Log(
            "[PANEL DEBUG] SELECTION CHANGED\n" +
            "Panel: " +
            selectedPanel.PanelID +
            "\nPage: " +
            selectedPanel.PageIndex +
            "\nSlot: " +
            selectedPanel.SlotIndex +
            "\nInspector Movable: " +
            selectedPanel.IsMovable +
            "\nFinal Movable: " +
            IsPanelMovable(selectedPanel)
        );

        UpdateSelectionVisual();
    }


    // ============================================================
    // CHECK PANEL MOVABILITY
    // ============================================================

    /// <summary>
    /// Determines whether a panel can actually be picked up.
    ///
    /// This is intentionally DIFFERENT from selection.
    ///
    /// A panel is movable when:
    /// 1. It exists.
    /// 2. Inspector IsMovable is true.
    /// 3. It is NOT the panel ComicMan is currently inside.
    /// </summary>
    private bool IsPanelMovable(ComicPanel panel)
    {
        if (panel == null)
            return false;

        // Permanent Inspector rule.
        if (!panel.IsMovable)
            return false;

        // Temporary runtime rule.
        // ComicMan's current panel is always protected.
        if (panel == currentPanel)
            return false;

        return true;
    }


    // ============================================================
    // PICK UP SELECTED PANEL
    // ============================================================

    /// <summary>
    /// Starts moving the selected panel.
    /// </summary>
    private void PickUpSelectedPanel()
    {
        if (selectedPanel == null)
            return;

        if (!IsPanelMovable(selectedPanel))
        {
            Debug.LogWarning(
                "[PANEL DEBUG] Attempted to pick up an immovable panel."
            );

            return;
        }

        heldPanel = selectedPanel;

        // Save original panel state.
        originalPanelPage =
            heldPanel.PageIndex;

        originalPanelSlot =
            heldPanel.SlotIndex;

        originalPanelPosition =
            heldPanel.transform.position;

        // --------------------------------------------------------
        // PREPARE NON-PLAYER CONTENTS
        // --------------------------------------------------------

        heldPanelContents =
            heldPanel.GetComponent<PanelContents>();

        if (heldPanelContents != null)
        {
            heldPanelContents.PrepareForPanelMove();
        }

        // --------------------------------------------------------
        // CHECK WHETHER PLAYER IS INSIDE PANEL
        // --------------------------------------------------------

        carryingPlayer = false;

        if (playerBody != null)
        {
            Bounds panelBounds =
                heldPanel.GetWorldBounds();

            if (panelBounds.Contains(playerBody.position))
            {
                carryingPlayer = true;

                playerOffsetFromPanel =
                    (Vector3)playerBody.position -
                    heldPanel.transform.position;

                originalPlayerBodyType =
                    playerBody.bodyType;

                originalPlayerGravityScale =
                    playerBody.gravityScale;

                originalPlayerVelocity =
                    playerBody.linearVelocity;

                playerBody.linearVelocity =
                    Vector2.zero;

                playerBody.angularVelocity =
                    0f;

                playerBody.gravityScale = 0f;

                playerBody.bodyType =
                    RigidbodyType2D.Kinematic;

                Debug.Log(
                    "[PANEL DEBUG] Player is being carried with panel."
                );
            }
        }

        Debug.Log(
            "[PANEL DEBUG] PICKED UP PANEL: " +
            heldPanel.PanelID +
            "\nPage: " +
            heldPanel.PageIndex +
            "\nSlot: " +
            heldPanel.SlotIndex
        );
    }


    // ============================================================
    // PLACE HELD PANEL
    // ============================================================

    /// <summary>
    /// Places the held panel back into its current slot.
    ///
    /// Reordering is NOT implemented here yet.
    /// That will be the next mechanic.
    /// </summary>
    private void PlaceHeldPanel()
    {
        if (heldPanel == null)
            return;

        // --------------------------------------------------------
        // The highlighted panel is the destination while a panel
        // is being held.
        // --------------------------------------------------------

        ComicPanel destinationPanel = selectedPanel;

        if (destinationPanel == null)
        {
            Debug.LogWarning(
                "[PANEL DEBUG] Cannot place panel: " +
                "no destination is selected."
            );

            return;
        }

        // --------------------------------------------------------
        // The moving panel cannot be inserted into its own slot.
        // --------------------------------------------------------

        if (destinationPanel == heldPanel)
        {
            Debug.Log(
                "[PANEL DEBUG] PLACE CANCELLED: " +
                "destination is the held panel."
            );

            return;
        }

        // --------------------------------------------------------
        // A protected panel is never allowed to be displaced.
        // This includes ComicMan's current panel.
        // --------------------------------------------------------

        if (!IsPanelMovable(destinationPanel))
        {
            Debug.Log(
                "[PANEL DEBUG] PLACE REFUSED: destination " +
                destinationPanel.PanelID +
                " is protected."
            );

            return;
        }

        Debug.Log(
            "[PANEL DEBUG] REQUESTING INSERTION\n" +
            "Moving: " +
            heldPanel.PanelID +
            "\nFrom Slot: " +
            heldPanel.SlotIndex +
            "\nDestination: " +
            destinationPanel.PanelID +
            "\nDestination Slot: " +
            destinationPanel.SlotIndex
        );

        // --------------------------------------------------------
        // PanelReorderer performs ONLY the slot reordering.
        // PanelManager remains responsible for player/content
        // state and visual layout.
        // --------------------------------------------------------

        if (panelReorderer == null)
        {
            Debug.LogError(
                "[PANEL DEBUG] PanelReorderer reference is missing."
            );

            return;
        }

        bool reordered =
            panelReorderer.TryReorderPanel(
                heldPanel,
                destinationPanel
            );

        if (!reordered)
        {
            Debug.Log(
                "[PANEL DEBUG] REORDER FAILED. " +
                "Panel was not placed."
            );

            return;
        }

        Debug.Log(
            "[PANEL DEBUG] REORDER SUCCESSFUL: " +
            heldPanel.PanelID
        );

        RestorePlayerAfterPanelMove();

        if (heldPanelContents != null)
        {
            heldPanelContents.RestoreAfterPanelMove();
        }

        heldPanelContents = null;

        // Keep the moved panel selected after reordering.
        ComicPanel reorderedPanel =
            heldPanel;

        heldPanel = null;
        carryingPlayer = false;

        // --------------------------------------------------------
        // Rebuild page lists so navigation uses the new slots.
        // --------------------------------------------------------

        BuildPageLists();

        // --------------------------------------------------------
        // Move every panel to its new slot.
        // --------------------------------------------------------

        LayoutAllPages();

        // --------------------------------------------------------
        // Refresh ComicMan's current panel.
        // --------------------------------------------------------

        SetCurrentPanel();

        // --------------------------------------------------------
        // Keep the reordered panel highlighted.
        // --------------------------------------------------------

        selectedPanel = reorderedPanel;

        UpdateSelectionVisual();

        PrintAllPanelStatus();
    }


    // ============================================================
    // CANCEL PANEL MOVEMENT
    // ============================================================

    /// <summary>
    /// Cancels the current panel movement and restores
    /// the panel to its original position and slot.
    /// </summary>
    private void CancelPanelMove()
    {
        if (heldPanel == null)
            return;

        Debug.Log(
            "[PANEL DEBUG] CANCEL PANEL MOVE: " +
            heldPanel.PanelID
        );

        // Restore original page and slot.
        heldPanel.SetPageIndex(
            originalPanelPage
        );

        heldPanel.SetSlotIndex(
            originalPanelSlot
        );

        heldPanel.transform.position =
            originalPanelPosition;

        // Restore player.
        RestorePlayerAfterPanelMove();

        // Restore panel contents.
        if (heldPanelContents != null)
        {
            heldPanelContents.RestoreAfterPanelMove();
        }

        heldPanelContents = null;

        // Remember which panel should remain selected.
        ComicPanel restoredPanel =
            heldPanel;

        heldPanel = null;

        carryingPlayer = false;

        // Rebuild page lists.
        BuildPageLists();

        // Restore layout.
        LayoutAllPages();

        // Keep the restored panel selected.
        selectedPanel = restoredPanel;

        // Refresh current panel.
        SetCurrentPanel();

        UpdateSelectionVisual();

        Debug.Log(
            "[PANEL DEBUG] PANEL MOVEMENT CANCELLED."
        );
    }


    // ============================================================
    // RESTORE PLAYER
    // ============================================================

    /// <summary>
    /// Restores ComicMan's Rigidbody2D state after
    /// a panel movement.
    /// </summary>
    private void RestorePlayerAfterPanelMove()
    {
        if (!carryingPlayer)
            return;

        if (playerBody == null)
            return;

        playerBody.bodyType =
            originalPlayerBodyType;

        playerBody.gravityScale =
            originalPlayerGravityScale;

        playerBody.linearVelocity =
            originalPlayerVelocity;

        playerBody.angularVelocity = 0f;

        carryingPlayer = false;

        Debug.Log(
            "[PANEL DEBUG] Player Rigidbody restored."
        );
    }


    // ============================================================
    // CARRY PLAYER WITH PANEL
    // ============================================================

    /// <summary>
    /// Keeps ComicMan at the same local offset from a panel
    /// while that panel is being moved.
    /// </summary>
    private void CarryPlayerWithPanel()
    {
        if (!carryingPlayer)
            return;

        if (heldPanel == null)
            return;

        if (playerBody == null)
            return;

        playerBody.position =
            heldPanel.transform.position +
            playerOffsetFromPanel;
    }


    // ============================================================
    // LATE UPDATE
    // ============================================================

    /// <summary>
    /// Updates player carrying and selection highlight.
    /// </summary>
    private void LateUpdate()
    {
        if (heldPanel != null && carryingPlayer)
        {
            CarryPlayerWithPanel();
        }

        if (artistMode)
        {
            UpdateSelectionVisual();
        }
    }


    // ============================================================
    // SELECTION HIGHLIGHT
    // ============================================================

    /// <summary>
    /// Positions the selection highlight around the currently
    /// selected panel.
    ///
    /// The highlight works regardless of whether the panel
    /// is movable.
    /// </summary>
    private void UpdateSelectionVisual()
    {
        if (selectionHighlight == null)
            return;

        if (!artistMode || selectedPanel == null)
        {
            selectionHighlight.gameObject.SetActive(false);
            return;
        }

        selectionHighlight.gameObject.SetActive(true);

        Bounds bounds =
            selectedPanel.GetWorldBounds();

        Vector3 center =
            bounds.center;

        Vector3 scale =
            new Vector3(
                bounds.size.x + selectionPadding,
                bounds.size.y + selectionPadding,
                selectionHighlight.localScale.z
            );

        selectionHighlight.position =
            new Vector3(
                center.x,
                center.y,
                selectionHighlight.position.z
            );

        selectionHighlight.localScale =
            scale;
    }


    // ============================================================
    // AUTOMATIC HOP
    // ============================================================

    /// <summary>
    /// Attempts to automatically hop ComicMan to the next
    /// panel when he exits the current panel.
    /// </summary>
    public void TryAutomaticHop(
        ComicPanel fromPanel,
        float direction)
    {
        if (fromPanel == null)
            return;

        if (Time.time <
            lastHopTime + hopLockDuration)
        {
            return;
        }

        Page page =
            fromPanel.PageIndex == 0
                ? leftPage
                : rightPage;

        if (page == null)
            return;

        if (page.panels == null ||
            page.panels.Count == 0)
        {
            return;
        }

        ComicPanel targetPanel =
            FindNextPanel(
                page,
                fromPanel,
                direction
            );

        if (targetPanel == null)
        {
            Debug.Log(
                "[PANEL DEBUG] No hop target found from " +
                fromPanel.PanelID
            );

            return;
        }

        HopToPanel(targetPanel);
    }


    // ============================================================
    // FIND NEXT PANEL
    // ============================================================

    /// <summary>
    /// Finds the next panel on the SAME page.
    /// </summary>
    private ComicPanel FindNextPanel(
        Page page,
        ComicPanel fromPanel,
        float direction)
    {
        if (page == null ||
            fromPanel == null)
        {
            return null;
        }

        int columns =
            Mathf.Max(1, page.columns);

        int rows =
            Mathf.Max(1, page.rows);

        int currentSlot =
            fromPanel.SlotIndex;

        int currentColumn =
            currentSlot % columns;

        int currentRow =
            currentSlot / columns;

        int targetColumn = currentColumn;
        int targetRow = currentRow;

        // --------------------------------------------------------
        // HOP RIGHT
        // --------------------------------------------------------
        // Normal case:
        //   1 -> 2
        //   2 -> 3
        //   4 -> 5
        //   5 -> 6
        //
        // At the end of a row, move to the FIRST panel of the
        // NEXT row:
        //   3 -> 4
        //   6 -> 7
        //
        // At the final panel of the page, do nothing for now.
        // Page-to-page hopping will be added later when the
        // required pin/game-object mechanic is implemented.
        // --------------------------------------------------------
        if (direction > 0)
        {
            if (currentColumn < columns - 1)
            {
                targetColumn = currentColumn + 1;
            }
            else
            {
                if (currentRow >= rows - 1)
                {
                    Debug.Log(
                        "[PANEL DEBUG] RIGHT EDGE OF PAGE REACHED FROM " +
                        fromPanel.PanelID +
                        ". Page-to-page hop is not implemented yet."
                    );

                    return null;
                }

                targetColumn = 0;
                targetRow = currentRow + 1;
            }
        }

        // --------------------------------------------------------
        // HOP LEFT
        // --------------------------------------------------------
        // Normal case:
        //   3 -> 2
        //   2 -> 1
        //   6 -> 5
        //   5 -> 4
        //
        // At the START of a row, move to the LAST panel of the
        // PREVIOUS row:
        //   4 -> 3
        //   7 -> 6
        //
        // At Panel 1 there is no previous panel, so return Panel 1
        // itself. HopToPanel() will therefore place ComicMan back
        // on the same panel's closest hop point.
        // --------------------------------------------------------
        else
        {
            if (currentColumn > 0)
            {
                targetColumn = currentColumn - 1;
            }
            else
            {
                if (currentRow <= 0)
                {
                    Debug.Log(
                        "[PANEL DEBUG] LEFT EDGE OF PAGE REACHED FROM " +
                        fromPanel.PanelID +
                        ". Returning to the same panel."
                    );

                    return fromPanel;
                }

                targetColumn = columns - 1;
                targetRow = currentRow - 1;
            }
        }

        int targetSlot =
            targetRow * columns +
            targetColumn;

        // If target slot somehow exceeds the page,
        // fail safely without changing ComicMan's position.
        if (targetSlot < 0 ||
            targetSlot >= page.panels.Count + 1)
        {
            return null;
        }

        foreach (ComicPanel panel in page.panels)
        {
            if (panel == null)
                continue;

            if (panel.SlotIndex == targetSlot)
            {
                Debug.Log(
                    "[PANEL DEBUG] HOP ROUTE: " +
                    fromPanel.PanelID +
                    " -> " +
                    panel.PanelID +
                    " | from slot " +
                    currentSlot +
                    " to slot " +
                    targetSlot
                );

                return panel;
            }
        }

        Debug.LogWarning(
            "[PANEL DEBUG] No panel exists at target slot " +
            targetSlot +
            " on the current page."
        );

        return null;
    }

    // ============================================================
    // HOP TO PANEL
    // ============================================================

    /// <summary>
    /// Moves ComicMan to the closest hop point of the target panel.
    /// </summary>
    // ============================================================
    // HOP TO PANEL
    // Moves ComicMan to the closest hop point of the target panel.
    // ============================================================
    private void HopToPanel(ComicPanel targetPanel)
    {
        if (targetPanel == null)
            return;

        if (playerBody == null)
            return;

        // GetClosestHopPoint returns a Transform.
        Transform hopPoint =
            targetPanel.GetClosestHopPoint(
                playerBody.position
            );

        if (hopPoint == null)
        {
            Debug.LogWarning(
                "[PANEL DEBUG] No hop point found on " +
                targetPanel.PanelID
            );

            return;
        }

        // Transform.position is a Vector3.
        playerBody.position = hopPoint.position;

        playerBody.linearVelocity =
            Vector2.zero;

        lastHopTime =
            Time.time;

        // Tell PanelManager that ComicMan is now inside
        // this panel.
        SetCurrentPanel(targetPanel);

        Debug.Log(
            "[PANEL DEBUG] HOPPED TO " +
            targetPanel.PanelID +
            " at world position " +
            hopPoint.position
        );
    }


    // ============================================================
    // PRINT MOVEMENT STATUS
    // ============================================================

    /// <summary>
    /// Prints the dynamic movement state of the previous
    /// and new current panels.
    /// </summary>
    private void PrintPanelMovementStatus(
        ComicPanel previousPanel,
        ComicPanel newCurrentPanel)
    {
        string previousStatus =
            "None";

        if (previousPanel != null)
        {
            previousStatus =
                previousPanel.PanelID +
                " | Inspector Movable = " +
                previousPanel.IsMovable +
                " | Final Movable = " +
                IsPanelMovable(previousPanel);
        }

        string currentStatus =
            "None";

        if (newCurrentPanel != null)
        {
            currentStatus =
                newCurrentPanel.PanelID +
                " | Inspector Movable = " +
                newCurrentPanel.IsMovable +
                " | Final Movable = " +
                IsPanelMovable(newCurrentPanel);
        }

        Debug.Log(
            "[PANEL DEBUG] MOVEMENT STATUS\n" +
            "Previous: " +
            previousStatus +
            "\nCurrent: " +
            currentStatus
        );
    }


    // ============================================================
    // PRINT ALL PANEL STATUS
    // ============================================================

    /// <summary>
    /// Prints every panel's page, slot, selection and
    /// movement state for debugging.
    /// </summary>
    private void PrintAllPanelStatus()
    {
        Debug.Log(
            "========== PANEL STATUS BEGIN =========="
        );

        PrintPageStatus(
            "LEFT",
            leftPage.panels
        );

        PrintPageStatus(
            "RIGHT",
            rightPage.panels
        );

        Debug.Log(
            "========== PANEL STATUS END =========="
        );
    }


    // ============================================================
    // PRINT PAGE STATUS
    // ============================================================

    /// <summary>
    /// Prints detailed information about all panels on one page.
    /// </summary>
    private void PrintPageStatus(
        string pageName,
        List<ComicPanel> panels)
    {
        if (panels == null)
            return;

        foreach (ComicPanel panel in panels)
        {
            if (panel == null)
                continue;

            Debug.Log(
                "[PANEL DEBUG] " +
                pageName +
                " | " +
                panel.PanelID +
                " | Page=" +
                panel.PageIndex +
                " | Slot=" +
                panel.SlotIndex +
                " | Selected=" +
                (panel == selectedPanel) +
                " | Current=" +
                (panel == currentPanel) +
                " | InspectorMovable=" +
                panel.IsMovable +
                " | FinalMovable=" +
                IsPanelMovable(panel)
            );
        }
    }


    // ============================================================
    // GIZMOS
    // ============================================================

    /// <summary>
    /// Draws page boundaries in the Unity Scene view.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        DrawPageGizmo(leftPage);
        DrawPageGizmo(rightPage);
    }


    // ============================================================
    // DRAW PAGE GIZMO
    // ============================================================

    /// <summary>
    /// Draws one page's BoxCollider2D bounds.
    /// </summary>
    private void DrawPageGizmo(Page page)
    {
        if (page == null ||
            page.pageArea == null)
        {
            return;
        }

        Bounds bounds =
            page.pageArea.bounds;

        Gizmos.DrawWireCube(
            bounds.center,
            bounds.size
        );
    }
}