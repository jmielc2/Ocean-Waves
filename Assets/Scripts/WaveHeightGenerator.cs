using UnityEngine;
using UnityEngine.Rendering;

public class WaveHeightGenerator : MonoBehaviour {
    public ComputeShader oceanCompute;
    public Shader oceanShader;

    public RenderTexture normal_texture;
    public RenderTexture displacement_texture;

    private RenderTexture spectrum_texture;
    private RenderTexture horizontal_fourier_texture;
    private RenderTexture vertical_fourier_texture;
    private Mesh ocean_mesh;
    private GameObject ocean_mesh_object;
    private bool compute_configured = false;
    private const int N = 1024; // ocean plane resolution 
    private const int L = 128; // ocean plane length
    private const int init_spectrum_kernel = 0;
    private const int pack_minus_k_conj_kernel = 1;
    private const int cycle_through_time_kernel = 2;
    private const int horizontal_ifft_kernel = 3;
    private const int vertical_ifft_kernel = 4;

    void Start() {
        CreateSpectrumTexture();
        CreateFourierTextures();
        CreateDisplacementTexture();
        CreateNormalTexture();
        ocean_mesh = CreateOceanMesh();
        ocean_mesh_object = transform.GetChild(0).gameObject;
        ocean_mesh_object.GetComponent<MeshFilter>().mesh = ocean_mesh;
        ocean_mesh_object.GetComponent<MeshRenderer>().material = CreateOceanMaterial();
    }

    void OnDestroy() {
        spectrum_texture.Release();
        vertical_fourier_texture.Release();
        horizontal_fourier_texture.Release();
        displacement_texture.Release();
        normal_texture.Release();
    }

    private void Update() {
        if (!compute_configured) {
            // Spectrum Texture Generation
            oceanCompute.SetTexture(init_spectrum_kernel, "spectrum_texture", spectrum_texture);
            oceanCompute.SetTexture(pack_minus_k_conj_kernel, "spectrum_texture", spectrum_texture);

            // Fourier Texture Preparation
            oceanCompute.SetTexture(cycle_through_time_kernel, "spectrum_texture", spectrum_texture);
            oceanCompute.SetTexture(cycle_through_time_kernel, "vertical_fourier_texture", vertical_fourier_texture);
            oceanCompute.SetTexture(cycle_through_time_kernel, "horizontal_fourier_texture", horizontal_fourier_texture);

            // IFFT Texture Loading
            oceanCompute.SetTexture(horizontal_ifft_kernel, "vertical_fourier_texture", vertical_fourier_texture);
            oceanCompute.SetTexture(horizontal_ifft_kernel, "horizontal_fourier_texture", horizontal_fourier_texture);
            oceanCompute.SetTexture(vertical_ifft_kernel, "vertical_fourier_texture", vertical_fourier_texture);
            oceanCompute.SetTexture(vertical_ifft_kernel, "horizontal_fourier_texture", horizontal_fourier_texture);
            oceanCompute.SetTexture(vertical_ifft_kernel, "displacement_texture", displacement_texture);
            oceanCompute.SetTexture(vertical_ifft_kernel, "normal_texture", normal_texture);

            // General Uniform Configuration
            oceanCompute.SetInt("u_N", N);
            oceanCompute.SetVector("u_wind_direction", new Vector4(1, 1, 0, 0).normalized);
            oceanCompute.SetFloat("u_wind_speed", 50f);
            oceanCompute.SetFloat("u_L", (float)L);
            oceanCompute.Dispatch(init_spectrum_kernel, N / 8, N / 8, 1);
            oceanCompute.Dispatch(pack_minus_k_conj_kernel, N / 8, N / 8, 1);
            compute_configured = true;
        }
        float time = Time.time;
        oceanCompute.SetFloat("u_time", time);
        oceanCompute.Dispatch(cycle_through_time_kernel, N / 8, N / 8, 1);
        oceanCompute.Dispatch(horizontal_ifft_kernel, 1, N, 1);
        oceanCompute.Dispatch(vertical_ifft_kernel, N, 1, 1);
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

    private void CreateFourierTextures() {
        vertical_fourier_texture = CreateRenderTexture(N, N, 0, RenderTextureFormat.RGFloat);
        horizontal_fourier_texture = CreateRenderTexture(N, N, 0, RenderTextureFormat.ARGBFloat);
    }

    private void CreateDisplacementTexture() {
        displacement_texture = CreateRenderTexture(N, N, 0, RenderTextureFormat.ARGBFloat);
    }

    private void CreateNormalTexture() {
        normal_texture = CreateRenderTexture(N, N, 0, RenderTextureFormat.ARGBFloat);
    }

    private Mesh CreateOceanMesh() {
        var mesh = new Mesh()
        {
            name = "Ocean Mesh",
            subMeshCount = 1,
            indexFormat = IndexFormat.UInt32
        };
        Vector3[] vertices = new Vector3[N * N];
        int[] triangles = new int[(N - 1) * (N - 1) * 6];
        Vector2[] uvs = new Vector2[N * N];

        for (int i = 0; i < N; i++)
        {
            float y = (i - N * 0.5f) * L / N;
            for (int j = 0; j < N; j++)
            {
                float x = (j - N * 0.5f) * L / N;
                vertices[i * N + j] = new Vector3(x, 0, y);
                uvs[i * N + j] = new Vector2((float) j / N, (float) i / N);
            }
        }

        int triangleIndex = 0;
        for (int i = 0; i < N - 1; i++)
        { // i is mesh row index
            for (int j = 0; j < N - 1; j++)
            { // j is mesh point index in current row
                int topLeft = i * N + j;
                int topRight = i * N + j + 1;
                int bottomLeft = (i + 1) * N + j;
                int bottomRight = (i + 1) * N + j + 1;

                triangles[triangleIndex] = topLeft;
                triangles[triangleIndex + 1] = topRight;
                triangles[triangleIndex + 2] = bottomLeft;

                triangles[triangleIndex + 3] = topRight;
                triangles[triangleIndex + 4] = bottomRight;
                triangles[triangleIndex + 5] = bottomLeft;

                triangleIndex += 6;
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        return mesh;
    }

    private Material CreateOceanMaterial() {
        Material mat = new Material(oceanShader);
        mat.SetTexture("displacement_texture", displacement_texture);
        mat.SetTexture("normal_texture", normal_texture);
        mat.SetFloat("N", (float)N);
        mat.SetFloat("L", (float)L);
        return mat;
    }
}
