namespace NovelWriter.Wpf.Services;

/// <summary>이미지 생성 백엔드 종류입니다.</summary>
public enum ImageBackendKind
{
    /// <summary>AUTOMATIC1111 Stable Diffusion WebUI (포트 7860).</summary>
    A1111,

    /// <summary>ComfyUI (포트 8188, 최신 모델·FLUX 지원).</summary>
    ComfyUi
}

/// <summary>
/// 이미지 생성 백엔드 공통 계약입니다. A1111·ComfyUI가 이 인터페이스로 교체 가능합니다.
/// </summary>
public interface IImageBackend
{
    /// <summary>이미지 서버 주소입니다.</summary>
    string BaseUrl { get; set; }

    /// <summary>가로 크기입니다.</summary>
    int Width { get; set; }

    /// <summary>세로 크기입니다.</summary>
    int Height { get; set; }

    /// <summary>샘플링 스텝입니다.</summary>
    int Steps { get; set; }

    /// <summary>CFG 스케일입니다.</summary>
    double CfgScale { get; set; }

    /// <summary>공통 네거티브 프롬프트입니다.</summary>
    string NegativePrompt { get; set; }

    /// <summary>서버가 실행 중인지 확인합니다.</summary>
    Task<bool> IsRunningAsync(CancellationToken cancellationToken = default);

    /// <summary>프롬프트로 이미지를 생성합니다. 실패 시 null입니다.</summary>
    Task<ImageGenResult?> GenerateAsync(string prompt, long seed = -1, CancellationToken cancellationToken = default);
}
