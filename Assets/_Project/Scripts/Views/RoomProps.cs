using System.Collections.Generic;
using CatMerge.Core;
using CatMerge.Data;
using CatMerge.Game;
using DG.Tweening;
using UnityEngine;

namespace CatMerge.Views
{
    /// <summary>
    /// Items in the room that show the bought upgrades: one per upgrade, hidden at level 0 and bigger with each
    /// stage (a third of the levels). Placeholders are tinted squares; put pictures into
    /// <see cref="UpgradeConfig.propSprites"/> to replace them. Sits under the cats, in the room's corners.
    /// </summary>
    public sealed class RoomProps : MonoBehaviour
    {
        private const int Stages = 3;
        private const int SortingOrder = -900; // above the floor (-1000), below every cat
        private const float PlaceholderBase = 0.5f;
        private const float PlaceholderStep = 0.25f;

        private sealed class Prop
        {
            public UpgradeDef Def;
            public UpgradeConfig Config;
            public SpriteRenderer Renderer;
            public int Stage;
        }

        private readonly List<Prop> _props = new List<Prop>();
        private GameController _game;
        private Room _room;
        private Sprite _square;

        public void Init(GameController game, Room room, Sprite square)
        {
            _game = game;
            _room = room;
            _square = square;
        }

        private void Start()
        {
            if (_game == null || _game.Upgrades == null) return;
            foreach (var def in _game.Upgrades.Defs)
            {
                if (Anchor(def.Effect) == null) continue;
                var go = new GameObject($"Prop {def.Id}");
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = SortingOrder;
                go.SetActive(false);
                _props.Add(new Prop
                {
                    Def = def,
                    Config = _game.UpgradeDatabase != null ? _game.UpgradeDatabase.Get(def.Id) : null,
                    Renderer = renderer,
                });
            }

            _game.Upgrades.Changed += Refresh;
            if (_room != null) _room.BoundsChanged += Place;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_game != null && _game.Upgrades != null) _game.Upgrades.Changed -= Refresh;
            if (_room != null) _room.BoundsChanged -= Place;
            foreach (var prop in _props)
            {
                if (prop.Renderer != null) prop.Renderer.transform.DOKill();
            }
        }

        private void Refresh()
        {
            foreach (var prop in _props)
            {
                int level = _game.Upgrades.GetLevel(prop.Def);
                int stage = StageOf(level, prop.Def.MaxLevel);
                if (stage == prop.Stage && prop.Renderer.gameObject.activeSelf == (stage > 0)) continue;

                bool grew = stage > prop.Stage;
                prop.Stage = stage;
                Show(prop, grew);
            }
            Place();
        }

        private void Show(Prop prop, bool pop)
        {
            var t = prop.Renderer.transform;
            t.DOKill();
            prop.Renderer.gameObject.SetActive(prop.Stage > 0);
            if (prop.Stage == 0) return;

            var custom = prop.Config != null ? prop.Config.propSprites : null;
            Sprite picture = custom != null && custom.Length > 0 ? custom[Mathf.Min(prop.Stage, custom.Length) - 1] : null;
            if (picture != null)
            {
                prop.Renderer.sprite = picture;
                prop.Renderer.color = Color.white;
                t.localScale = Vector3.one;
            }
            else
            {
                prop.Renderer.sprite = _square;
                prop.Renderer.color = prop.Config != null ? prop.Config.color : Color.white;
                t.localScale = Vector3.one * (PlaceholderBase + PlaceholderStep * prop.Stage);
            }

            if (!pop) return;
            var target = t.localScale;
            t.localScale = Vector3.zero;
            t.DOScale(target, 0.35f).SetEase(Ease.OutBack);
        }

        private void Place()
        {
            if (_room == null) return;
            var bounds = _room.Bounds;
            foreach (var prop in _props)
            {
                var anchor = Anchor(prop.Def.Effect) ?? new Vector2(0.5f, 0.5f);
                prop.Renderer.transform.position = new Vector3(
                    bounds.xMin + bounds.width * anchor.x, bounds.yMin + bounds.height * anchor.y, 0f);
            }
        }

        /// <summary>1..Stages for a bought level; 0 for none. Unlimited upgrades count as one big stage.</summary>
        private static int StageOf(int level, int maxLevel)
        {
            if (level <= 0) return 0;
            if (maxLevel <= 0) return 1;
            return Mathf.Clamp(Mathf.CeilToInt((float)level / maxLevel * Stages), 1, Stages);
        }

        /// <summary>Spot in the room (0..1 of its size), so it fits both the landscape and portrait shape.</summary>
        private static Vector2? Anchor(UpgradeEffect effect)
        {
            switch (effect)
            {
                case UpgradeEffect.RoomCapacity: return new Vector2(0.10f, 0.85f);
                case UpgradeEffect.IncomeMultiplier: return new Vector2(0.90f, 0.85f);
                case UpgradeEffect.OfflineHours: return new Vector2(0.10f, 0.15f);
                case UpgradeEffect.OfflineRate: return new Vector2(0.90f, 0.15f);
                default: return null; // BestKitten has nothing to show
            }
        }
    }
}
