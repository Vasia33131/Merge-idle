using System.Collections.Generic;
using CatMerge.Core;
using CatMerge.Data;
using CatMerge.Views;
using DG.Tweening;
using UnityEngine;

namespace CatMerge.Game
{
    /// <summary>
    /// Composition root: owns the <see cref="GameState"/>, <see cref="Core.Wallet"/> and <see cref="Core.Economy"/>
    /// and keeps cat views in sync with the state.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] private CatDatabase catDatabase;
        [SerializeField] private BalanceConfig balance;
        [SerializeField] private UpgradeDatabase upgradeDatabase;
        [SerializeField] private HomeDatabase homeDatabase;
        [SerializeField] private Room room;
        [SerializeField] private CatView catPrefab;
        [SerializeField] private Transform catsRoot;
        [Tooltip("Fraction of (r1 + r2) the drop point must be within to count as 'dropped on' a cat.")]
        [SerializeField, Range(0.3f, 1.2f)] private float dropOverlap = 0.8f;

        private readonly Dictionary<int, CatView> _views = new Dictionary<int, CatView>();
        /// <summary>Saved positions for cats being restored, consumed in CatAdded order.</summary>
        private readonly Queue<Vector2> _spawnPositions = new Queue<Vector2>();

        public GameState State { get; private set; }
        public Wallet Wallet { get; private set; }
        public Economy Economy { get; private set; }
        public Upgrades Upgrades { get; private set; }
        public Codex Codex { get; private set; }
        public Prestige Prestige { get; private set; }
        public CatDatabase CatDatabase => catDatabase;
        public UpgradeDatabase UpgradeDatabase => upgradeDatabase;
        public BalanceConfig Balance => balance;

        /// <summary>The home the cats live in now; null if there is no HomeDatabase.</summary>
        public HomeConfig CurrentHome => HomeAfterMoves(Prestige.Moves);

        /// <summary>The home a move would lead to.</summary>
        public HomeConfig NextHome => HomeAfterMoves(Prestige.Moves + 1);

        private HomeConfig HomeAfterMoves(int moves) => homeDatabase != null ? homeDatabase.ForMoves(moves) : null;

        /// <summary>Share of income earned offline, base from <see cref="BalanceConfig"/> plus upgrades.</summary>
        public double OfflineRate => Upgrades.OfflineRate(balance.offlineIncomeRate);

        /// <summary>Offline time limit in seconds, base from <see cref="BalanceConfig"/> plus upgrades.</summary>
        public double OfflineMaxSeconds => Upgrades.OfflineMaxSeconds(balance.OfflineMaxSeconds);

        private void Awake()
        {
            if (catDatabase == null || balance == null)
            {
                Debug.LogError("[CatMerge] GameController needs CatDatabase and BalanceConfig. " +
                               "Rebuild the scene via Tools/CatMerge/Build Game Scene.", this);
                enabled = false;
                return;
            }

            // Room for every UI animation + all coin popups, so DOTween doesn't grow its pools mid-game.
            DOTween.Init(recycleAllByDefault: false, useSafeMode: true, logBehaviour: LogBehaviour.ErrorsOnly).SetCapacity(200, 50);

            // Starts empty: SaveService fills it with either saved progress or StartNewGame().
            State = new GameState(balance.capacity, catDatabase.MaxLevel, balance.shopLevelOffset);
            Wallet = new Wallet();
            Economy = new Economy(State, Wallet, level => catDatabase.Get(level).incomePerSec, balance.KittenPrice);

            if (upgradeDatabase == null)
            {
                Debug.LogWarning("[CatMerge] GameController has no UpgradeDatabase, upgrades are off. " +
                                 "Rebuild the scene via Tools/CatMerge/Build Game Scene.", this);
            }
            var upgradeDefs = upgradeDatabase != null ? upgradeDatabase.ToDefs() : new List<UpgradeDef>();
            Upgrades = new Upgrades(upgradeDefs, State, Economy, balance.capacity, level => balance.KittenPrice(level, 0));
            Codex = new Codex(State);
            Prestige = new Prestige(State, Economy, balance.fishIncomeBonus, balance.fishBaseCoins, balance.fishExponent);
            if (homeDatabase == null)
            {
                Debug.LogWarning("[CatMerge] GameController has no HomeDatabase, the room keeps its look after moving. " +
                                 "Rebuild the scene via Tools/CatMerge/Build Game Scene.", this);
            }

            State.CatAdded += OnCatAdded;
            State.CatsMerged += OnCatsMerged;
        }

        /// <summary>Fresh start: start coins and a few free kittens.</summary>
        public void StartNewGame()
        {
            Wallet.Add(balance.startCoins);
            for (int i = 0; i < balance.startCats; i++)
            {
                if (!State.TryGrantCat(GameState.StartLevel, out _)) break;
            }
        }

        /// <summary>
        /// Loads saved progress into the (still empty) game; cats reappear where they were. A save written by a move
        /// (<see cref="SaveData.startNewRun"/>) only brings back fish and the Catpedia and starts a new run.
        /// </summary>
        public void Restore(SaveData data)
        {
            Prestige.Restore(data.fish, data.moves);
            if (data.startNewRun)
            {
                Codex.Restore(data.codex, GameState.StartLevel);
                StartNewGame();
                return;
            }
            // Before the cats come back, so restored cats don't celebrate as new discoveries.
            Codex.Restore(data.codex, data.maxLevelReached);
            Economy.RestoreRunEarned(data.runEarned);

            var levels = new List<int>(data.cats.Count);
            _spawnPositions.Clear();
            foreach (var cat in data.cats)
            {
                levels.Add(cat.level);
                _spawnPositions.Enqueue(room.Clamp(new Vector2(cat.x, cat.y), catPrefab.Radius));
            }

            Upgrades.Restore(data.upgrades); // first: bought room spots must exist before the cats come back
            State.Restore(levels, data.purchases, data.maxLevelReached);
            _spawnPositions.Clear(); // cats over capacity were dropped

            double coins = data.coins;
            if (double.IsNaN(coins) || double.IsInfinity(coins) || coins < 0) coins = 0;
            Wallet.Add(coins);
        }

        /// <summary>Writes the current progress into <paramref name="data"/> (time fields are left to the caller).</summary>
        public void WriteTo(SaveData data)
        {
            WriteKeptTo(data);
            data.startNewRun = false;
            data.coins = Wallet.Coins;
            data.runEarned = Economy.RunEarned;
            data.maxLevelReached = State.MaxLevelReached;

            if (data.purchases == null || data.purchases.Length != State.MaxLevel + 1) data.purchases = new long[State.MaxLevel + 1];
            for (int level = GameState.StartLevel; level <= State.MaxLevel; level++) data.purchases[level] = State.GetPurchaseCount(level);

            data.upgrades ??= new List<UpgradeSave>();
            Upgrades.WriteTo(data.upgrades);

            data.cats.Clear();
            foreach (var cat in State.Cats)
            {
                Vector2 position = _views.TryGetValue(cat.Id, out var view) ? view.Position : Vector2.zero;
                data.cats.Add(new CatSave { level = cat.Level, x = position.x, y = position.y });
            }
        }

        /// <summary>Writes only what survives a move to a new home: fish, moves and the Catpedia.</summary>
        public void WriteKeptTo(SaveData data)
        {
            data.version = SaveData.CurrentVersion;
            data.fish = Prestige.Fish;
            data.moves = Prestige.Moves;
            data.codex = Codex.ToArray();
        }

        private void OnDestroy()
        {
            if (State == null) return;
            State.CatAdded -= OnCatAdded;
            State.CatsMerged -= OnCatsMerged;
        }

        /// <summary>Buys a kitten of any level the shop sells (see <see cref="GameState.MaxBuyableLevel"/>) for coins.</summary>
        public bool TryBuyCat(int level) => Economy.TryBuy(level, out _);

        public bool TryGetView(int catId, out CatView view) => _views.TryGetValue(catId, out view);

        public IReadOnlyCollection<CatView> Views => _views.Values;

        /// <summary>Topmost cat under the given world point, or null.</summary>
        public CatView FindCatAt(Vector2 worldPoint)
        {
            CatView best = null;
            foreach (var view in _views.Values)
            {
                if (!view.Contains(worldPoint)) continue;
                // Lower Y is drawn on top, so prefer it.
                if (best == null || view.Position.y < best.Position.y) best = view;
            }
            return best;
        }

        /// <summary>Called by the drag controller when a dragged cat is released.</summary>
        public void Drop(CatView dragged)
        {
            CatView mergeTarget = null;
            CatView blocker = null;
            float mergeDist = float.MaxValue;
            float blockDist = float.MaxValue;

            foreach (var other in _views.Values)
            {
                if (other == dragged) continue;
                float dist = Vector2.Distance(dragged.Position, other.Position);
                if (dist > (dragged.Radius + other.Radius) * dropOverlap) continue;

                if (State.CanMerge(dragged.CatId, other.CatId) == MergeResult.Success)
                {
                    if (dist < mergeDist) { mergeDist = dist; mergeTarget = other; }
                }
                else if (dist < blockDist)
                {
                    blockDist = dist; blocker = other;
                }
            }

            if (mergeTarget != null && State.TryMerge(dragged.CatId, mergeTarget.CatId, out _) == MergeResult.Success)
            {
                return; // views are swapped in OnCatsMerged
            }

            dragged.Bounce(GetBounceLanding(dragged, blocker));
        }

        private static Vector2 GetBounceLanding(CatView dragged, CatView blocker)
        {
            if (blocker == null) return dragged.Position;

            Vector2 away = dragged.Position - blocker.Position;
            if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitCircle;
            return blocker.Position + away.normalized * (dragged.Radius + blocker.Radius) * 1.1f;
        }

        private void OnCatAdded(CatData cat)
        {
            Vector2 position = _spawnPositions.Count > 0 ? _spawnPositions.Dequeue() : room.GetRandomPoint(catPrefab.Radius);
            var view = Spawn(cat, position);
            view.PlaySpawn();
        }

        private void OnCatsMerged(CatData dragged, CatData target, CatData result)
        {
            Vector2 position = _views[target.Id].Position;
            Despawn(dragged.Id);
            Despawn(target.Id);
            var view = Spawn(result, position);
            view.PlaySpawn();
        }

        private CatView Spawn(CatData cat, Vector2 position)
        {
            var view = Instantiate(catPrefab, position, Quaternion.identity, catsRoot);
            view.Init(cat, catDatabase.Get(cat.Level), room);
            _views.Add(cat.Id, view);
            return view;
        }

        private void Despawn(int catId)
        {
            if (!_views.TryGetValue(catId, out var view)) return;
            _views.Remove(catId);
            Destroy(view.gameObject);
        }
    }
}
