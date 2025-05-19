using UnityEngine;

[RequireComponent(typeof(Camera))]
public class WaveHeightGenerator : MonoBehaviour {
    public enum Resolution {
        TWO_FIFTY_SIX = 256, FIVE_TWELVE = 512, TEN_TWENTY_FOUR = 1024
    }

    public ComputeShader compute;
    public RenderTexture spectrum_texture;
    public RenderTexture height_texture;
    public RenderTexture fourier_texture;
    public Resolution resolution = Resolution.TEN_TWENTY_FOUR;

    private int N;
    private bool compute_configured = false;
    private const int init_spectrum_kernel = 0;
    private const int pack_minus_k_conj_kernel = 1;
    private const int cycle_through_time_kernel = 2;
    private const int horizontal_ifft_kernel = 3;
    private const int vertical_ifft_kernel = 4;

    void Start() {
        N = (int)resolution;
        CreateSpectrumTexture();
        CreateFourierTexture();
        CreateHeightTexture();
    }

    void OnDestroy() {
        spectrum_texture.Release();
        fourier_texture.Release();
        height_texture.Release();
    }

    private void Update() {
        float time = Time.time + 10.0f;
        compute.SetTexture(init_spectrum_kernel, "spectrum_texture", spectrum_texture);
        compute.SetTexture(pack_minus_k_conj_kernel, "spectrum_texture", spectrum_texture);
        compute.SetTexture(cycle_through_time_kernel, "spectrum_texture", spectrum_texture);
        compute.SetTexture(cycle_through_time_kernel, "fourier_texture", fourier_texture);
        compute.SetTexture(horizontal_ifft_kernel, "fourier_texture", fourier_texture);
        compute.SetTexture(vertical_ifft_kernel, "fourier_texture", fourier_texture);
        compute.SetTexture(vertical_ifft_kernel, "height_texture", height_texture);
        compute.SetInt("u_N", N);
        compute.SetFloat("u_L", 256f);
        compute.SetFloat("u_time", time);
        compute.SetVector("u_wind_direction", new Vector4(1, 1, 0, 0).normalized);
        compute.SetFloat("u_wind_speed", 15f);
        if (!compute_configured) {
            compute.Dispatch(init_spectrum_kernel, N / 8, N / 8, 1);
            compute.Dispatch(pack_minus_k_conj_kernel, N / 8, N / 8, 1);
            compute_configured = true;
        }
        compute.Dispatch(cycle_through_time_kernel, N / 8, N / 8, 1);
        compute.Dispatch(horizontal_ifft_kernel, 1, N, 1);
        compute.Dispatch(vertical_ifft_kernel, N, 1, 1);
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

    private void CreateFourierTexture() {
        fourier_texture = CreateRenderTexture(N, N, 0, RenderTextureFormat.RGFloat);
    }

    private void CreateHeightTexture() {
        height_texture = CreateRenderTexture(N, N, 0, RenderTextureFormat.RFloat);
    }
}
