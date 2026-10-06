using UnityEngine;

public class PanelContents : MonoBehaviour
{
    private Rigidbody2D[] childBodies;
    private RigidbodyType2D[] originalBodyTypes;
    private Vector2[] originalVelocities;
    private float[] originalGravityScales;

    private bool preparedForMove;


    // ==================== UNITY START ====================

    // Finds Rigidbody2D objects belonging to this panel,
    // but deliberately ignores ComicMan.
    private void Awake()
    {
        Rigidbody2D[] allBodies =
            GetComponentsInChildren<Rigidbody2D>();

        // Temporary list containing only bodies that belong
        // to panel-controlled objects.
        System.Collections.Generic.List<Rigidbody2D> validBodies =
            new System.Collections.Generic.List<Rigidbody2D>();

        foreach (Rigidbody2D body in allBodies)
        {
            if (body == null)
                continue;

            // NEVER control the player through PanelContents.
            if (body.CompareTag("Player"))
                continue;

            validBodies.Add(body);
        }

        childBodies = validBodies.ToArray();

        originalBodyTypes =
            new RigidbodyType2D[childBodies.Length];

        originalVelocities =
            new Vector2[childBodies.Length];

        originalGravityScales =
            new float[childBodies.Length];

        // ==================== DEBUG START ====================

        Debug.Log(
            "[PanelContents] " +
            gameObject.name +
            " controls " +
            childBodies.Length +
            " Rigidbody2D objects."
        );

        // ===================== DEBUG END =====================
    }

    // ===================== UNITY END =====================


    // ==================== PANEL MOVE ====================

    // Temporarily disables physics for panel-controlled objects.
    public void PrepareForPanelMove()
    {
        if (preparedForMove)
            return;

        for (int i = 0; i < childBodies.Length; i++)
        {
            Rigidbody2D body = childBodies[i];

            if (body == null)
                continue;

            originalBodyTypes[i] = body.bodyType;
            originalVelocities[i] = body.linearVelocity;
            originalGravityScales[i] = body.gravityScale;

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
        }

        preparedForMove = true;
    }


    // Restores the physics state of panel-controlled objects.
    public void RestoreAfterPanelMove()
    {
        if (!preparedForMove)
            return;

        for (int i = 0; i < childBodies.Length; i++)
        {
            Rigidbody2D body = childBodies[i];

            if (body == null)
                continue;

            body.bodyType = originalBodyTypes[i];
            body.gravityScale = originalGravityScales[i];

            // Start with zero velocity after the panel moves.
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        preparedForMove = false;
    }

    // ===================== PANEL MOVE END =====================
}