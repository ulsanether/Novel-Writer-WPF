namespace NovelWriter.Wpf.Services;

/// <summary>
/// 설정된 백엔드(A1111/ComfyUI)로 이미지 생성 호출을 위임하는 라우터입니다.
/// 스토리 플래너·메인 뷰모델은 이 라우터 하나만 공유하고, 백엔드는 런타임에 전환됩니다.
/// </summary>
public sealed class ImageServiceRouter : IImageBackend
{
    /// <summary>AUTOMATIC1111 백엔드입니다.</summary>
    public ImageGenService A1111 { get; } = new();

    /// <summary>ComfyUI 백엔드입니다.</summary>
    public ComfyUiImageService Comfy { get; } = new();

    /// <summary>현재 사용할 백엔드입니다.</summary>
    public ImageBackendKind Backend { get; set; } = ImageBackendKind.A1111;

    private IImageBackend Active => Backend == ImageBackendKind.ComfyUi ? Comfy : A1111;

    /// <summary>현재 백엔드의 서버 주소입니다.</summary>
    public string BaseUrl
    {
        get => Active.BaseUrl;
        set => Active.BaseUrl = value;
    }

    /// <summary>가로 크기입니다. (양 백엔드에 함께 적용)</summary>
    public int Width
    {
        get => Active.Width;
        set { A1111.Width = value; Comfy.Width = value; }
    }

    /// <summary>세로 크기입니다. (양 백엔드에 함께 적용)</summary>
    public int Height
    {
        get => Active.Height;
        set { A1111.Height = value; Comfy.Height = value; }
    }

    /// <summary>샘플링 스텝입니다.</summary>
    public int Steps
    {
        get => Active.Steps;
        set { A1111.Steps = value; Comfy.Steps = value; }
    }

    /// <summary>CFG 스케일입니다.</summary>
    public double CfgScale
    {
        get => Active.CfgScale;
        set { A1111.CfgScale = value; Comfy.CfgScale = value; }
    }

    /// <summary>공통 네거티브 프롬프트입니다.</summary>
    public string NegativePrompt
    {
        get => Active.NegativePrompt;
        set { A1111.NegativePrompt = value; Comfy.NegativePrompt = value; }
    }

    /// <summary>현재 백엔드가 실행 중인지 확인합니다.</summary>
    public Task<bool> IsRunningAsync(CancellationToken cancellationToken = default)
        => Active.IsRunningAsync(cancellationToken);

    /// <summary>현재 백엔드로 이미지를 생성합니다.</summary>
    public Task<ImageGenResult?> GenerateAsync(string prompt, long seed = -1, CancellationToken cancellationToken = default)
        => Active.GenerateAsync(prompt, seed, cancellationToken);
}
