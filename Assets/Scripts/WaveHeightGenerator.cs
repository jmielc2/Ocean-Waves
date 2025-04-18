using UnityEngine;

[RequireComponent(typeof(Camera))]
public class WaveHeightGenerator : MonoBehaviour {
    public ComputeShader compute;
    public RenderTexture spectrum_texture;
    public RenderTexture height_texture;
    [Range(64, 1024)]
    public int N;

    private bool compute_configured = false;
    private const int init_spectrum_kernel = 0;
    private const int generate_height_spectrum_kernel = 1;

    void Start() {
        CreateSpectrumTexture();
        CreateHeightTexture();
    }

    void OnDestroy() {
        spectrum_texture.Release();
        height_texture.Release();
    }

    private void Update() {
        if (!compute_configured) {
            compute.SetTexture(init_spectrum_kernel, "spectrum_texture", spectrum_texture);
            compute.SetTexture(generate_height_spectrum_kernel, "spectrum_texture", spectrum_texture);
            compute.SetTexture(generate_height_spectrum_kernel, "height_texture", height_texture);
            compute.SetInt("u_N", N);
            compute.SetFloat("u_L", 256f);
            compute.SetVector("u_wind_direction", new Vector4(1, 1, 0, 0).normalized);
            compute.SetFloat("u_wind_speed", 5f);
            compute.Dispatch(init_spectrum_kernel, N / 8, N / 8, 1);
            compute.Dispatch(generate_height_spectrum_kernel, N / 8, N / 8, 1);
            compute_configured = true;
        }
    }

    private RenderTexture CreateRenderTexture(int width, int height, int depth, RenderTextureFormat format) {
        var texture = new RenderTexture(width, height, depth, format, RenderTextureReadWrite.Linear) {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat,
            enableRandomWrite = true,
            useMipMap = true,
            autoGenerateMips = false,
            anisoLevel = 16
        };
        texture.Create();
        return texture;
    }

    private void CreateSpectrumTexture() {
        spectrum_texture = CreateRenderTexture(N, N, 0, RenderTextureFormat.RGFloat);
    }

    private void CreateHeightTexture() {
        height_texture = CreateRenderTexture(N, N, 0, RenderTextureFormat.RFloat);
    }
}
