using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SpriteShadowCaster : MonoBehaviour
{
    private void Awake()
    {
        SpriteRenderer sr = GetComponentInParent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        // Build a mesh from the sprite's own geometry
        Mesh mesh = new Mesh();
        mesh.vertices  = System.Array.ConvertAll(sr.sprite.vertices, v => new Vector3(v.x, v.y, 0f));
        mesh.triangles = System.Array.ConvertAll(sr.sprite.triangles, t => (int)t);
        mesh.uv        = sr.sprite.uv;
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;

        // Use parent's scale so shadow matches sprite size exactly
        transform.localScale = Vector3.one;
    }
}