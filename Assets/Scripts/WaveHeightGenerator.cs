using UnityEngine;

[RequireComponent(typeof(Camera))]
public class WaveHeightGenerator : MonoBehaviour {
    public ComputeShader compute;
    public RenderTexture spectrum_texture;
    public RenderTexture height_texture;
    public RenderTexture butterfly_texture;
    public RenderTexture fourier_texture;

    [Range(64, 1024)]
    public int N;

    private int N_times_N_log2;
    private bool compute_configured = false;
    private const int init_spectrum_kernel = 0;
    private const int generate_butterfly_texture_kernel = 1;
    private const int cycle_through_time_kernel = 2;
    private const int horizontal_ifft_kernel = 3;
    private const int vertical_ifft_kernel = 4;

    void Start() {
        N_times_N_log2 = (int)Mathf.Log(N * N, 2.0f);
        CreateSpectrumTexture();
        CreateButterflyTexture();
        CreateFourierTexture();
        CreateHeightTexture();
    }

    void OnDestroy() {
        spectrum_texture.Release();
        butterfly_texture.Release();
        fourier_texture.Release();
        height_texture.Release();
    }

    private void Update() {
        if (!compute_configured) {
            float time = 100f;
            compute.SetTexture(init_spectrum_kernel, "spectrum_texture", spectrum_texture);
            compute.SetTexture(generate_butterfly_texture_kernel, "butterfly_texture", butterfly_texture);
            compute.SetTexture(cycle_through_time_kernel, "spectrum_texture", spectrum_texture);
            compute.SetTexture(cycle_through_time_kernel, "fourier_texture", fourier_texture);
            compute.SetInt("u_N", N);
            compute.SetFloat("u_L", 256f);
            compute.SetFloat("u_time", time);
            compute.SetVector("u_wind_direction", new Vector4(1, 1, 0, 0).normalized);
            compute.SetFloat("u_wind_speed", 5f);
            compute.Dispatch(init_spectrum_kernel, N / 8, N / 8, 1);
            compute.Dispatch(generate_butterfly_texture_kernel, N_times_N_log2, N * N / 64, 1);
            compute.Dispatch(cycle_through_time_kernel, N / 8, N / 8, 1);
            // Do Horizontal and Vertical IFFTs
            int pingpong_iteration = 0;
            int pingpong_direction = 0;
            
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
        spectrum_texture = CreateRenderTexture(N, N, 0, RenderTextureFormat.ARGBFloat);
    }

    private void CreateButterflyTexture() {
        butterfly_texture = CreateRenderTexture(N_times_N_log2, N * N, 0, RenderTextureFormat.ARGBFloat);
    }

    private void CreateFourierTexture() {
        fourier_texture = CreateRenderTexture(N, N, 0, RenderTextureFormat.RGFloat);
    }

    private void CreateHeightTexture() {
        height_texture = CreateRenderTexture(N, N, 0, RenderTextureFormat.RFloat);
    }
}
