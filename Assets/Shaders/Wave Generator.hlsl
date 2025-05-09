#pragma kernel InitializeSpectrum
#pragma kernel CycleThroughTime
#pragma kernel HorizontalIFFT
#pragma kernel VerticalIFFT

#define PI 3.14159265358979323846
#define G 9.8

RWTexture2D<float4> spectrum_texture;
RWTexture2D<float2> fourier_texture;
RWTexture2D<float> height_texture;

uniform uint u_N;
uniform float u_L;
uniform float u_time;
uniform float2 u_wind_direction;
uniform float u_wind_speed;

// Huga Elias integer hash
float Hash(uint n) {
    n = (n << 13U) ^ n;
    n = n * (n * n * 15731U + 0x789221U) + 0x1376312589U;
    return float(n & uint(0x7fffffffU)) / float(0x7fffffff);
}

// Box Muller transform
float4 UniformToGaussianDistribution(float u1, float u2, float u3, float u4) {
    float2 R = float2(sqrt(-2.0f * log(u1)), sqrt(-2.0f * log(u3)));
    float2 theta = float2(2.0f * PI * u2, 2.0f * PI * u4);
    return float4(R.x * cos(theta.x), R.x * sin(theta.x), R.y * cos(theta.y), R.y * sin(theta.y));
}

float PhillipsSpectrum(float2 wavevector) {
    const float L = u_wind_speed * u_wind_speed / G;
    const float k = length(wavevector);
    const float inv_k = 1.0f / k;
    const float2 direction = wavevector / k;
    const float inv_k_times_L = 1.0f * inv_k / L;
    const float wind_dot_wavedirection = dot(u_wind_direction, direction);
    return 0.0081f * exp(-1.0f * inv_k_times_L * inv_k_times_L) * inv_k * inv_k * inv_k * inv_k * wind_dot_wavedirection * wind_dot_wavedirection;
}

float ShortWaveSuppressionFactor(float2 wavevector, float threshold, float falloff = 1.0f) {
    const float k = length(wavevector);
    if (k >= threshold) {
        const float diff = (k - threshold) / falloff;
        return exp(-1.0f * diff * diff);
    }
    return 1.0f;
}

float2 ComplexAdd(float2 a, float2 b) {
    return a + b;
}

float2 ComplexMul(float2 a, float2 b) {
    return float2((a.x * b.x) - (a.y * b.y), (a.x * b.y) + (a.y * b.x));
}

float2 ComplexConj(float2 a) {
    return float2(a.x, -1.0f * a.y);
}

// Referenced from https://graphics.stanford.edu/~seander/bithacks.html#ReverseParallel
int ReverseBits(int v, int num_bits = 16) {
    v = ((v >> 1) & 0x55555555) | ((v & 0x55555555) << 1);
    v = ((v >> 2) & 0x33333333) | ((v & 0x33333333) << 2);
    v = ((v >> 4) & 0x0F0F0F0F) | ((v & 0x0F0F0F0F) << 4);
    v = ((v >> 8) & 0x00FF00FF) | ((v & 0x00FF00FF) << 8);
    return v >> (16 - num_bits);
}

/*
* Kernels
*/

[numthreads(8, 8, 1)]
void InitializeSpectrum(uint3 id : SV_DispatchThreadID) {
    const uint seed = id.y * u_N + id.x;
    const float scalar = 2.0f * PI / u_L;
    const float threshold = u_N * 0.5f * scalar;
    const float2 wavevector = id.xy * scalar - threshold;
    const float4 gaussians = UniformToGaussianDistribution(Hash(seed), Hash(seed * 2), Hash(seed * 3), Hash(seed * 4));
    const float short_wave_suppression = ShortWaveSuppressionFactor(wavevector, threshold, (sqrt(2.0f) - 1.0f) * threshold);
    const float spectrum_k = PhillipsSpectrum(wavevector) * short_wave_suppression;
    const float spectrum_minus_k = PhillipsSpectrum(-1.0f * wavevector) * short_wave_suppression;
    // const float spectrum = JonswapSpectrum(wavevector);
    float4 amplitudes = float4(sqrt(0.5f * spectrum_k) * gaussians.xy, sqrt(0.5f * spectrum_minus_k) * gaussians.zw);
    
    spectrum_texture[id.xy] = amplitudes;
}

[numthreads(8, 8, 1)]
void CycleThroughTime(uint3 id : SV_DispatchThreadID) {
    const float scalar = 2.0f * PI / u_L;
    const float2 wavevector = (id.xy - (u_N * 0.5f)) * scalar;
    const float magnitude = length(wavevector);
    const float w = sqrt(magnitude * G);
    const float4 amplitudes = spectrum_texture[id.xy];
    float2 euler_formula = float2(cos(w * u_time), sin(w * u_time));
    const float2 euler_formula_conj = ComplexConj(euler_formula);

    fourier_texture[id.xy] = ComplexMul(amplitudes.xy, euler_formula) + ComplexMul(ComplexConj(amplitudes.zw), euler_formula_conj);
}


// IFFT Relevant Defines

#define SIZE 1024
#define LOG2_SIZE 10
groupshared float2 butterfly_buffer[2][SIZE];

// Referenced from  Fynn-Jorin Fl�gge's Thesis "Realtime GPGPU FFT Ocean Water Simulation"
float4 CalcButterflyOperands(int step, int index) {
    float4 butterfly_operands = float4(0.0f, 0.0f, 0.0f, 0.0f);
    const float butterfly_span = pow(2.0f, step);
    const float butterfly_span_doubled = butterfly_span * 2.0f;
    const float k = fmod(index * SIZE / butterfly_span_doubled, SIZE);
    const float euler_exponent = 2.0f * PI * k / SIZE;
    sincos(euler_exponent, butterfly_operands.y, butterfly_operands.x);
    bool isTopWing = false;
    if (fmod(index, butterfly_span_doubled) < butterfly_span) {
        isTopWing = true;
    }

    if (step == 0) {
        if (isTopWing) {
            butterfly_operands.zw = float2(
                float(ReverseBits(index, LOG2_SIZE)),
                float(ReverseBits(index + 1, LOG2_SIZE))
            );
        } else {
            butterfly_operands.xy *= -1.0f;
            butterfly_operands.zw = float2(
                float(ReverseBits(index - 1, LOG2_SIZE)),
                float(ReverseBits(index, LOG2_SIZE))
            );
        }
    } else {
        if (isTopWing) {
            butterfly_operands.zw = float2(
                float(index),
                float(index + butterfly_span)
            );
        } else {
            butterfly_operands.xy *= -1.0f;
            butterfly_operands.zw = float2(
                float(index - butterfly_span),
                float(index)
            );
        }
    }
    return butterfly_operands;
}

// Referenced from Garrett Gunnell's implementation of IFFT in his water simulation
[numthreads(SIZE, 1, 1)]
void HorizontalIFFT(uint3 id : SV_DispatchThreadID) {
    butterfly_buffer[0][id.x] = fourier_texture[id.xy];
    bool butterfly_direction = false;
    GroupMemoryBarrierWithGroupSync();
    
    [unroll]
    for (int step = 0; step < LOG2_SIZE; step++) {
        const float4 butterfly_operands = CalcButterflyOperands(step, id.x);
        const float2 a = butterfly_buffer[butterfly_direction][butterfly_operands.z];
        const float2 b = butterfly_buffer[butterfly_direction][butterfly_operands.w];
        const float2 twiddle = butterfly_operands.xy;
        butterfly_buffer[!butterfly_direction][id.x] = ComplexAdd(a, ComplexMul(twiddle, b));
        butterfly_direction = !butterfly_direction;
        GroupMemoryBarrierWithGroupSync();
    }
    fourier_texture[id.xy] = butterfly_buffer[butterfly_direction][id.x];
}

[numthreads(1, SIZE, 1)]
void VerticalIFFT(uint3 id : SV_DispatchThreadID) {
    butterfly_buffer[0][id.x] = fourier_texture[id.xy];
    bool butterfly_direction = false;
    GroupMemoryBarrierWithGroupSync();

    [unroll]
    for (int step = 0; step < LOG2_SIZE; step++) {
        const float4 butterfly_operands = CalcButterflyOperands(step, id.y);
        const float2 a = butterfly_buffer[butterfly_direction][butterfly_operands.z];
        const float2 b = butterfly_buffer[butterfly_direction][butterfly_operands.z];
        const float2 twiddle = butterfly_operands.xy;
        butterfly_buffer[!butterfly_direction][id.y] = ComplexAdd(a, ComplexMul(twiddle, b));
        butterfly_direction = !butterfly_direction;
        GroupMemoryBarrierWithGroupSync();
    }
    
    float perm = 1.0f;
    if ((id.x + id.y) % 2 == 1) {
        perm = -1.0f;
    }
    
    height_texture[id.xy] = perm * butterfly_buffer[butterfly_direction][id.y].x / (u_N * u_N);
}

