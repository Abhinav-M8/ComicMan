using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PanelManager : MonoBehaviour
{
    // ============================================================
    // SINGLETON
    // Allows other scripts to access PanelManager.Instance.
    // ============================================================

    public static PanelManager Instance { get; private set; }


    // ============================================================
    // PAGE DATA
    // Each page has its own bounds and panel list.
    // ============================================================

    [System.Serializable]
    public class Page
    {
        [Header("Page Bounds")]

        public BoxCollider2D pageArea;


        [Header("Grid")]

        // Number of panel columns.
        public int columns = 2;

        // Number of panel rows.
        public int rows = 2;


        // Runtime list of panels belonging to this page.
        [HideInInspector]
        public List<ComicPanel> panels =
            new List<ComicPanel>();
    }


    // ============================================================
    // PLAYER REFERENCES
    // ============================================================

    [Header("Player")]

    // ComicMan Rigidbody2D.
    [SerializeField] private Rigidbody2D playerBody;

    // ComicMan controller.
    [SerializeField] private PlayerController playerController;


    // ============================================================
    // PAGES
    // ============================================================

    [Header("Pages")]

    // Left side of the book.
    [SerializeField] private Page leftPage;

    // Right side of the book.
    [SerializeField] private Page rightPage;


    // ============================================================
    // PANEL SPACING
    // ============================================================

    [Header("Panel Layout")]

    // Horizontal distance between slots.
    [SerializeField] private float horizontalSpacing = 0.2f;

    // Vertical distance between slots.
    [SerializeField] private float verticalSpacing = 0.2f;

    // Padding from page edges.
    [SerializeField] private float pagePadding = 0.2f;


    // ============================================================
    // ARTIST MODE
    // ============================================================

    [Header("Artist Mode")]

    // Allows Artist Mode interaction.
    [SerializeField] private bool allowArtistMode = true;


    // ============================================================
    // SELECTION HIGHLIGHT
    // ============================================================

    [Header("Selection Highlight")]

    // Optional visual object following selected panel.
    [SerializeField] private Transform selectionHighlight;

    // Extra size around the panel.
    [SerializeField] private float selectionPadding = 0.1f;


    // ============================================================
    // AUTOMATIC HOP
    // ============================================================

    [Header("Automatic Hop")]

    // Minimum horizontal velocity required to hop.
    [SerializeField] private float hopHorizontalThreshold = 0.5f;

    // Prevents repeated hops during one transition.
    [SerializeField] private float hopLockDuration = 0.2f;


    // ============================================================
    // RUNTIME STATE
    // ============================================================

    // Panel ComicMan currently occupies.
    private ComicPanel currentPanel;

    // Panel currently selected in Artist Mode.
    private ComicPanel selectedPanel;

    // Panel currently being carried.
    private ComicPanel heldPanel;

    // Whether Artist Mode is active.
    private bool artistMode;

    // Whether ComicMan is being carried with a panel.
    private bool carryingPlayer;

    // ComicMan's position relative to the panel.
    private Vector3 playerOffsetFromPanel;


    // ============================================================
    // MOVE STATE
    // ============================================================

    // Original panel position before movement.
    private Vector3 originalPanelPosition;

    // Original player position before movement.
    private Vector3 originalPlayerPosition;

    // Original player velocity.
    private Vector2 originalPlayerVelocity;

    // Original panel page.
    private int originalPageIndex;

    // Original panel slot.
    private int originalSlotIndex;

    // Current intended slot while moving.
    private int targetSlotIndex;
    private PanelContents heldPanelContents;

    // ============================================================
    // PHYSICS STATE
    // ============================================================

    // Original gravity scale.
    private float originalPlayerGravityScale;


    // ============================================================
    // HOP LOCK
    // ============================================================

    private float hopLockTimer;


    // ============================================================
    // AWAKE
    // Creates the singleton and prepares panels.
    // ============================================================

    private void Awake()
    {
        // Ensure only one PanelManager exists.
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Build the page lists.
        FindPanels();
    }


    // ============================================================
    // START
    // Stores player's original gravity.
    // ============================================================

    private void Start()
    {
        if (playerBody != null)
        {
            originalPlayerGravityScale =
                playerBody.gravityScale;
        }

        // Build the pages again after all objects initialize.
        BuildPageLists();
    }


    // ============================================================
    // UPDATE
    // Handles Artist Mode and visual selection.
    // ============================================================

    private void Update()
    {
        // Update selected panel highlight.
        UpdateSelectionVisual();

        // Artist Mode input.
        if (artistMode)
        {
            HandleArtistModeInput();
        }

        // Count down hop lock.
        if (hopLockTimer > 0f)
        {
            hopLockTimer -= Time.deltaTime;
        }
    }


    // ============================================================
    // LATE UPDATE
    // Keeps ComicMan attached to a moving panel.
    // ============================================================

    private void LateUpdate()
    {
        if (!carryingPlayer)
            return;

        if (heldPanel == null)
            return;

        if (playerBody == null)
            return;

        // Keep ComicMan at the same local offset.
        playerBody.position =
            heldPanel.transform.position +
            playerOffsetFromPanel;

        // Stop physics from moving ComicMan independently.
        playerBody.linearVelocity =
            Vector2.zero;
    }


    // ============================================================
    // FIND PANELS
    // Finds all ComicPanel objects in the scene.
    // ============================================================

    private void FindPanels()
    {
        BuildPageLists();
    }


    // ============================================================
    // BUILD PAGE LISTS
    // Sorts every panel into its assigned page.
    // ============================================================

    private void BuildPageLists()
    {
        if (leftPage == null ||
            rightPage == null)
        {
            return;
        }

        // Clear previous lists.
        leftPage.panels.Clear();
        rightPage.panels.Clear();

        // Find all panels in the scene.
        ComicPanel[] panels =
            FindObjectsByType<ComicPanel>(
                FindObjectsSortMode.None
            );

        // Add each panel to its assigned page.
        foreach (ComicPanel panel in panels)
        {
            if (panel == null)
                continue;

            if (panel.PageIndex == 0)
            {
                leftPage.panels.Add(panel);
            }
            else
            {
                rightPage.panels.Add(panel);
            }
        }

        // Sort each page by slot index.
        SortPage(leftPage);
        SortPage(rightPage);

        // Position panels according to their slots.
        LayoutPage(leftPage);
        LayoutPage(rightPage);


        // ==================== DEBUG START ====================
        PrintPageContents(leftPage, "LEFT PAGE");
        PrintPageContents(rightPage, "RIGHT PAGE");
        // ===================== DEBUG END =====================
    }


    // ============================================================
    // SORT PAGE
    // Orders panels according to their slot indices.
    // ============================================================

    private void SortPage(Page page)
    {
        page.panels.Sort(
            (a, b) =>
                a.SlotIndex.CompareTo(
                    b.SlotIndex
                )
        );
    }


    // ============================================================
    // SET ARTIST MODE
    // Called by PlayerController when Q is pressed.
    // ============================================================

    public void SetArtistMode(bool enabled)
    {
        if (!allowArtistMode)
            enabled = false;

        artistMode = enabled;

        if (!artistMode)
        {
            // Cancel an unfinished panel movement.
            if (heldPanel != null)
            {
                CancelPanelMove();
            }

            // Hide selection highlight.
            if (selectionHighlight != null)
            {
                selectionHighlight.gameObject
                    .SetActive(false);
            }

            return;
        }

        // Select current panel when entering Artist Mode.
        if (currentPanel != null)
        {
            selectedPanel = currentPanel;
        }
        else
        {
            // Otherwise select the first panel.
            selectedPanel =
                GetFirstPanel();
        }

        // ==================== DEBUG START ====================
        Debug.Log(
            $"[PanelManager] Artist Mode = {artistMode}"
        );
        // ===================== DEBUG END =====================
    }


    // ============================================================
    // HANDLE ARTIST MODE INPUT
    // E = pickup/place.
    // Esc = cancel.
    // WASD/Arrows = navigation.
    // ============================================================

    private void HandleArtistModeInput()
    {
        if (Keyboard.current == null)
            return;

        // --------------------------------------------------------
        // ESCAPE
        // Cancel current movement.
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
        // E
        // Pick up selected panel OR place held panel.
        // --------------------------------------------------------

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (heldPanel == null)
            {
                PickUpSelectedPanel();
            }
            else
            {
                PlaceHeldPanel();
            }

            return;
        }


        // --------------------------------------------------------
        // NAVIGATION
        // Only navigate when not currently carrying a panel.
        // --------------------------------------------------------

        if (heldPanel != null)
        {
            MoveHeldPanel();
            return;
        }

        // A / Left = previous panel.
        if (Keyboard.current.aKey.wasPressedThisFrame ||
            Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            NavigateSelection(-1, 0);
        }

        // D / Right = next panel.
        if (Keyboard.current.dKey.wasPressedThisFrame ||
            Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            NavigateSelection(1, 0);
        }

        // W / Up = panel above.
        if (Keyboard.current.wKey.wasPressedThisFrame ||
            Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            NavigateSelection(0, 1);
        }

        // S / Down = panel below.
        if (Keyboard.current.sKey.wasPressedThisFrame ||
            Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            NavigateSelection(0, -1);
        }
    }


    // ============================================================
    // NAVIGATE SELECTION
    // Moves selection through the fixed panel grid.
    // ============================================================

    private void NavigateSelection(
        int horizontal,
        int vertical)
    {
        if (selectedPanel == null)
            return;

        Page page =
            GetPage(selectedPanel.PageIndex);

        if (page == null)
            return;

        int currentIndex =
            page.panels.IndexOf(selectedPanel);

        if (currentIndex < 0)
            return;

        int columns =
            Mathf.Max(1, page.columns);

        int rows =
            Mathf.Max(1, page.rows);

        int row =
            currentIndex / columns;

        int column =
            currentIndex % columns;

        // Horizontal navigation.
        if (horizontal != 0)
        {
            column += horizontal;

            // Wrap to other side of same row.
            if (column < 0)
            {
                column = columns - 1;
            }
            else if (column >= columns)
            {
                column = 0;
            }
        }

        // Vertical navigation.
        if (vertical != 0)
        {
            row += vertical;

            // Wrap vertically.
            if (row < 0)
            {
                row = rows - 1;
            }
            else if (row >= rows)
            {
                row = 0;
            }
        }

        int targetIndex =
            row * columns + column;

        // Prevent invalid index.
        if (targetIndex < 0 ||
            targetIndex >= page.panels.Count)
        {
            return;
        }

        selectedPanel =
            page.panels[targetIndex];


        // ==================== DEBUG START ====================
        Debug.Log(
            $"[PanelManager] Selected " +
            $"{selectedPanel.PanelID}"
        );
        // ===================== DEBUG END =====================
    }


    // ============================================================
    // PICK UP SELECTED PANEL
    // Begins moving a panel.
    // ============================================================

    private void PickUpSelectedPanel()
    {
        if (selectedPanel == null)
            return;

        // Cannot move panels marked as immovable.
        if (!selectedPanel.IsMovable)
            return;

        // --------------------------------------------------------
        // IMPORTANT RULE:
        // ComicMan cannot move a panel he is currently standing in.
        // --------------------------------------------------------

        if (IsPlayerOnPanel(selectedPanel))
        {
            // ==================== DEBUG START ====================
            Debug.Log(
                $"[PanelManager] Cannot move " +
                $"{selectedPanel.PanelID}: " +
                "ComicMan is inside it."
            );
            // ===================== DEBUG END =====================

            return;
        }

        // Store original information.
        originalPanelPosition =
            selectedPanel.transform.position;

        originalPageIndex =
            selectedPanel.PageIndex;

        originalSlotIndex =
            selectedPanel.SlotIndex;

        targetSlotIndex =
            originalSlotIndex;

        // Store player information.
        if (playerBody != null)
        {
            originalPlayerPosition =
                playerBody.position;

            originalPlayerVelocity =
                playerBody.linearVelocity;
        }

        // Store the selected panel.
        heldPanel = selectedPanel;
        // ==================== PANEL CONTENTS ====================

        heldPanelContents = heldPanel.GetComponent<PanelContents>();

        if (heldPanelContents != null)
        {
            heldPanelContents.PrepareForPanelMove();
        }

        // ===================== PANEL CONTENTS END =====================

        // Remember player's offset from the panel.
        if (playerBody != null)
        {
            playerOffsetFromPanel =
                playerBody.transform.position -
                heldPanel.transform.position;
        }

        // Prevent player gravity while carrying.
        if (playerBody != null)
        {
            playerBody.gravityScale = 0f;
            playerBody.linearVelocity = Vector2.zero;
        }

        // Carry ComicMan only if he is actually inside the panel.
        carryingPlayer =
            IsPlayerOnPanel(heldPanel);


        // ==================== DEBUG START ====================
        Debug.Log(
            $"[PanelManager] Picked up " +
            $"{heldPanel.PanelID}. " +
            $"PlayerCarried={carryingPlayer}"
        );
        // ===================== DEBUG END =====================
    }


    // ============================================================
    // MOVE HELD PANEL
    // Changes the panel's target slot while carrying it.
    // ============================================================

    private void MoveHeldPanel()
    {
        if (heldPanel == null)
            return;

        Page page =
            GetPage(heldPanel.PageIndex);

        if (page == null)
            return;

        if (Keyboard.current == null)
            return;

        int direction = 0;

        // A / Left = move panel left.
        if (Keyboard.current.aKey.wasPressedThisFrame ||
            Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            direction = -1;
        }

        // D / Right = move panel right.
        if (Keyboard.current.dKey.wasPressedThisFrame ||
            Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            direction = 1;
        }

        if (direction == 0)
            return;

        // Calculate insertion destination.
        int newIndex =
            CalculateMoveIndex(
                page,
                targetSlotIndex,
                direction
            );

        if (newIndex == targetSlotIndex)
            return;

        // Actually reorder the page list.
        ReorderPanel(
            page,
            targetSlotIndex,
            newIndex
        );

        // Update target slot.
        targetSlotIndex = newIndex;

        // Update every panel's slot.
        UpdateSlotIndices(page);

        // Rearrange the visual panel positions.
        LayoutPage(page);


        // ==================== DEBUG START ====================
        Debug.Log(
            $"[PanelManager] Moving " +
            $"{heldPanel.PanelID} -> slot {newIndex}"
        );
        // ===================== DEBUG END =====================
    }


    // ============================================================
    // CALCULATE MOVE INDEX
    // Handles left/right movement with wrapping.
    // ============================================================

    private int CalculateMoveIndex(
        Page page,
        int currentIndex,
        int direction)
    {
        int count =
            page.panels.Count;

        if (count <= 0)
            return currentIndex;

        int columns =
            Mathf.Max(1, page.columns);

        int rows =
            Mathf.Max(1, page.rows);

        int row =
            currentIndex / columns;

        int column =
            currentIndex % columns;

        column += direction;

        // Moving left from first column:
        // wrap to the last column of the SAME row.
        if (column < 0)
        {
            column = columns - 1;
        }

        // Moving right from last column:
        // wrap to first column of the SAME row.
        else if (column >= columns)
        {
            column = 0;
        }

        int target =
            row * columns + column;

        // Make sure target is valid.
        if (target < 0 ||
            target >= count)
        {
            return currentIndex;
        }

        return target;
    }


    // ============================================================
    // REORDER PANEL
    // Uses INSERTION instead of swapping.
    //
    // Example:
    // [1,2,3,4]
    //
    // Move 3 to index 0:
    // [3,1,2,4]
    // ============================================================

    private void ReorderPanel(
        Page page,
        int oldIndex,
        int newIndex)
    {
        if (oldIndex == newIndex)
            return;

        if (oldIndex < 0 ||
            oldIndex >= page.panels.Count)
            return;

        if (newIndex < 0 ||
            newIndex >= page.panels.Count)
            return;

        // Save the panel being moved.
        ComicPanel movingPanel =
            page.panels[oldIndex];

        // Remove it.
        page.panels.RemoveAt(oldIndex);

        // Insert it at the new location.
        page.panels.Insert(
            newIndex,
            movingPanel
        );
    }


    // ============================================================
    // UPDATE SLOT INDICES
    // Gives every panel its new logical slot.
    // ============================================================

    private void UpdateSlotIndices(Page page)
    {
        for (int i = 0;
             i < page.panels.Count;
             i++)
        {
            page.panels[i]
                .SetSlotIndex(i);
        }
    }


    // ============================================================
    // PLACE HELD PANEL
    // Finalizes panel movement.
    // ============================================================

    private void PlaceHeldPanel()
    {
        if (heldPanel == null)
            return;

        Page page =
            GetPage(heldPanel.PageIndex);

        if (page == null)
            return;

        // Ensure correct slot.
        heldPanel.SetSlotIndex(
            targetSlotIndex
        );

        // Re-layout the page.
        LayoutPage(page);

        // Restore player physics.
        ReleaseCarriedPlayer();

        // Remember which panel was selected.
        selectedPanel = heldPanel;

        // ==================== PANEL CONTENTS ====================

        if (heldPanelContents != null)
        {
            heldPanelContents.RestoreAfterPanelMove();
            heldPanelContents = null;
        }

        // ===================== PANEL CONTENTS END =====================

        // Stop carrying.
        heldPanel = null;
        carryingPlayer = false;


        // ==================== DEBUG START ====================
        Debug.Log(
            $"[PanelManager] Placed panel. " +
            $"Final slot={targetSlotIndex}"
        );

        PrintPageContents(
            page,
            "AFTER PANEL MOVE"
        );
        // ===================== DEBUG END =====================
    }


    // ============================================================
    // CANCEL PANEL MOVE
    // Restores original panel state.
    // ============================================================

    private void CancelPanelMove()
    {
        if (heldPanel == null)
            return;

        Page page =
            GetPage(originalPageIndex);

        if (page == null)
            return;

        // Restore original slot.
        heldPanel.SetSlotIndex(
            originalSlotIndex
        );

        // Rebuild list ordering.
        SortPage(page);

        // Restore original panel position.
        heldPanel.transform.position =
            originalPanelPosition;

        // Restore player.
        ReleaseCarriedPlayer();

        // Restore original player position.
        if (playerBody != null)
        {
            playerBody.position =
                originalPlayerPosition;

            playerBody.linearVelocity =
                originalPlayerVelocity;
        }

        // Restore selection.
        selectedPanel =
            heldPanel;

        // ==================== PANEL CONTENTS ====================

        if (heldPanelContents != null)
        {
            heldPanelContents.RestoreAfterPanelMove();
            heldPanelContents = null;
        }

        // ===================== PANEL CONTENTS END =====================

        heldPanel = null;
        carryingPlayer = false;

        // Restore layout.
        LayoutPage(page);


        // ==================== DEBUG START ====================
        Debug.Log(
            "[PanelManager] Panel movement cancelled."
        );
        // ===================== DEBUG END =====================
    }


    // ============================================================
    // RELEASE CARRIED PLAYER
    // Restores gravity and normal physics.
    // ============================================================

    private void ReleaseCarriedPlayer()
    {
        if (playerBody == null)
            return;

        playerBody.gravityScale =
            originalPlayerGravityScale;

        playerBody.linearVelocity =
            Vector2.zero;

        carryingPlayer = false;
    }


    // ============================================================
    // IS PLAYER ON PANEL
    // Determines whether ComicMan is inside a panel.
    // ============================================================

    private bool IsPlayerOnPanel(
        ComicPanel panel)
    {
        if (panel == null)
            return false;

        if (playerBody == null)
            return false;

        Bounds bounds =
            panel.GetWorldBounds();

        return bounds.Contains(
            playerBody.position
        );
    }


    // ============================================================
    // GET PAGE
    // Returns page 0 or page 1.
    // ============================================================

    private Page GetPage(int pageIndex)
    {
        return pageIndex == 0
            ? leftPage
            : rightPage;
    }


    // ============================================================
    // GET FIRST PANEL
    // Used when entering Artist Mode without a current panel.
    // ============================================================

    private ComicPanel GetFirstPanel()
    {
        if (leftPage != null &&
            leftPage.panels.Count > 0)
        {
            return leftPage.panels[0];
        }

        if (rightPage != null &&
            rightPage.panels.Count > 0)
        {
            return rightPage.panels[0];
        }

        return null;
    }


    // ============================================================
    // LAYOUT PAGE
    // Places panels into their fixed grid slots.
    // ============================================================

    private void LayoutPage(Page page)
    {
        if (page == null)
            return;

        if (page.panels == null)
            return;

        int columns =
            Mathf.Max(1, page.columns);

        for (int i = 0;
             i < page.panels.Count;
             i++)
        {
            ComicPanel panel =
                page.panels[i];

            if (panel == null)
                continue;

            // Make sure logical slot matches list position.
            panel.SetSlotIndex(i);

            // Calculate world position.
            Vector3 position =
                GetSlotPosition(
                    page,
                    i
                );

            // Apply position.
            panel.transform.position =
                position;
        }
    }


    // ============================================================
    // GET SLOT POSITION
    // Converts a page slot index into a world position.
    // ============================================================

    // ==================== PANEL SLOT POSITION ====================
    // Calculates where a panel should be placed inside its page.
    // HorizontalSpacing and VerticalSpacing control the gaps
    // between panels.
    // ====================
    // ==================== PANEL SLOT POSITION ====================
    // Calculates a fixed slot position inside the page's
    // BoxCollider2D boundary.
    // Panels cannot be positioned outside this rectangle.
    // ====================
    private Vector3 GetSlotPosition(Page page, int slotIndex)
    {
        if (page.pageArea == null)
        {
            Debug.LogWarning(
                "PanelManager: Page has no Page Area BoxCollider2D assigned."
            );

            return Vector3.zero;
        }

        int columns = Mathf.Max(1, page.columns);
        int rows = Mathf.Max(1, page.rows);

        Bounds pageBounds = page.pageArea.bounds;

        int column = slotIndex % columns;
        int row = slotIndex / columns;

        // Get the actual size of a panel.
        float panelWidth = 0f;
        float panelHeight = 0f;

        if (page.panels.Count > 0)
        {
            Bounds panelBounds = page.panels[0].GetWorldBounds();

            panelWidth = panelBounds.size.x;
            panelHeight = panelBounds.size.y;
        }

        // Calculate total dimensions of the panel grid.
        float totalWidth =
            (columns * panelWidth) +
            ((columns - 1) * horizontalSpacing);

        float totalHeight =
            (rows * panelHeight) +
            ((rows - 1) * verticalSpacing);

        // Start the grid from the center of the fixed page area.
        float startX =
            pageBounds.center.x -
            totalWidth * 0.5f +
            panelWidth * 0.5f;

        float startY =
            pageBounds.center.y +
            totalHeight * 0.5f -
            panelHeight * 0.5f;

        float x =
            startX +
            column * (panelWidth + horizontalSpacing);

        float y =
            startY -
            row * (panelHeight + verticalSpacing);

        return new Vector3(x, y, 0f);
    }


    // ============================================================
    // SET CURRENT PANEL
    // Called by PanelTrigger.
    // ============================================================

    public void SetCurrentPanel(
        ComicPanel panel)
    {
        if (panel == null)
            return;

        currentPanel = panel;

        // Automatically select current panel
        // when not carrying another panel.
        if (!artistMode &&
            heldPanel == null)
        {
            selectedPanel = panel;
        }
    }


    // ============================================================
    // TRY AUTOMATIC HOP
    // Attempts to move ComicMan to another panel.
    // ============================================================

    public void TryAutomaticHop(
        ComicPanel exitingPanel,
        float horizontalVelocity)
    {
        if (exitingPanel == null)
            return;

        if (hopLockTimer > 0f)
            return;

        // Require enough horizontal movement.
        if (Mathf.Abs(horizontalVelocity) <
            hopHorizontalThreshold)
        {
            return;
        }

        Page page =
            GetPage(exitingPanel.PageIndex);

        if (page == null)
            return;

        int currentIndex =
            page.panels.IndexOf(
                exitingPanel
            );

        if (currentIndex < 0)
            return;

        int direction =
            horizontalVelocity > 0f
                ? 1
                : -1;

        // Move to the next panel.
        int targetIndex =
            currentIndex + direction;

        // Wrap around within the same page.
        if (targetIndex < 0)
        {
            targetIndex =
                page.panels.Count - 1;
        }
        else if (
            targetIndex >=
            page.panels.Count)
        {
            targetIndex = 0;
        }

        ComicPanel targetPanel =
            page.panels[targetIndex];

        HopToPanel(targetPanel);

        hopLockTimer =
            hopLockDuration;
    }


    // ============================================================
    // HOP TO PANEL
    // Moves ComicMan to a hop point in another panel.
    // ============================================================

    private void HopToPanel(
        ComicPanel targetPanel)
    {
        if (targetPanel == null)
            return;

        if (playerBody == null)
            return;

        // Find closest valid arrival point.
        Transform hopPoint =
            targetPanel.GetClosestHopPoint(
                playerBody.position
            );

        if (hopPoint == null)
            return;

        // Move player to the new panel.
        playerBody.position =
            hopPoint.position;

        // Stop existing velocity.
        playerBody.linearVelocity =
            Vector2.zero;

        // Update current panel.
        currentPanel =
            targetPanel;


        // ==================== DEBUG START ====================
        Debug.Log(
            $"[PanelManager] Hopped to " +
            $"{targetPanel.PanelID} at " +
            $"{hopPoint.position}"
        );
        // ===================== DEBUG END =====================
    }


    // ============================================================
    // UPDATE SELECTION VISUAL
    // Makes the highlight follow selected panel.
    // ============================================================

    private void UpdateSelectionVisual()
    {
        if (selectionHighlight == null)
            return;

        if (!artistMode ||
            selectedPanel == null)
        {
            selectionHighlight.gameObject
                .SetActive(false);

            return;
        }

        selectionHighlight.gameObject
            .SetActive(true);

        // Position highlight on selected panel.
        selectionHighlight.position =
            selectedPanel.transform.position;

        // Match panel size.
        Bounds bounds =
            selectedPanel.GetWorldBounds();

        Vector3 scale =
            new Vector3(
                bounds.size.x +
                selectionPadding * 2f,

                bounds.size.y +
                selectionPadding * 2f,

                1f
            );

        selectionHighlight.localScale =
            scale;
    }


    // ============================================================
    // PRINT PAGE CONTENTS
    // Debug helper for checking panel ordering.
    // ============================================================

    private void PrintPageContents(
        Page page,
        string label)
    {
        if (page == null)
            return;

        string output =
            $"[{label}] ";

        for (int i = 0;
             i < page.panels.Count;
             i++)
        {
            ComicPanel panel =
                page.panels[i];

            if (panel == null)
                continue;

            output +=
                $"[{i}] {panel.PanelID} ";

            if (i <
                page.panels.Count - 1)
            {
                output += "-> ";
            }
        }

        Debug.Log(output);
    }


    // ============================================================
    // GIZMOS
    // Shows page boundaries and panel slot positions.
    // ============================================================

    private void OnDrawGizmosSelected()
    {
        DrawPageGizmos(leftPage);
        DrawPageGizmos(rightPage);
    }


    // ============================================================
    // DRAW PAGE GIZMOS
    // Draws page rectangle and slot positions.
    // ============================================================

    // ==================== PAGE GIZMOS ====================
    // Draws the page boundary using the assigned BoxCollider2D.
    // The BoxCollider2D is now the single source of truth for
    // the playable page area.
    // ====================
    private void DrawPageGizmos(Page page)
    {
        if (page == null)
            return;

        if (page.pageArea == null)
            return;

        Bounds bounds = page.pageArea.bounds;

        Gizmos.DrawWireCube(
            bounds.center,
            bounds.size
        );
    }
}