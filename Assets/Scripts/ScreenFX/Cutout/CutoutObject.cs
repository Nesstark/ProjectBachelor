using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CutoutObject : MonoBehaviour
{
    [SerializeField]
    private Transform targetObject;

    [SerializeField]
    private LayerMask wallMask;

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = GetComponent<Camera>();
    }

    private void Update()
    {
        Vector2 cutoutPos = mainCamera.WorldToViewportPoint(targetObject.position);

        Vector3 offset = targetObject.position - transform.position;
        RaycastHit[] hitObjects = Physics.RaycastAll(transform.position, offset, offset.magnitude, wallMask);

        for (int i = 0; i < hitObjects.Length; ++i)
        {
            SpriteRenderer sr = hitObjects[i].transform.GetComponent<SpriteRenderer>();
            if (sr == null) continue;

            sr.material.SetVector("_CutoutPos", cutoutPos);
            sr.material.SetFloat("_CutoutSize", 0.15f);   // tweak to taste
            sr.material.SetFloat("_FalloffSize", 0.05f);
        }
    }
}
