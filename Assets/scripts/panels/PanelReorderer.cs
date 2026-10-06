using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles the actual panel insertion/reordering logic.
///
/// PanelManager remains responsible for:
/// - Artist Mode
/// - Panel selection
/// - Player state
/// - Panel contents
/// - Layout
/// - Automatic hopping
///
/// This script only changes SlotIndex values.
/// PageIndex is never changed.
/// </summary>
public class PanelReorderer : MonoBehaviour
{
    // ============================================================
    // DEBUG
    // ============================================================

    [Header("Debug")]

    /// <summary>
    /// Enables detailed reorder debug messages.
    /// </summary>
    [SerializeField] private bool debugLogs = true;


    // ============================================================
    // PUBLIC REORDER FUNCTION
    // ============================================================

    /// <summary>
    /// Inserts movingPanel at the destination panel's slot.
    ///
    /// This is INSERTION, not swapping.
    ///
    /// Example:
    ///
    /// Before:
    /// [1][2]
    /// [3][4]
    ///
    /// Move 3 -> slot 0:
    ///
    /// After:
    /// [3][1]
    /// [2][4]
    ///
    /// Protected/non-movable panels keep their slots.
    /// </summary>
    public bool TryReorderPanel(
        ComicPanel movingPanel,
        ComicPanel destinationPanel
    )
    {
        if (movingPanel == null)
        {
            DebugLog(
                "Reorder failed: moving panel is null."
            );

            return false;
        }

        if (destinationPanel == null)
        {
            DebugLog(
                "Reorder failed: destination panel is null."
            );

            return false;
        }

        if (movingPanel == destinationPanel)
        {
            DebugLog(
                "Reorder failed: moving and destination panels " +
                "are the same."
            );

            return false;
        }

        if (!movingPanel.IsMovable)
        {
            DebugLog(
                "Reorder refused: " +
                movingPanel.PanelID +
                " is marked non-movable."
            );

            return false;
        }

        // --------------------------------------------------------
        // Reordering is page-local.
        // --------------------------------------------------------

        if (
            movingPanel.PageIndex
            != destinationPanel.PageIndex
        )
        {
            DebugLog(
                "Reorder refused: panels are on different pages."
            );

            return false;
        }

        // --------------------------------------------------------
        // The destination itself must be movable.
        // PanelManager additionally protects ComicMan's current
        // panel before calling this function.
        // --------------------------------------------------------

        if (!destinationPanel.IsMovable)
        {
            DebugLog(
                "Reorder refused: destination " +
                destinationPanel.PanelID +
                " is non-movable."
            );

            return false;
        }

        int originalSlot =
            movingPanel.SlotIndex;

        int targetSlot =
            destinationPanel.SlotIndex;

        // --------------------------------------------------------
        // Find all panels on this page.
        // --------------------------------------------------------

        ComicPanel[] allPanels =
            FindObjectsByType<ComicPanel>(
                FindObjectsSortMode.None
            );

        List<ComicPanel> pagePanels =
            new List<ComicPanel>();

        foreach (ComicPanel panel in allPanels)
        {
            if (panel == null)
                continue;

            if (
                panel.PageIndex
                == movingPanel.PageIndex
            )
            {
                pagePanels.Add(panel);
            }
        }

        if (pagePanels.Count == 0)
        {
            DebugLog(
                "Reorder failed: page contains no panels."
            );

            return false;
        }

        // --------------------------------------------------------
        // Build the sequence of movable panels.
        //
        // The moving panel is removed from this sequence first.
        // Its old slot becomes available for insertion.
        // --------------------------------------------------------

        List<ComicPanel> movablePanels =
            new List<ComicPanel>();

        foreach (ComicPanel panel in pagePanels)
        {
            if (panel == movingPanel)
                continue;

            if (!panel.IsMovable)
                continue;

            movablePanels.Add(panel);
        }

        movablePanels.Sort(
            (a, b) =>
                a.SlotIndex.CompareTo(
                    b.SlotIndex
                )
        );

        // --------------------------------------------------------
        // Build the list of slots that movable panels may occupy.
        //
        // The moving panel's original slot is included because
        // that slot becomes available during insertion.
        // --------------------------------------------------------

        List<int> movableSlots =
            new List<int>();

        foreach (ComicPanel panel in pagePanels)
        {
            if (panel == movingPanel)
            {
                movableSlots.Add(
                    panel.SlotIndex
                );

                continue;
            }

            if (!panel.IsMovable)
                continue;

            movableSlots.Add(
                panel.SlotIndex
            );
        }

        movableSlots.Sort();

        // --------------------------------------------------------
        // Find the destination's position in the movable sequence.
        // --------------------------------------------------------

        int targetIndex =
            movableSlots.IndexOf(
                targetSlot
            );

        if (targetIndex < 0)
        {
            DebugLog(
                "Reorder failed: destination slot " +
                targetSlot +
                " is protected or invalid."
            );

            return false;
        }

        // --------------------------------------------------------
        // INSERT the moving panel.
        //
        // This shifts the other movable panels instead of swapping.
        // --------------------------------------------------------

        movablePanels.Insert(
            targetIndex,
            movingPanel
        );

        if (
            movablePanels.Count
            != movableSlots.Count
        )
        {
            DebugLog(
                "Reorder safety check failed: " +
                "panel/slot counts do not match."
            );

            return false;
        }

        // --------------------------------------------------------
        // Assign the new slots.
        // --------------------------------------------------------

        for (
            int i = 0;
            i < movablePanels.Count;
            i++
        )
        {
            movablePanels[i].SetSlotIndex(
                movableSlots[i]
            );
        }

        // --------------------------------------------------------
        // PageIndex is deliberately NOT changed.
        // --------------------------------------------------------

        DebugLog(
            "REORDER COMPLETE: " +
            movingPanel.PanelID +
            " | Slot " +
            originalSlot +
            " -> " +
            movingPanel.SlotIndex
        );

        PrintPageOrder(
            pagePanels
        );

        return true;
    }


    // ============================================================
    // DEBUG PAGE ORDER
    // ============================================================

    /// <summary>
    /// Prints the final panel order for one page.
    /// </summary>
    private void PrintPageOrder(
        List<ComicPanel> pagePanels
    )
    {
        if (!debugLogs)
            return;

        pagePanels.Sort(
            (a, b) =>
                a.SlotIndex.CompareTo(
                    b.SlotIndex
                )
        );

        string result =
            "[REORDER DEBUG] PAGE " +
            pagePanels[0].PageIndex +
            " ORDER: ";

        foreach (ComicPanel panel in pagePanels)
        {
            result +=
                "[" +
                panel.PanelID +
                " -> Slot " +
                panel.SlotIndex +
                "] ";
        }

        Debug.Log(result);
    }


    // ============================================================
    // DEBUG LOG
    // ============================================================

    /// <summary>
    /// Prints a reorder debug message when enabled.
    /// </summary>
    private void DebugLog(
        string message
    )
    {
        if (!debugLogs)
            return;

        Debug.Log(
            "[REORDER DEBUG] " +
            message
        );
    }
}
