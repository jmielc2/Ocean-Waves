using UnityEngine;

[RequireComponent(typeof(Camera))]
public class WaveHeightGenerator : MonoBehaviour {
    GameObject displayQuad;
    Material displayMaterial;

    void Start() {
        CreateDisplayQuad();
    }

    private void CreateDisplayQuad() {
        // Create a quad
        displayQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        displayQuad.transform.position = transform.position + transform.forward * 2f; // Place in front of camera
        displayQuad.transform.localScale = new Vector3(1f, 1f, 1f) * 2f;
        
        // Create a material
        displayMaterial = new Material(Shader.Find("Custom/Height Map"));
        displayQuad.GetComponent<MeshRenderer>().material = displayMaterial;
    }
}
