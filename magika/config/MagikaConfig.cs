
namespace magika;

public readonly record struct MagikaConfig
{
    public const string default_model_name = "standard_v1";
    public const float medium_confidence_threshold = 0.5f;
    public const int min_file_size_for_dl = 16;
    public const int padding_token = 256;
    public const int block_size = 4096;
}
