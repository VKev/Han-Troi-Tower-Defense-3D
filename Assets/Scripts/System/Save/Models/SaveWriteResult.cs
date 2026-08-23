namespace TowerDefense3D.GameFlow
{
    public enum SaveWriteStatus
    {
        Success,
        ValidationFailed,
        Unavailable,
        Unexpected
    }

    public readonly struct SaveWriteResult
    {
        public SaveWriteResult(SaveWriteStatus status, string error)
        {
            Status = status;
            Error = error ?? string.Empty;
        }

        public SaveWriteStatus Status { get; }
        public string Error { get; }
        public bool IsSuccess => Status == SaveWriteStatus.Success;
    }
}
