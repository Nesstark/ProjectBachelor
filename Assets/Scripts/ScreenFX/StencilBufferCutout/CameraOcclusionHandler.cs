using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

public class CameraOcclusionHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Material occluderCutoutMaterial;

    [Header("Settings")]
    [SerializeField] private LayerMask occluderMask;
    [SerializeField] private float playerHeightOffset = 0.7f; // aim at sprite center

    private Camera _cam;
    private readonly Dictionary<Renderer, Material[]> _originalMaterials = new();
    private readonly HashSet<Renderer> _activeOccluders = new();

    void Start() => _cam = Camera.main;

    void LateUpdate()
    {
        // Target the center of your capsule, not the feet
        Vector3 targetPos = transform.position + Vector3.up * playerHeightOffset;
        Vector3 camPos = _cam.transform.position;
        Vector3 dir = targetPos - camPos;

        RaycastHit[] hits = Physics.RaycastAll(camPos, dir.normalized, dir.magnitude, occluderMask);
        var newOccluders = new HashSet<Renderer>();

        foreach (var hit in hits)
        {
            var rend = hit.collider.GetComponentInParent<Renderer>();
            if (rend == null) continue;
            newOccluders.Add(rend);

            if (!_originalMaterials.ContainsKey(rend))
            {
                _originalMaterials[rend] = rend.sharedMaterials;
                var swapped = new Material[rend.sharedMaterials.Length];
                for (int i = 0; i < swapped.Length; i++) swapped[i] = occluderCutoutMaterial;
                rend.materials = swapped;
            }
        }

        // Restore renderers that are no longer blocked
        foreach (var rend in _activeOccluders)
        {
            if (!newOccluders.Contains(rend) && _originalMaterials.TryGetValue(rend, out var original))
            {
                rend.materials = original;
                _originalMaterials.Remove(rend);
            }
        }

        _activeOccluders.Clear();
        foreach (var r in newOccluders) _activeOccluders.Add(r);
    }
}