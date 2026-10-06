using UnityEngine;

/// <summary>
/// Controls the camera specifically for Artist Mode.
/// When Artist Mode starts, the camera zooms out and centers on the
/// page ComicMan is currently on so the whole page is visible.
/// When Artist Mode ends, the normal camera-follow script is restored.
/// </summary>
public class ArtistModeCamera : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private BoxCollider2D leftPageArea;

    [SerializeField]
    private BoxCollider2D rightPageArea;

    [Tooltip("Your normal camera-follow component, usually CameraFollow.")]
    [SerializeField]
    private MonoBehaviour normalCameraFollow;

    [Header("Artist Mode Camera")]

    [SerializeField]
    private float extraPadding = 1f;

    [SerializeField]
    private float zoomTransitionSpeed = 6f;

    [SerializeField]
    private float minimumArtistOrthographicSize = 4f;

    private Camera targetCamera;

    private bool artistModeWasActive;

    private float normalOrthographicSize;

    private Vector3 normalCameraPosition;

    private void Awake()
    {
        // Get the Camera component on this GameObject.
        targetCamera = GetComponent<Camera>();

        if (targetCamera == null)
        {
            Debug.LogError(
                "[ARTIST CAMERA] No Camera component found on " +
                gameObject.name
            );

            return;
        }

        // Automatically find ComicMan's PlayerController if needed.
        if (playerController == null)
        {
            playerController =
                FindFirstObjectByType<PlayerController>();
        }

        // Automatically find the normal CameraFollow component if it
        // is attached to this same Camera.
        if (normalCameraFollow == null)
        {
            normalCameraFollow =
                GetComponent("CameraFollow") as MonoBehaviour;
        }

        // Save the normal camera settings so they can be restored later.
        normalOrthographicSize =
            targetCamera.orthographicSize;

        normalCameraPosition =
            transform.position;

        Debug.Log(
            "[ARTIST CAMERA] Initialized. Normal orthographic size = " +
            normalOrthographicSize
        );
    }

    private void LateUpdate()
    {
        if (targetCamera == null || playerController == null)
        {
            return;
        }

        bool artistModeActive =
            playerController.IsArtistMode;

        // Detect entering Artist Mode.
        if (artistModeActive && !artistModeWasActive)
        {
            EnterArtistCamera();
        }

        // Detect leaving Artist Mode.
        if (!artistModeActive && artistModeWasActive)
        {
            ExitArtistCamera();
        }

        artistModeWasActive =
            artistModeActive;

        // While Artist Mode is active, continuously frame the
        // page ComicMan is currently standing on.
        if (artistModeActive)
        {
            UpdateArtistCamera();
        }
    }

    /// <summary>
    /// Takes control away from the normal camera-follow script
    /// and prepares the camera for Artist Mode.
    /// </summary>
    private void EnterArtistCamera()
    {
        if (normalCameraFollow != null)
        {
            normalCameraFollow.enabled = false;
        }

        // Store the camera's current position in case another
        // camera system has moved it before Artist Mode starts.
        normalCameraPosition =
            transform.position;

        normalOrthographicSize =
            targetCamera.orthographicSize;

        Debug.Log(
            "[ARTIST CAMERA] Entered Artist Mode camera."
        );
    }

    /// <summary>
    /// Finds the page ComicMan is on and smoothly zooms the camera
    /// until that entire page fits on screen.
    /// </summary>
    private void UpdateArtistCamera()
    {
        BoxCollider2D activePage =
            GetCurrentPageArea();

        if (activePage == null)
        {
            return;
        }

        Bounds pageBounds =
            activePage.bounds;

        float screenAspect =
            (float)Screen.width /
            Mathf.Max(1, Screen.height);

        // Orthographic size controls half of the visible vertical area.
        // Calculate the size required to fit the entire page height.
        float requiredVerticalSize =
            pageBounds.size.y * 0.5f;

        // Calculate the size required to fit the entire page width.
        float requiredHorizontalSize =
            pageBounds.size.x /
            (2f * Mathf.Max(0.01f, screenAspect));

        float requiredSize =
            Mathf.Max(
                requiredVerticalSize,
                requiredHorizontalSize
            );

        requiredSize += extraPadding;

        requiredSize =
            Mathf.Max(
                requiredSize,
                minimumArtistOrthographicSize
            );

        // Center the camera on the page.
        Vector3 targetPosition =
            new Vector3(
                pageBounds.center.x,
                pageBounds.center.y,
                transform.position.z
            );

        // Smoothly move the camera to the page center.
        transform.position =
            Vector3.Lerp(
                transform.position,
                targetPosition,
                Time.unscaledDeltaTime *
                zoomTransitionSpeed
            );

        // Smoothly zoom out to show the entire page.
        targetCamera.orthographicSize =
            Mathf.Lerp(
                targetCamera.orthographicSize,
                requiredSize,
                Time.unscaledDeltaTime *
                zoomTransitionSpeed
            );
    }

    /// <summary>
    /// Determines whether ComicMan is currently on the left or right page.
    /// </summary>
    private BoxCollider2D GetCurrentPageArea()
    {
        if (playerController == null)
        {
            return null;
        }

        Vector3 playerPosition =
            playerController.transform.position;

        if (leftPageArea != null &&
            leftPageArea.bounds.Contains(playerPosition))
        {
            return leftPageArea;
        }

        if (rightPageArea != null &&
            rightPageArea.bounds.Contains(playerPosition))
        {
            return rightPageArea;
        }

        // If ComicMan is near an edge and no collider contains the
        // position, use the closest page center as a fallback.
        if (leftPageArea == null)
        {
            return rightPageArea;
        }

        if (rightPageArea == null)
        {
            return leftPageArea;
        }

        float leftDistance =
            Mathf.Abs(
                playerPosition.x -
                leftPageArea.bounds.center.x
            );

        float rightDistance =
            Mathf.Abs(
                playerPosition.x -
                rightPageArea.bounds.center.x
            );

        return leftDistance <= rightDistance
            ? leftPageArea
            : rightPageArea;
    }

    /// <summary>
    /// Restores the normal camera-follow system and original zoom
    /// when Artist Mode ends.
    /// </summary>
    private void ExitArtistCamera()
    {
        if (normalCameraFollow != null)
        {
            normalCameraFollow.enabled = true;
        }

        targetCamera.orthographicSize =
            normalOrthographicSize;

        Debug.Log(
            "[ARTIST CAMERA] Exited Artist Mode camera."
        );
    }

    /// <summary>
    /// Draws the page areas in the Scene view for easier setup/debugging.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        DrawPageGizmo(
            leftPageArea,
            Color.white
        );

        DrawPageGizmo(
            rightPageArea,
            Color.gray
        );
    }

    /// <summary>
    /// Draws one page-area gizmo.
    /// </summary>
    private void DrawPageGizmo(
        BoxCollider2D pageArea,
        Color gizmoColor
    )
    {
        if (pageArea == null)
        {
            return;
        }

        Gizmos.color =
            gizmoColor;

        Gizmos.DrawWireCube(
            pageArea.bounds.center,
            pageArea.bounds.size
        );
    }
}
