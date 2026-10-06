using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Blink : MonoBehaviour
{
    public bool isBlink = false;
    public Material highlightMaterial; // The highlight material to use during blinking.
    public float blinkDuration = 0.5f; // Time in seconds for each blink cycle.

    private bool isBlinking = false;
    private Dictionary<MeshRenderer, Material[]> originalMaterials = new Dictionary<MeshRenderer, Material[]>();

    private void Start()
    {
        InitializeOriginalMaterials();
    }

    private void OnEnable()
    {
        // Clear stale flag so Update() can restart blinking cleanly after SetActive(false)/(true) cycles.
        isBlinking = false;
    }

    private void OnDisable()
    {
        // GameObject is being disabled: coroutines die, so flush state and restore materials.
        StopAllCoroutines();
        if (isBlinking)
        {
            ResetMaterials();
            isBlinking = false;
        }
    }

    private void Update()
    {
        if (isBlink && !isBlinking)
        {
            StartBlink();
        }
        else if (!isBlink && isBlinking)
        {
            StopBlink();
        }
    }

    private void InitializeOriginalMaterials()
    {
        MeshRenderer[] allMeshRenderers = GetComponentsInChildren<MeshRenderer>(true);

        foreach (MeshRenderer meshRenderer in allMeshRenderers)
        {
            originalMaterials[meshRenderer] = meshRenderer.materials;
        }
    }

    private void StartBlink()
    {
        isBlinking = true;
        MeshRenderer[] allMeshRenderers = GetComponentsInChildren<MeshRenderer>(true);

        StartCoroutine(BlinkCoroutine(allMeshRenderers));
    }

    private void StopBlink()
    {
        isBlinking = false;
        StopAllCoroutines();
        ResetMaterials();
    }

    private void ResetMaterials()
    {
        foreach (var entry in originalMaterials)
        {
            MeshRenderer meshRenderer = entry.Key;
            Material[] materials = entry.Value;

            meshRenderer.materials = materials; // Restore original materials.
        }
    }

    IEnumerator BlinkCoroutine(MeshRenderer[] allMeshRenderers)
    {
        while (isBlink)
        {
            // Apply highlight materials
            foreach (MeshRenderer meshRenderer in allMeshRenderers)
            {
                Material[] highlightMaterials = new Material[meshRenderer.materials.Length];
                for (int i = 0; i < highlightMaterials.Length; i++)
                {
                    highlightMaterials[i] = highlightMaterial; // Set highlight material.
                }
                meshRenderer.materials = highlightMaterials;
            }

            yield return new WaitForSeconds(blinkDuration);

            // Restore original materials
            ResetMaterials();

            yield return new WaitForSeconds(blinkDuration);
        }

        // Ensure the materials are reset after blinking is stopped
        ResetMaterials();
    }
}
