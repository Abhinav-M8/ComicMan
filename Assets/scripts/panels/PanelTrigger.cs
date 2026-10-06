using UnityEngine;

public class PanelTrigger : MonoBehaviour
{
    private ComicPanel comicPanel;


    // ==================== UNITY START ====================

    // Gets the ComicPanel attached to this panel.
    private void Awake()
    {
        comicPanel = GetComponent<ComicPanel>();
    }


    // Detects when ComicMan enters this panel.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (comicPanel == null)
            return;

        if (PanelManager.Instance == null)
            return;

        PanelManager.Instance.SetCurrentPanel(comicPanel);
    }


    // Detects when ComicMan exits this panel.
    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (comicPanel == null)
            return;

        if (PanelManager.Instance == null)
            return;

        Collider2D playerCollider = other;

        // Get the actual world bounds of this panel.
        Bounds panelBounds = comicPanel.GetWorldBounds();

        // Get ComicMan's collider bounds.
        Bounds playerBounds = playerCollider.bounds;


        // ==================== EDGE CHECK ====================

        // Check whether ComicMan actually crossed
        // the LEFT edge of the panel.
        bool exitedLeft =
            playerBounds.max.x < panelBounds.min.x;


        // Check whether ComicMan actually crossed
        // the RIGHT edge of the panel.
        bool exitedRight =
            playerBounds.min.x > panelBounds.max.x;


        // If ComicMan left through the top or bottom,
        // neither of the above will be true.
        //
        // Therefore:
        //
        // Jumping upward       -> NO HOP
        // Falling downward     -> NO HOP
        // Leaving left edge    -> HOP LEFT
        // Leaving right edge   -> HOP RIGHT

        if (!exitedLeft && !exitedRight)
        {
            // ==================== DEBUG START ====================

            Debug.Log(
                "[PanelTrigger] ComicMan left " +
                comicPanel.PanelID +
                " vertically. No hop."
            );

            // ===================== DEBUG END =====================

            return;
        }

        // Determine the direction of the hop.
        float hopDirection;

        if (exitedRight)
        {
            hopDirection = 1f;
        }
        else
        {
            hopDirection = -1f;
        }


        // ==================== DEBUG START ====================

        Debug.Log(
            "[PanelTrigger] ComicMan left " +
            comicPanel.PanelID +
            " through the " +
            (exitedRight ? "RIGHT" : "LEFT") +
            " edge. Automatic hop."
        );

        // ===================== DEBUG END ====================


        PanelManager.Instance.TryAutomaticHop(
            comicPanel,
            hopDirection
        );
    }

    // ===================== UNITY END =====================
}