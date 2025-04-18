using UnityEngine;

[RequireComponent(typeof(Camera))]
public class WaveHeightGenerator : MonoBehaviour {
    public ComputeShader compute;
    public RenderTexture spectrum_texture;
    [Range(64, 1024)]
    public int N;

    private GameObject display_quad;
    private bool compute_configured = false;
    private const int init_spectrum_kernel = 0;

    void Start() {
        CreateDisplayQuad();
        CreateSpectrumTexture();
    }

    void OnDestroy() {
        Destroy(display_quad);
        spectrum_texture.Release();
    }

    private void Update() {
        if (!compute_configured) {
            compute.SetTexture(init_spectrum_kernel, "initial_spectrum", spectrum_texture);
            compute.SetInt("u_N", N);
            compute.SetFloat("u_L", 256f);
            compute.SetVector("u_wind_direction", new Vector4(1, 1, 0, 0).normalized);
            compute.SetFloat("u_wind_speed", 5f);
            compute.Dispatch(init_spectrum_kernel, N / 8, N / 8, 1);
            compute_configured = true;
        }
    }

    private void CreateSpectrumTexture() {
        spectrum_texture = new RenderTexture(N, N, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat,
            enableRandomWrite = true,
            useMipMap = true,
            autoGenerateMips = false,
            anisoLevel = 16,
        };
        spectrum_texture.Create();
    }

    private void CreateDisplayQuad() {
        // Create a quad
        display_quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        display_quad.transform.position = transform.position + transform.forward * 2f; // Place in front of camera
        display_quad.transform.localScale = new Vector3(1f, 1f, 1f) * 2f;
    }
}
