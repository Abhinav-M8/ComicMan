using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls comic panel layout and Artist Mode panel manipulation.
///
/// Artist Mode:
/// WASD / Arrow Keys = select panel
/// E = choose / pick up / place panel
/// Escape = cancel panel movement
/// Q = handled by PlayerController
///
/// IMPORTANT:
/// Every ComicPanel remains in the page list.
/// A panel containing ComicMan is simply NOT allowed to move.
/// </summary>
public class PanelManager : MonoBehaviour
{
    // =========================================================
    // SINGLETON
    // =========================================================

    public static PanelManager Instance;

    // =========================================================
    // PAGE DATA
    // =========================================================

    [System.Serializable]
    public class Page
    {
        [Header("Page Area")]
        public BoxCollider2D pageArea;

        [Header("Grid")]
        public int columns = 2;
        public int rows = 2;

        [HideInInspector]
        public List<ComicPanel> panels = new List<ComicPanel>();
    }

    [Header("Pages")]
    [SerializeField] private Page leftPage;
    [SerializeField] private Page rightPage;

    // =========================================================
    // PLAYER REFERENCES
    // =========================================================

    [Header("Player")]
    [SerializeField] private Rigidbody2D playerBody;
    [SerializeField] private PlayerController playerController;

    // =========================================================
    // LAYOUT
    // =========================================================

    [Header("Panel Layout")]
    [SerializeField] private float horizontalSpacing = 0.5f;
    [SerializeField] private float verticalSpacing = 0.5f;
    [SerializeField] private float pagePadding = 0.1f;

    // =========================================================
    // ARTIST MODE
    // =========================================================

    [Header("Artist Mode")]
    [SerializeField] private bool allowArtistMode = true;

    [SerializeField] private Transform selectionHighlight;

    [SerializeField] private float selectionPadding = 0.1f;

    // =========================================================
    // HOP SETTINGS
    // =========================================================

    [Header("Hop")]
    [SerializeField] private float hopLockDuration = 0.15f;

    // =========================================================
    // CURRENT PANEL STATE
    // =========================================================

    private ComicPanel currentPanel;

    private ComicPanel selectedPanel;

    private ComicPanel chosenPanel;

    private ComicPanel heldPanel;

    // =========================================================
    // PANEL CONTENT STATE
    // =========================================================

    private PanelContents heldPanelContents;

    // =========================================================
    // ARTIST MODE STATE
    // =========================================================

    private bool artistMode;

    // =========================================================
    // PANEL MOVEMENT STATE
    // =========================================================

    private Vector3 originalPanelPosition;

    private int originalPageIndex;

    private int originalSlotIndex;

    // =========================================================
    // PLAYER CARRY STATE
    // =========================================================

    private bool carryingPlayer;

    private Vector3 playerOffsetFromPanel;

    private Vector3 originalPlayerPosition;

    private Vector2 originalPlayerVelocity;

    private float originalPlayerGravityScale;

    // =========================================================
    // HOP LOCK
    // =========================================================

    private float hopLockTimer;

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        // -----------------------------------------------------
        // Singleton setup
        // -----------------------------------------------------

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "PANEL MANAGER: Duplicate PanelManager found. Destroying duplicate."
            );

            Destroy(gameObject);
            return;
        }

        Instance = this;

        Debug.Log("=================================================");
        Debug.Log("PANEL MANAGER: AWAKE");
        Debug.Log("PanelManager.Instance assigned.");
        Debug.Log("=================================================");

        // -----------------------------------------------------
        // Automatic player references
        // -----------------------------------------------------

        if (playerBody == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player != null)
            {
                playerBody = player.GetComponent<Rigidbody2D>();
            }
        }

        if (playerController == null && playerBody != null)
        {
            playerController =
                playerBody.GetComponent<PlayerController>();
        }

        if (playerBody == null)
        {
            Debug.LogWarning(
                "PANEL MANAGER: Player Rigidbody2D not found."
            );
        }
    }

    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        Debug.Log("=================================================");
        Debug.Log("PANEL MANAGER: START");
        Debug.Log("=================================================");

        // Find EVERY panel.
        BuildPageLists();

        // Arrange panels.
        LayoutAllPages();

        // Find the panel containing ComicMan.
        SetCurrentPanel();

        // Artist Mode starts OFF.
        artistMode = false;

        // Nothing is selected initially.
        selectedPanel = null;
        chosenPanel = null;
        heldPanel = null;

        UpdateSelectionVisual();

        PrintPageContents();

        Debug.Log("PANEL MANAGER: START COMPLETE.");
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (hopLockTimer > 0f)
        {
            hopLockTimer -= Time.deltaTime;
        }

        if (!artistMode)
            return;

        HandleArtistModeInput();
    }

    // =========================================================
    // LATE UPDATE
    // =========================================================

    private void LateUpdate()
    {
        // If a panel is being carried and ComicMan is attached,
        // keep ComicMan at the same offset.
        if (heldPanel != null && carryingPlayer)
        {
            CarryPlayerWithPanel();
        }
    }

    // =========================================================
    // BUILD PAGE LISTS
    // =========================================================

    private void BuildPageLists()
    {
        leftPage.panels.Clear();
        rightPage.panels.Clear();

        ComicPanel[] allPanels = FindObjectsByType<ComicPanel>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        Debug.Log("=================================================");
        Debug.Log("PANEL MANAGER: BUILDING PAGE LISTS");
        Debug.Log("TOTAL PANELS FOUND = " + allPanels.Length);

        foreach (ComicPanel panel in allPanels)
        {
            if (panel == null)
                continue;

            // -------------------------------------------------
            // IMPORTANT:
            //
            // DO NOT remove the panel containing ComicMan.
            //
            // It remains part of the page layout.
            // We simply prevent that panel from being moved.
            // -------------------------------------------------

            if (panel.PageIndex == 0)
            {
                leftPage.panels.Add(panel);

                Debug.Log(
                    "LEFT PAGE <- " +
                    panel.name +
                    " | Slot = " +
                    panel.SlotIndex
                );
            }
            else
            {
                rightPage.panels.Add(panel);

                Debug.Log(
                    "RIGHT PAGE <- " +
                    panel.name +
                    " | Slot = " +
                    panel.SlotIndex
                );
            }
        }

        // Sort panels by slot.
        leftPage.panels.Sort(
            (a, b) => a.SlotIndex.CompareTo(b.SlotIndex)
        );

        rightPage.panels.Sort(
            (a, b) => a.SlotIndex.CompareTo(b.SlotIndex)
        );

        Debug.Log(
            "LEFT PAGE COUNT = " +
            leftPage.panels.Count
        );

        Debug.Log(
            "RIGHT PAGE COUNT = " +
            rightPage.panels.Count
        );

        Debug.Log("=================================================");
    }

    // =========================================================
    // FIND CURRENT PANEL
    // =========================================================

    private void SetCurrentPanel()
    {
        if (playerBody == null)
        {
            Debug.LogWarning(
                "PANEL MANAGER: Player Body is NULL."
            );

            return;
        }

        Vector2 playerPosition = playerBody.position;

        ComicPanel[] allPanels = FindObjectsByType<ComicPanel>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (ComicPanel panel in allPanels)
        {
            if (panel == null)
                continue;

            Bounds panelBounds = panel.GetWorldBounds();

            if (panelBounds.Contains(playerPosition))
            {
                currentPanel = panel;

                Debug.Log(
                    "CURRENT PANEL = " +
                    panel.name +
                    " | Page = " +
                    panel.PageIndex +
                    " | Slot = " +
                    panel.SlotIndex
                );

                return;
            }
        }

        Debug.LogWarning(
            "PANEL MANAGER: Could not find ComicMan's current panel."
        );
    }

    // =========================================================
    // PANEL TRIGGER VERSION
    // =========================================================

    public void SetCurrentPanel(ComicPanel panel)
    {
        if (panel == null)
            return;

        currentPanel = panel;

        Debug.Log(
            "CURRENT PANEL UPDATED = " +
            panel.name
        );
    }

    // =========================================================
    // ARTIST MODE
    // =========================================================

    public void SetArtistMode(bool enabled)
    {
        if (!allowArtistMode)
            return;

        artistMode = enabled;

        Debug.Log("=================================================");
        Debug.Log("PANEL MANAGER: ARTIST MODE = " + artistMode);
        Debug.Log("=================================================");

        if (artistMode)
        {
            // -------------------------------------------------
            // ENTER ARTIST MODE
            // -------------------------------------------------

            // Refresh current panel.
            SetCurrentPanel();

            // Start by selecting ComicMan's current panel.
            selectedPanel = currentPanel;

            if (selectedPanel != null)
            {
                Debug.Log(
                    "CURRENT PANEL = " +
                    selectedPanel.name
                );
            }

            // -------------------------------------------------
            // If ComicMan's panel cannot move,
            // find another movable panel.
            // -------------------------------------------------

            if (
                selectedPanel == null ||
                !IsPanelMovable(selectedPanel)
            )
            {
                Debug.Log(
                    "CURRENT PANEL CANNOT BE MOVED."
                );

                selectedPanel = FindFirstMovablePanel();
            }

            if (selectedPanel != null)
            {
                Debug.Log(
                    "SELECTED PANEL = " +
                    selectedPanel.name
                );
            }
            else
            {
                Debug.LogWarning(
                    "ARTIST MODE: NO MOVABLE PANELS FOUND."
                );
            }

            UpdateSelectionVisual();
        }
        else
        {
            // -------------------------------------------------
            // EXIT ARTIST MODE
            // -------------------------------------------------

            // Do not leave a panel half-held.
            if (heldPanel != null)
            {
                Debug.Log(
                    "ARTIST MODE EXITED WHILE PANEL WAS HELD."
                );

                CancelPanelMove();
            }

            chosenPanel = null;

            UpdateSelectionVisual();

            Debug.Log(
                "ARTIST MODE EXITED."
            );
        }
    }

    // =========================================================
    // FIND FIRST MOVABLE PANEL
    // =========================================================

    private ComicPanel FindFirstMovablePanel()
    {
        // Search left page first.
        foreach (ComicPanel panel in leftPage.panels)
        {
            if (panel != null && IsPanelMovable(panel))
            {
                return panel;
            }
        }

        // Then search right page.
        foreach (ComicPanel panel in rightPage.panels)
        {
            if (panel != null && IsPanelMovable(panel))
            {
                return panel;
            }
        }

        return null;
    }

    // =========================================================
    // CHECK WHETHER PANEL CAN MOVE
    // =========================================================

    private bool IsPanelMovable(ComicPanel panel)
    {
        if (panel == null)
            return false;

        // Panel's own Inspector setting.
        if (!panel.IsMovable)
            return false;

        // -----------------------------------------------------
        // IMPORTANT:
        // The panel containing ComicMan cannot be moved.
        // -----------------------------------------------------

        if (panel == currentPanel)
            return false;

        return true;
    }

    // =========================================================
    // ARTIST MODE INPUT
    // =========================================================

    private void HandleArtistModeInput()
    {
        if (Keyboard.current == null)
            return;

        // Escape cancels a panel move.
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (heldPanel != null)
            {
                CancelPanelMove();
            }

            return;
        }

        // E = choose / pick up / place.
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            HandleArtistE();
            return;
        }

        // Movement keys select panels.
        int horizontal = 0;
        int vertical = 0;

        if (
            Keyboard.current.aKey.wasPressedThisFrame ||
            Keyboard.current.leftArrowKey.wasPressedThisFrame
        )
        {
            horizontal = -1;
        }

        if (
            Keyboard.current.dKey.wasPressedThisFrame ||
            Keyboard.current.rightArrowKey.wasPressedThisFrame
        )
        {
            horizontal = 1;
        }

        if (
            Keyboard.current.wKey.wasPressedThisFrame ||
            Keyboard.current.upArrowKey.wasPressedThisFrame
        )
        {
            vertical = 1;
        }

        if (
            Keyboard.current.sKey.wasPressedThisFrame ||
            Keyboard.current.downArrowKey.wasPressedThisFrame
        )
        {
            vertical = -1;
        }

        if (horizontal != 0 || vertical != 0)
        {
            NavigateSelection(horizontal, vertical);
        }
    }

    // =========================================================
    // E IN ARTIST MODE
    // =========================================================

    private void HandleArtistE()
    {
        if (selectedPanel == null)
        {
            Debug.LogWarning(
                "ARTIST MODE: No panel selected."
            );

            return;
        }

        // -----------------------------------------------------
        // If already holding a panel:
        // E = place it.
        // -----------------------------------------------------

        if (heldPanel != null)
        {
            PlaceHeldPanel();
            return;
        }

        // -----------------------------------------------------
        // Otherwise:
        // E = choose the selected panel.
        // -----------------------------------------------------

        chosenPanel = selectedPanel;

        Debug.Log("=================================================");
        Debug.Log(
            "CHOSEN PANEL = " +
            chosenPanel.name
        );

        Debug.Log(
            "PAGE = " +
            chosenPanel.PageIndex
        );

        Debug.Log(
            "SLOT = " +
            chosenPanel.SlotIndex
        );

        Debug.Log(
            "MOVABLE = " +
            IsPanelMovable(chosenPanel)
        );

        Debug.Log("=================================================");

        // Cannot move ComicMan's panel.
        if (!IsPanelMovable(chosenPanel))
        {
            Debug.LogWarning(
                "CHOSEN PANEL CANNOT BE MOVED."
            );

            return;
        }

        PickUpSelectedPanel();
    }

    // =========================================================
    // NAVIGATE PANEL SELECTION
    // =========================================================

    private void NavigateSelection(
        int horizontal,
        int vertical
    )
    {
        if (selectedPanel == null)
            return;

        Page page = GetPageForPanel(selectedPanel);

        if (page == null || page.panels.Count == 0)
            return;

        int columns = Mathf.Max(1, page.columns);
        int rows = Mathf.Max(1, page.rows);

        int currentSlot = selectedPanel.SlotIndex;

        int currentColumn = currentSlot % columns;
        int currentRow = currentSlot / columns;

        currentColumn += horizontal;
        currentRow += vertical;

        // Horizontal wrap.
        if (currentColumn < 0)
            currentColumn = columns - 1;

        if (currentColumn >= columns)
            currentColumn = 0;

        // Vertical wrap.
        if (currentRow < 0)
            currentRow = rows - 1;

        if (currentRow >= rows)
            currentRow = 0;

        int targetSlot =
            currentRow * columns +
            currentColumn;

        // Find panel in that slot.
        ComicPanel targetPanel = null;

        foreach (ComicPanel panel in page.panels)
        {
            if (
                panel != null &&
                panel.SlotIndex == targetSlot
            )
            {
                targetPanel = panel;
                break;
            }
        }

        if (targetPanel == null)
        {
            Debug.LogWarning(
                "ARTIST MODE: No panel found at slot " +
                targetSlot
            );

            return;
        }

        // Selection is allowed to land on the
        // ComicMan panel, but E will refuse to move it.
        selectedPanel = targetPanel;

        Debug.Log(
            "SELECTED PANEL = " +
            selectedPanel.name +
            " | Page = " +
            selectedPanel.PageIndex +
            " | Slot = " +
            selectedPanel.SlotIndex +
            " | Movable = " +
            IsPanelMovable(selectedPanel)
        );

        UpdateSelectionVisual();
    }

    // =========================================================
    // PICK UP PANEL
    // =========================================================

    private void PickUpSelectedPanel()
    {
        if (selectedPanel == null)
            return;

        if (!IsPanelMovable(selectedPanel))
        {
            Debug.LogWarning(
                "PANEL MANAGER: Attempted to pick up immovable panel."
            );

            return;
        }

        heldPanel = selectedPanel;

        originalPanelPosition =
            heldPanel.transform.position;

        originalPageIndex =
            heldPanel.PageIndex;

        originalSlotIndex =
            heldPanel.SlotIndex;

        // -----------------------------------------------------
        // Handle panel contents.
        // -----------------------------------------------------

        heldPanelContents =
            heldPanel.GetComponent<PanelContents>();

        if (heldPanelContents != null)
        {
            heldPanelContents.PrepareForPanelMove();
        }

        // -----------------------------------------------------
        // Carry player only if player happens to be inside.
        // Normally this should be false because ComicMan's
        // panel is immovable.
        // -----------------------------------------------------

        if (playerBody != null)
        {
            Bounds panelBounds =
                heldPanel.GetWorldBounds();

            if (panelBounds.Contains(playerBody.position))
            {
                carryingPlayer = true;

                originalPlayerPosition =
                    playerBody.position;

                originalPlayerVelocity =
                    playerBody.linearVelocity;

                originalPlayerGravityScale =
                    playerBody.gravityScale;

                playerOffsetFromPanel =
                (Vector3)playerBody.position -
                 heldPanel.transform.position;

                playerBody.linearVelocity = Vector2.zero;
                playerBody.gravityScale = 0f;

                Debug.Log(
                    "PANEL MANAGER: ComicMan is being carried."
                );
            }
            else
            {
                carryingPlayer = false;
            }
        }

        Debug.Log("=================================================");
        Debug.Log(
            "PANEL PICKED UP = " +
            heldPanel.name
        );
        Debug.Log("=================================================");
    }

    // =========================================================
    // CARRY PLAYER
    // =========================================================

    private void CarryPlayerWithPanel()
    {
        if (
            playerBody == null ||
            heldPanel == null
        )
        {
            return;
        }

        playerBody.position =
            heldPanel.transform.position +
            playerOffsetFromPanel;
    }

    // =========================================================
    // PLACE PANEL
    // =========================================================

    private void PlaceHeldPanel()
    {
        if (heldPanel == null)
            return;

        Debug.Log(
            "PLACING PANEL = " +
            heldPanel.name
        );

        // Restore panel contents.
        if (heldPanelContents != null)
        {
            heldPanelContents.RestoreAfterPanelMove();
        }

        // Restore player if necessary.
        RestoreCarriedPlayer();

        heldPanelContents = null;
        heldPanel = null;
        chosenPanel = null;
        carryingPlayer = false;

        // Re-layout everything.
        LayoutAllPages();

        // Refresh current panel.
        SetCurrentPanel();

        // Update highlight.
        UpdateSelectionVisual();

        PrintPageContents();

        Debug.Log("PANEL PLACED.");
    }

    // =========================================================
    // CANCEL PANEL MOVE
    // =========================================================

    private void CancelPanelMove()
    {
        if (heldPanel == null)
            return;

        Debug.Log(
            "CANCEL PANEL MOVE = " +
            heldPanel.name
        );

        // Restore original page/slot.
        heldPanel.SetPageIndex(
            originalPageIndex
        );

        heldPanel.SetSlotIndex(
            originalSlotIndex
        );

        heldPanel.transform.position =
            originalPanelPosition;

        // Restore contents.
        if (heldPanelContents != null)
        {
            heldPanelContents.RestoreAfterPanelMove();
        }

        // Restore player.
        RestoreCarriedPlayer();

        heldPanelContents = null;
        heldPanel = null;
        chosenPanel = null;
        carryingPlayer = false;

        // Rebuild page lists.
        BuildPageLists();

        LayoutAllPages();

        // Re-select the restored panel.
        selectedPanel = FindPanelByPageAndSlot(
            originalPageIndex,
            originalSlotIndex
        );

        UpdateSelectionVisual();

        Debug.Log(
            "PANEL MOVE CANCELLED."
        );
    }

    // =========================================================
    // RESTORE PLAYER
    // =========================================================

    private void RestoreCarriedPlayer()
    {
        if (!carryingPlayer)
            return;

        if (playerBody == null)
            return;

        playerBody.position =
            originalPlayerPosition;

        playerBody.linearVelocity =
            originalPlayerVelocity;

        playerBody.gravityScale =
            originalPlayerGravityScale;

        carryingPlayer = false;
    }

    // =========================================================
    // GET PAGE FOR PANEL
    // =========================================================

    private Page GetPageForPanel(ComicPanel panel)
    {
        if (panel == null)
            return null;

        if (panel.PageIndex == 0)
            return leftPage;

        return rightPage;
    }

    // =========================================================
    // FIND PANEL BY PAGE + SLOT
    // =========================================================

    private ComicPanel FindPanelByPageAndSlot(
        int pageIndex,
        int slotIndex
    )
    {
        Page page =
            pageIndex == 0
            ? leftPage
            : rightPage;

        foreach (ComicPanel panel in page.panels)
        {
            if (
                panel != null &&
                panel.SlotIndex == slotIndex
            )
            {
                return panel;
            }
        }

        return null;
    }

    // =========================================================
    // LAYOUT ALL PAGES
    // =========================================================

    private void LayoutAllPages()
    {
        LayoutPage(leftPage);
        LayoutPage(rightPage);
    }

    // =========================================================
    // LAYOUT ONE PAGE
    // =========================================================

    private void LayoutPage(Page page)
    {
        if (page == null)
            return;

        if (page.pageArea == null)
        {
            Debug.LogWarning(
                "PANEL MANAGER: Page Area is missing."
            );

            return;
        }

        int maxPanels =
            page.columns * page.rows;

        int count =
            Mathf.Min(
                page.panels.Count,
                maxPanels
            );

        for (int i = 0; i < count; i++)
        {
            ComicPanel panel =
                page.panels[i];

            if (panel == null)
                continue;

            panel.SetSlotIndex(i);

            panel.transform.position =
                GetSlotPosition(page, i);
        }
    }

    // =========================================================
    // GET SLOT POSITION
    // =========================================================

    private Vector3 GetSlotPosition(
        Page page,
        int slotIndex
    )
    {
        Bounds bounds =
            page.pageArea.bounds;

        int columns =
            Mathf.Max(1, page.columns);

        int row =
            slotIndex / columns;

        int column =
            slotIndex % columns;

        int totalColumns =
            Mathf.Max(1, page.columns);

        int totalRows =
            Mathf.Max(1, page.rows);

        float usableWidth =
            bounds.size.x -
            pagePadding * 2f;

        float usableHeight =
            bounds.size.y -
            pagePadding * 2f;

        float cellWidth =
            usableWidth / totalColumns;

        float cellHeight =
            usableHeight / totalRows;

        float x =
            bounds.min.x +
            pagePadding +
            cellWidth * 0.5f +
            column * cellWidth;

        float y =
            bounds.max.y -
            pagePadding -
            cellHeight * 0.5f -
            row * cellHeight;

        return new Vector3(
            x,
            y,
            0f
        );
    }

    // =========================================================
    // UPDATE SELECTION HIGHLIGHT
    // =========================================================

    private void UpdateSelectionVisual()
    {
        if (selectionHighlight == null)
            return;

        if (
            !artistMode ||
            selectedPanel == null
        )
        {
            selectionHighlight.gameObject.SetActive(false);
            return;
        }

        selectionHighlight.gameObject.SetActive(true);

        Bounds bounds =
            selectedPanel.GetWorldBounds();

        selectionHighlight.position =
            bounds.center;

        selectionHighlight.localScale =
            new Vector3(
                bounds.size.x + selectionPadding,
                bounds.size.y + selectionPadding,
                1f
            );

        Debug.Log(
            "HIGHLIGHT = " +
            selectedPanel.name
        );
    }

    // =========================================================
    // PRINT PAGE CONTENTS
    // =========================================================

    private void PrintPageContents()
    {
        Debug.Log("=================================================");
        Debug.Log("PAGE CONTENTS");

        PrintPage("LEFT PAGE", leftPage);
        PrintPage("RIGHT PAGE", rightPage);

        Debug.Log("=================================================");
    }

    // =========================================================
    // PRINT ONE PAGE
    // =========================================================

    private void PrintPage(
        string pageName,
        Page page
    )
    {
        Debug.Log(pageName);

        if (page == null)
            return;

        foreach (ComicPanel panel in page.panels)
        {
            if (panel == null)
                continue;

            Debug.Log(
                "Slot " +
                panel.SlotIndex +
                " = " +
                panel.name
            );
        }
    }

    // =========================================================
    // GET PANEL NAME
    // =========================================================

    private string GetPanelName(ComicPanel panel)
    {
        if (panel == null)
            return "NULL";

        return panel.name;
    }

    // =========================================================
    // AUTOMATIC HOP
    // =========================================================

    public void TryAutomaticHop(
        ComicPanel fromPanel,
        float direction
    )
    {
        if (fromPanel == null)
            return;

        if (hopLockTimer > 0f)
            return;

        Page page =
            GetPageForPanel(fromPanel);

        if (page == null)
            return;

        if (page.panels.Count == 0)
            return;

        int currentSlot =
            fromPanel.SlotIndex;

        int columns =
            Mathf.Max(1, page.columns);

        int row =
            currentSlot / columns;

        int column =
            currentSlot % columns;

        if (direction > 0)
            column++;
        else
            column--;

        // Horizontal wrapping.
        if (column < 0)
            column = columns - 1;

        if (column >= columns)
            column = 0;

        int targetSlot =
            row * columns +
            column;

        ComicPanel target =
            FindPanelByPageAndSlot(
                fromPanel.PageIndex,
                targetSlot
            );

        if (target == null)
            return;

        HopToPanel(target);

        hopLockTimer =
            hopLockDuration;
    }

    // =========================================================
    // HOP TO PANEL
    // =========================================================

    private void HopToPanel(ComicPanel target)
    {
        if (
            playerBody == null ||
            target == null
        )
        {
            return;
        }

        Transform hopPoint =
            target.GetClosestHopPoint(
                playerBody.position
            );

        if (hopPoint != null)
        {
            playerBody.position =
                hopPoint.position;

            Debug.Log(
                "Hopped to " +
                target.name +
                " at world position " +
                hopPoint.position
            );
        }
        else
        {
            Debug.LogWarning(
                "No hop point found on " +
                target.name
            );
        }

        currentPanel = target;
    }

    // =========================================================
    // PAGE GIZMOS
    // =========================================================

    private void OnDrawGizmos()
    {
        DrawPageGizmos(leftPage);
        DrawPageGizmos(rightPage);
    }

    // =========================================================
    // DRAW PAGE GIZMOS
    // =========================================================

    private void DrawPageGizmos(Page page)
    {
        if (page == null)
            return;

        if (page.pageArea == null)
            return;

        Bounds bounds =
            page.pageArea.bounds;

        Gizmos.DrawWireCube(
            bounds.center,
            bounds.size
        );
    }
}