using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// Lets the user drag this solar panel with mouse/touch. When it is brought near the
// snap target it snaps onto it, hides the blinking target indicator and lights the house.
// Dragging a snapped panel away unsnaps it and reverts the indicator and house material.
[RequireComponent(typeof(MeshRenderer))]
public class SolarPanelDragSnap : MonoBehaviour
{
    [Header("Snap")]
    public Transform snapTarget;              // Where the panel should end up (TargetObjectPosition).
    public GameObject targetIndicator;        // Blinking guide to disable once snapped (usually the same object).
    public float snapDistance = 0.15f;        // World-space distance between pointer ray and target that triggers the snap.
    public float snapDuration = 0.2f;         // Time in seconds for the panel to settle into place.
    public float unsnapDragPixels = 8f;       // Pointer movement needed on a snapped panel before it unsnaps (so a plain click does nothing).

    [Header("House")]
    public MeshRenderer houseRenderer;        // House mesh renderer.
    public Material litMaterial;              // "With light" material applied after snapping.

    public UnityEvent onSnapped;
    public UnityEvent onUnsnapped;

    private Camera cam;
    private Collider panelCollider;
    private Material[] unlitMaterials;        // House materials at startup ("Without light"), restored on unsnap.
    private bool isDragging = false;
    private bool isSnapped = false;
    private bool isAnimating = false;
    private bool pendingUnsnap = false;
    private bool snapArmed = true;            // False right after unsnapping until the pointer leaves the snap zone.
    private Vector2 pressScreenPos;
    private Plane dragPlane;
    private Vector3 grabOffset;

    private void Awake()
    {
        cam = Camera.main;

        // Raycasting needs a collider; a BoxCollider sizes itself to the mesh bounds when added.
        panelCollider = GetComponent<Collider>();
        if (panelCollider == null)
        {
            panelCollider = gameObject.AddComponent<BoxCollider>();
        }

        if (houseRenderer != null)
        {
            unlitMaterials = houseRenderer.sharedMaterials;
        }
    }

    private void Update()
    {
        if (isAnimating || cam == null) return;

        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        Vector2 screenPos = pointer.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (pointer.press.wasPressedThisFrame)
        {
            TryBeginDrag(ray, screenPos);
        }
        else if (isDragging && pointer.press.isPressed)
        {
            if (pendingUnsnap)
            {
                if (Vector2.Distance(screenPos, pressScreenPos) < unsnapDragPixels) return;
                pendingUnsnap = false;
                Unsnap();
            }
            Drag(ray);
        }
        else if (isDragging && pointer.press.wasReleasedThisFrame)
        {
            isDragging = false;
            pendingUnsnap = false;
        }
    }

    private void TryBeginDrag(Ray ray, Vector2 screenPos)
    {
        if (!panelCollider.Raycast(ray, out RaycastHit hit, Mathf.Infinity)) return;

        // Drag along a camera-facing plane through the grab point so the panel stays under the pointer.
        dragPlane = new Plane(-cam.transform.forward, hit.point);
        grabOffset = transform.position - hit.point;
        pressScreenPos = screenPos;
        pendingUnsnap = isSnapped;
        isDragging = true;
    }

    private void Drag(Ray ray)
    {
        if (dragPlane.Raycast(ray, out float enter))
        {
            transform.position = ray.GetPoint(enter) + grabOffset;
        }

        // Measure how close the pointer ray passes to the target, so the snap works
        // even though the target sits at a different depth than the drag plane.
        if (snapTarget != null)
        {
            Vector3 toTarget = snapTarget.position - ray.origin;
            float distanceToRay = Vector3.Cross(ray.direction, toTarget).magnitude;

            // After unsnapping the pointer starts inside the snap zone; require it to leave first.
            if (!snapArmed && distanceToRay > snapDistance * 1.5f)
            {
                snapArmed = true;
            }

            if (snapArmed && distanceToRay <= snapDistance)
            {
                Snap();
            }
        }
    }

    private void Snap()
    {
        isDragging = false;
        isSnapped = true;
        StartCoroutine(SnapCoroutine());
    }

    private void Unsnap()
    {
        isSnapped = false;
        snapArmed = false;

        if (targetIndicator != null)
        {
            targetIndicator.SetActive(true); // Blink resumes from OnEnable/Update.
        }

        if (houseRenderer != null && unlitMaterials != null)
        {
            houseRenderer.sharedMaterials = unlitMaterials;
        }

        onUnsnapped?.Invoke();
    }

    IEnumerator SnapCoroutine()
    {
        isAnimating = true;
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        for (float t = 0f; t < snapDuration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / snapDuration);
            transform.SetPositionAndRotation(
                Vector3.Lerp(startPos, snapTarget.position, k),
                Quaternion.Slerp(startRot, snapTarget.rotation, k));
            yield return null;
        }
        transform.SetPositionAndRotation(snapTarget.position, snapTarget.rotation);
        isAnimating = false;

        if (targetIndicator != null)
        {
            targetIndicator.SetActive(false); // Blink.OnDisable restores its materials.
        }

        if (houseRenderer != null && litMaterial != null)
        {
            Material[] materials = houseRenderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = litMaterial;
            }
            houseRenderer.sharedMaterials = materials;
        }

        onSnapped?.Invoke();
    }

#if UNITY_EDITOR
    // Auto-fill references from this scene's naming when the component is added (or Reset).
    private void Reset()
    {
        GameObject target = GameObject.Find("TargetObjectPosition");
        if (target != null)
        {
            snapTarget = target.transform;
            targetIndicator = target;
        }

        GameObject house = GameObject.Find("House");
        if (house != null)
        {
            houseRenderer = house.GetComponent<MeshRenderer>();
        }

        litMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/With light.mat");
    }
#endif
}
