namespace SqueezeIt.Models
{
    public class UserConfigs
    {
        public bool UseLossLess { get; set; }
        public bool OverwriteOriginal { get; set; }
        public bool KeepOriginalMetadata { get; set; }
        public bool KeepModificationDate { get; set; }
        public int CompressionType { get; set; }
        public int CompressionQuality { get; set; }
        public int ResizeOption { get; set; }
    }
}
