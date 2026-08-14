using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ConstellationConnector : MonoBehaviour
{
    [Header("Stars")]

    [Tooltip("Parent containing all stars. Leave empty to automatically find 'Stars'.")]
    public Transform starsParent;


    [Header("Constellation Line")]

    public Material lineMaterial;

    public float lineWidth = 0.02f;


    [Header("Raycast")]

    [Tooltip("Maximum distance the star ray can travel.")]
    public float raycastDistance = 2000f;


    private Camera mainCamera;

    private GameObject selectedStar;


    private class StarConnection
    {
        public GameObject starA;
        public GameObject starB;
        public LineRenderer line;
    }


    private readonly List<StarConnection> connections =
        new List<StarConnection>();


    private void Start()
    {
        // This is the normal Unity camera.
        //
        // It is NOT an AR plane raycast.
        // It is only being used to turn a screen position
        // into a 3D ray.
        mainCamera = Camera.main;


        if (starsParent == null)
        {
            GameObject starsObject =
                GameObject.Find("Stars");

            if (starsObject != null)
            {
                starsParent =
                    starsObject.transform;
            }
        }


        if (mainCamera == null)
        {
            Debug.LogError(
                "ConstellationConnector: " +
                "Could not find Camera.main."
            );
        }


        if (starsParent == null)
        {
            Debug.LogError(
                "ConstellationConnector: " +
                "Could not find the Stars GameObject."
            );
        }
    }


    private void Update()
    {
        HandleMouse();
        HandleTouch();
    }


    // =========================================================
    // MOUSE INPUT - NEW INPUT SYSTEM
    // =========================================================

    private void HandleMouse()
    {
        if (Mouse.current == null)
            return;


        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;


        Vector2 mousePosition =
            Mouse.current.position.ReadValue();


        Debug.Log(
            "ConstellationConnector: Mouse click at " +
            mousePosition
        );


        TrySelectStar(mousePosition);
    }


    // =========================================================
    // TOUCH INPUT - NEW INPUT SYSTEM
    // =========================================================

    private void HandleTouch()
    {
        if (Touchscreen.current == null)
            return;


        UnityEngine.InputSystem.Controls.TouchControl touch =
            Touchscreen.current.primaryTouch;


        if (!touch.press.wasPressedThisFrame)
            return;


        Vector2 touchPosition =
            touch.position.ReadValue();


        Debug.Log(
            "ConstellationConnector: Touch at " +
            touchPosition
        );


        TrySelectStar(touchPosition);
    }
    // =========================================================
    // RAYCAST
    // =========================================================

    private void TrySelectStar(Vector2 screenPosition)
    {
        if (mainCamera == null)
            return;


        Ray ray =
            mainCamera.ScreenPointToRay(
                screenPosition
            );


        RaycastHit[] hits =
            Physics.RaycastAll(
                ray,
                raycastDistance
            );


        if (hits.Length == 0)
        {
            Debug.Log(
                "ConstellationConnector: " +
                "Raycast did not hit anything."
            );

            return;
        }


        // Find the closest collider belonging to a star.
        //
        // We don't use a special layer here.
        // The default Physics.RaycastAll behaviour checks
        // all layers.
        System.Array.Sort(
            hits,
            (a, b) =>
                a.distance.CompareTo(b.distance)
        );


        for (int i = 0; i < hits.Length; i++)
        {
            GameObject star =
                GetStarFromCollider(
                    hits[i].collider
                );


            if (star == null)
                continue;


            SelectStar(star);

            return;
        }
    }


    // =========================================================
    // FIND STAR
    // =========================================================

    private GameObject GetStarFromCollider(
        Collider collider)
    {
        if (collider == null)
            return null;


        Transform current =
            collider.transform;


        while (current != null)
        {
            // If Stars is assigned, only accept objects
            // that actually belong to the Stars hierarchy.
            if (
                starsParent != null &&
                current == starsParent
            )
            {
                return null;
            }


            if (
                starsParent != null &&
                current.IsChildOf(starsParent)
            )
            {
                return current.gameObject;
            }


            current =
                current.parent;
        }


        return null;
    }


    // =========================================================
    // STAR SELECTION
    // =========================================================

    private void SelectStar(GameObject star)
    {
        Debug.Log(
            "ConstellationConnector: " +
            "Tapped star: " +
            star.name
        );


        // No star selected yet.
        if (selectedStar == null)
        {
            selectedStar = star;

            Debug.Log(
                "ConstellationConnector: " +
                "First star selected: " +
                star.name
            );

            return;
        }


        // Tapping the same star cancels selection.
        if (selectedStar == star)
        {
            selectedStar = null;

            Debug.Log(
                "ConstellationConnector: " +
                "Selection cancelled."
            );

            return;
        }


        // Don't create duplicate connections.
        if (
            ConnectionExists(
                selectedStar,
                star
            )
        )
        {
            Debug.Log(
                "ConstellationConnector: " +
                "Connection already exists."
            );

            selectedStar = star;

            return;
        }


        CreateConnection(
            selectedStar,
            star
        );


        // Continue from the second star.
        //
        // This allows:
        //
        // A -> B -> C -> D
        //
        selectedStar = star;
    }


    // =========================================================
    // CREATE LINE
    // =========================================================

    private void CreateConnection(
        GameObject starA,
        GameObject starB)
    {
        GameObject lineObject =
            new GameObject(
                "ConstellationLine"
            );


        // Keep the lines under Stars so they move
        // with the celestial sphere.
        if (starsParent != null)
        {
            lineObject.transform.SetParent(
                starsParent,
                true
            );
        }


        LineRenderer line =
            lineObject.AddComponent<LineRenderer>();


        line.useWorldSpace = true;

        line.positionCount = 2;

        line.startWidth = lineWidth;
        line.endWidth = lineWidth;

        line.numCapVertices = 4;
        line.numCornerVertices = 4;


        if (lineMaterial != null)
        {
            line.material =
                lineMaterial;
        }


        UpdateLine(
            line,
            starA,
            starB
        );


        StarConnection connection =
            new StarConnection();


        connection.starA = starA;
        connection.starB = starB;
        connection.line = line;


        connections.Add(
            connection
        );


        Debug.Log(
            "ConstellationConnector: Connected " +
            starA.name +
            " -> " +
            starB.name
        );
    }


    // =========================================================
    // UPDATE LINES
    // =========================================================

    private void UpdateLines()
    {
        for (int i = connections.Count - 1;
             i >= 0;
             i--)
        {
            StarConnection connection =
                connections[i];


            if (
                connection.line == null ||
                connection.starA == null ||
                connection.starB == null
            )
            {
                connections.RemoveAt(i);

                continue;
            }


            UpdateLine(
                connection.line,
                connection.starA,
                connection.starB
            );
        }
    }


    private void UpdateLine(
        LineRenderer line,
        GameObject starA,
        GameObject starB)
    {
        if (line == null)
            return;


        if (starA == null || starB == null)
            return;


        line.SetPosition(
            0,
            starA.transform.position
        );


        line.SetPosition(
            1,
            starB.transform.position
        );
    }


    // =========================================================
    // DUPLICATE CONNECTION CHECK
    // =========================================================

    private bool ConnectionExists(
        GameObject starA,
        GameObject starB)
    {
        for (int i = 0;
             i < connections.Count;
             i++)
        {
            StarConnection connection =
                connections[i];


            bool same =
                connection.starA == starA &&
                connection.starB == starB;


            bool reversed =
                connection.starA == starB &&
                connection.starB == starA;


            if (same || reversed)
                return true;
        }


        return false;
    }


    // =========================================================
    // PUBLIC FUNCTIONS
    // =========================================================

    public void ClearConstellation()
    {
        for (int i = 0;
             i < connections.Count;
             i++)
        {
            if (connections[i].line != null)
            {
                Destroy(
                    connections[i].line.gameObject
                );
            }
        }


        connections.Clear();

        selectedStar = null;
    }


    public void UndoLastConnection()
    {
        if (connections.Count == 0)
            return;


        int index =
            connections.Count - 1;


        StarConnection connection =
            connections[index];


        if (connection.line != null)
        {
            Destroy(
                connection.line.gameObject
            );
        }


        connections.RemoveAt(index);

        selectedStar = null;
    }
}