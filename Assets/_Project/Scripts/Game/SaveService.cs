using System;
using System.Collections.Generic;
using CatMerge.Core;
using CatMerge.Platform;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatMerge.Game
{
    /// <summary>
    /// Loads progress on start (or starts a new game), saves it as JSON through <see cref="IPlatformService"/> every
    /// <see cref="autosaveInterval"/> seconds, when the app loses focus / is paused and on quit, and pays offline
    /// income for time spent away — both after a restart and after coming back to a backgrounded tab.
    /// </summary>
    public sealed class SaveService : MonoBehaviour
    {
        public const string SaveKey = "catmerge_save";

        [SerializeField] private GameController game;
        [SerializeField, Min(1f)] private float autosaveInterval = 10f;

        private readonly SaveData _data = new SaveData();
        private IPlatformService _platform;
        private bool _loaded;
        private bool _resetting;
        private float _autosaveTimer;

        // Background tracking: offline time = wall-clock time away minus the game time that still ran meanwhile
        // (the editor and some browsers keep updating an unfocused game, and that income was already paid).
        private bool _away;
        private long _awaySinceUnix;
        private double _playedWhileAway;

        /// <summary>(coins, seconds that counted) — offline income was just added to the wallet.</summary>
        public event Action<double, double> OfflineIncomeGranted;

        /// <summary>Last offline payout, kept so a popup created later than the event can still show it.</summary>
        public (double coins, double seconds)? PendingOfflineIncome { get; private set; }

        public void ClearPendingOfflineIncome() => PendingOfflineIncome = null;

        private static long NowUnix => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private void Start()
        {
            if (game.State == null)
            {
                enabled = false; // GameController failed to init and already logged why
                return;
            }

            _platform = PlatformProvider.Current;
            _platform.Load(SaveKey, OnLoaded);
        }

        private void OnLoaded(string json)
        {
            if (this == null || _loaded) return; // destroyed while an async load was pending

            var saved = Parse(json);
            if (saved == null)
            {
                game.StartNewGame();
            }
            else
            {
                game.Restore(saved);
                _data.lastSeenUnix = saved.lastSeenUnix;
                GrantOffline(OfflineIncome.ElapsedSeconds(saved.lastSeenUnix, NowUnix));
            }

            _loaded = true;
            Save();
        }

        private void Update()
        {
            if (_away) _playedWhileAway += Time.deltaTime;
            if (!_loaded) return;

            _autosaveTimer += Time.unscaledDeltaTime;
            if (_autosaveTimer >= autosaveInterval) Save();
        }

        private void OnApplicationPause(bool paused) => SetAway(paused);

        private void OnApplicationFocus(bool focused) => SetAway(!focused);

        private void OnApplicationQuit() => Save();

        private void SetAway(bool away)
        {
            if (!_loaded || away == _away) return;
            _away = away;

            if (away)
            {
                Save();
                // Not NowUnix: after the clock was turned back, the away time must still start from the latest
                // time already paid for, or moving the clock forward again would pay the same hours twice.
                _awaySinceUnix = _data.lastSeenUnix;
                _playedWhileAway = 0;
                return;
            }

            double seconds = OfflineIncome.ElapsedSeconds(_awaySinceUnix, NowUnix) - _playedWhileAway;
            GrantOffline(seconds);
            Save();
        }

        /// <summary>Writes the current progress now. Does nothing before loading has finished or during a reset.</summary>
        public void Save()
        {
            if (!_loaded || _resetting) return;
            _autosaveTimer = 0f;

            game.WriteTo(_data);
            _data.lastSeenUnix = OfflineIncome.NextLastSeen(_data.lastSeenUnix, NowUnix);
            _platform.Save(SaveKey, JsonUtility.ToJson(_data));
        }

        /// <summary>Debug: erases the save and reloads the scene as a brand-new game.</summary>
        public void ResetProgress()
        {
            _resetting = true;
            _platform?.Save(SaveKey, string.Empty);
            ReloadScene();
        }

        /// <summary>
        /// Moving to a new home: adds the fish, saves only what is kept (fish, moves, Catpedia) and reloads the scene,
        /// which then starts a fresh run in the next home. False if the move isn't available.
        /// </summary>
        public bool MoveToNewHome()
        {
            if (!_loaded || _resetting || !game.Prestige.TryMove(out _)) return false;

            var kept = new SaveData
            {
                startNewRun = true,
                lastSeenUnix = OfflineIncome.NextLastSeen(_data.lastSeenUnix, NowUnix),
            };
            game.WriteKeptTo(kept);
            _platform.Save(SaveKey, JsonUtility.ToJson(kept));

            _resetting = true; // no autosave of the old run in between
            ReloadScene();
            return true;
        }

        private static void ReloadScene() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        [ContextMenu("Debug/Pretend 1 hour away")]
        private void DebugOneHourAway() => GrantOffline(3600);

        private void GrantOffline(double awaySeconds)
        {
            if (awaySeconds < game.Balance.offlineMinSeconds) return;

            // Limit and rate include upgrades, which are already restored at this point.
            double maxSeconds = game.OfflineMaxSeconds;
            double seconds = OfflineIncome.CountedSeconds(awaySeconds, maxSeconds);
            double coins = OfflineIncome.Calculate(game.Economy.IncomePerSec, seconds, game.OfflineRate, maxSeconds);
            if (coins <= 0) return;

            game.Economy.Earn(coins);
            PendingOfflineIncome = (coins, seconds);
            OfflineIncomeGranted?.Invoke(coins, seconds);
        }

        private static SaveData Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) return null;
                if (data.version > SaveData.CurrentVersion)
                {
                    // Made by a newer build; loading it here could lose fields, but refusing would wipe progress
                    // on the next autosave. Load what we understand.
                    Debug.LogWarning($"[CatMerge] Save version {data.version} is newer than {SaveData.CurrentVersion}.");
                }
                data.cats ??= new List<CatSave>();
                data.upgrades ??= new List<UpgradeSave>();
                data.purchases ??= Array.Empty<long>();
                data.codex ??= Array.Empty<int>();
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CatMerge] Save is corrupted, starting a new game. {e.Message}\n{json}");
                return null;
            }
        }
    }
}
