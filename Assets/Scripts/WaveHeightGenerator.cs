using UnityEngine;
using UnityEngine.Rendering;

public class WaveHeightGenerator : MonoBehaviour {

    public ComputeShader compute;
    public RenderTexture spectrum_texture;
    public RenderTexture height_texture;
    public RenderTexture fourier_texture;
    public Mesh ocean_mesh;
    public GameObject ocean_mesh_object;

    private bool compute_configured = false;
    private const int N = 1024;
    private const int init_spectrum_kernel = 0;
    private const int pack_minus_k_conj_kernel = 1;
    private const int cycle_through_time_kernel = 2;
    private const int horizontal_ifft_kernel = 3;
    private const int vertical_ifft_kernel = 4;

    void Start() {
        CreateSpectrumTexture();
        CreateFourierTexture();
        CreateHeightTexture();
        ocean_mesh = CreateOceanMesh();
        ocean_mesh_object.GetComponent<MeshFilter>().mesh = ocean_mesh;
    }

    void OnDestroy() {
        spectrum_texture.Release();
        fourier_texture.Release();
        height_texture.Release();
    }

    private void Update() {
        if (!compute_configured) {
            compute.SetTexture(init_spectrum_kernel, "spectrum_texture", spectrum_texture);
            compute.SetTexture(pack_minus_k_conj_kernel, "spectrum_texture", spectrum_texture);
            compute.SetTexture(cycle_through_time_kernel, "spectrum_texture", spectrum_texture);
            compute.SetTexture(cycle_through_time_kernel, "fourier_texture", fourier_texture);
            compute.SetTexture(horizontal_ifft_kernel, "fourier_texture", fourier_texture);
            compute.SetTexture(vertical_ifft_kernel, "fourier_texture", fourier_texture);
            compute.SetTexture(vertical_ifft_kernel, "height_texture", height_texture);
            compute.SetInt("u_N", N);
            compute.SetVector("u_wind_direction", new Vector4(1, 1, 0, 0).normalized);
            compute.SetFloat("u_wind_speed", 15f);
            compute.SetFloat("u_L", 256f);
            compute.Dispatch(init_spectrum_kernel, N / 8, N / 8, 1);
            compute.Dispatch(pack_minus_k_conj_kernel, N / 8, N / 8, 1);
            compute_configured = true;
        }
        float time = Time.time;
        compute.SetFloat("u_time", time);
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

    private Mesh CreateOceanMesh() {
        var mesh = new Mesh() { 
            name = "Ocean Mesh",
            subMeshCount = 1,
            indexFormat = IndexFormat.UInt32
        };
        Vector3[] vertices = new Vector3[N * N];
        int[] triangles = new int[(N - 1) * (N - 1) * 6];
        const float dim = 128f;
        for (int i = 0; i < N; i++) {
            float y = (i - N * 0.5f) * dim / N;
            for (int j = 0; j < N; j++) {
                float x = (j - N * 0.5f) * dim / N;
                vertices[i * N + j] = new Vector3(x, 0, y);
            }
        }
        
        int triangleIndex = 0;
        for (int i = 0; i < N - 1; i++) { // i is mesh row index
            for (int j = 0; j < N - 1; j++) { // j is mesh point index in current row
                int topLeft = i * N + j;
                int topRight = i * N + j + 1;
                int bottomLeft = (i + 1) * N + j;
                int bottomRight = (i + 1) * N + j + 1;

                triangles[triangleIndex] = topLeft;
                triangles[triangleIndex + 1] = topRight;
                triangles[triangleIndex + 2] = bottomLeft;

                // Vector3 tl = vertices[topLeft];
                // Vector3 tr = vertices[topRight];
                // Vector3 bl = vertices[bottomLeft];
                // Vector3 br = vertices[bottomRight];
                // Debug.Log($"{tl}, {tr}, {bl}, {br}");

                triangles[triangleIndex + 3] = topRight;
                triangles[triangleIndex + 4] = bottomRight;
                triangles[triangleIndex + 5] = bottomLeft;

                triangleIndex += 6;
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        return mesh;
    }
}
