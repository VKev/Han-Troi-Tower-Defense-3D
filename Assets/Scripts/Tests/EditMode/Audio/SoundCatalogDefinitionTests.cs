using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense3D.Audio;
using TowerDefense3D.GameFlow;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.Audio.Tests.EditMode
{
    public sealed class SoundCatalogDefinitionTests
    {
        private const string CatalogPath = "Assets/Config/Audio/SoundCatalog.asset";
        private const string LobbyMusicPath = "Assets/Resources/Audio/Music/LobbyMusic.wav";
        private const string GameStartClickPath = "Assets/Resources/Audio/click1.ogg";
        private const string LevelSelectedPath = "Assets/Resources/Audio/10_blip_digital.wav";
        private const string EnterLevelPath = "Assets/Resources/Audio/rollover5.ogg";
        private const string WavePreparationMusicPath = "Assets/Resources/Audio/Music/WavePrepare.mp3";
        private const string TutorialTypingPath = "Assets/Resources/Audio/generated-004_medium.wav";

        [Test]
        public void SoundCatalog_IsCreatedAtTheProjectConfigPath()
        {
            SoundCatalogDefinition catalog = AssetDatabase.LoadAssetAtPath<SoundCatalogDefinition>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.CollectValidationErrors(), Is.Empty);
            Assert.That(catalog.TryGet(SoundId.LobbyMusic, out SoundDefinition lobbyMusic), Is.True);
            Assert.That(lobbyMusic.Clip, Is.EqualTo(AssetDatabase.LoadAssetAtPath<AudioClip>(LobbyMusicPath)));
            Assert.That(lobbyMusic.Loop, Is.True);
            Assert.That(lobbyMusic.IsProtected, Is.True);
            Assert.That(catalog.TryGet(SoundId.GameStarted, out SoundDefinition gameStartClick), Is.True);
            Assert.That(gameStartClick.Clip, Is.EqualTo(AssetDatabase.LoadAssetAtPath<AudioClip>(GameStartClickPath)));
            Assert.That(gameStartClick.Loop, Is.False);
            Assert.That(catalog.TryGet(SoundId.LevelSelected, out SoundDefinition levelSelected), Is.True);
            Assert.That(levelSelected.Clip, Is.EqualTo(AssetDatabase.LoadAssetAtPath<AudioClip>(LevelSelectedPath)));
            Assert.That(catalog.TryGet(SoundId.EnterLevel, out SoundDefinition enterLevel), Is.True);
            Assert.That(enterLevel.Clip, Is.EqualTo(AssetDatabase.LoadAssetAtPath<AudioClip>(EnterLevelPath)));
            Assert.That(catalog.TryGet(SoundId.LevelPreparationMusic, out SoundDefinition preparationMusic), Is.True);
            Assert.That(preparationMusic.Clip, Is.EqualTo(AssetDatabase.LoadAssetAtPath<AudioClip>(WavePreparationMusicPath)));
            Assert.That(preparationMusic.Loop, Is.True);
            Assert.That(catalog.TryGet(SoundId.TutorialTyping, out SoundDefinition tutorialTyping), Is.True);
            Assert.That(tutorialTyping.Clip, Is.EqualTo(AssetDatabase.LoadAssetAtPath<AudioClip>(TutorialTypingPath)));
            Assert.That(tutorialTyping.Loop, Is.True);
        }

        [Test]
        public void LobbyMusic_UsesStreamingImportForMobileMemory()
        {
            var importer = AssetImporter.GetAtPath(LobbyMusicPath) as AudioImporter;

            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming));
        }

        [Test]
        public void LobbyMusicSystem_PlaysOutsideLevelsAndStopsDuringLevelTransitions()
        {
            var gameFlow = new GameFlowSystem(null, null, null, null);
            var player = new RecordingSoundPlayer();
            var system = new LobbyMusicSystem(gameFlow, player);

            system.Start();
            gameFlow.SetState(GameFlowState.LevelMenu);
            gameFlow.SetState(GameFlowState.LoadingLevel);
            gameFlow.SetState(GameFlowState.Gameplay);
            gameFlow.SetState(GameFlowState.LoadingLevel);
            gameFlow.SetState(GameFlowState.LevelMenu);
            system.Dispose();

            CollectionAssert.AreEqual(
                new[] { SoundId.LobbyMusic, SoundId.LobbyMusic },
                player.Played);
            CollectionAssert.AreEqual(
                new[] { SoundId.LobbyMusic, SoundId.LobbyMusic },
                player.Stopped);
        }

        private sealed class RecordingSoundPlayer : ISoundPlayer
        {
            public List<SoundId> Played { get; } = new List<SoundId>();
            public List<SoundId> Stopped { get; } = new List<SoundId>();

            public bool Play(SoundId id)
            {
                Played.Add(id);
                return true;
            }

            public void Stop(SoundId id)
            {
                Stopped.Add(id);
            }

            public void SetVolume(SoundId id, float normalizedVolume)
            {
            }
        }
    }
}
