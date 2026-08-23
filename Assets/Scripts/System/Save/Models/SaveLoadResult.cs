namespace TowerDefense3D.GameFlow
{
    public enum SaveLoadStatus
    {
        Success,
        Missing,
        Corrupt,
        Incompatible,
        Unavailable,
        Unexpected
    }

    public readonly struct SaveLoadResult
    {
        public SaveLoadResult(SaveLoadStatus status, SaveSnapshot data, string error)
        {
            Status = status;
            Data = data;
            Error = error ?? string.Empty;
        }

        public SaveLoadStatus Status { get; }
        public SaveSnapshot Data { get; }
        public string Error { get; }
        public bool IsSuccess => Status == SaveLoadStatus.Success && Data != null;
    }
}
