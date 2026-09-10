namespace TowerDefense3D.Audio
{
    public interface ISoundPlayer
    {
        bool Play(SoundId id);
        void Stop(SoundId id);
        void SetVolume(SoundId id, float normalizedVolume);
    }
}
