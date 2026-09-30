using System;
using System.Globalization;
using System.Text;
using CatMerge.Core;
using CatMerge.Data;
using UnityEditor;
using UnityEngine;

namespace CatMerge.Editor
{
    /// <summary>
    /// Prints the economy curve to the console: per level income, its worth in level-1 kittens, shop prices,
    /// and when a greedy player reaches it (<see cref="EconomySimulator"/>).
    /// </summary>
    public static class BalanceTableReport
    {
        private static readonly string[] Headers =
            { "Ур", "Доход/с", "= котят 1ур", "Цена в магазине", "Открывает покупку", "Достигнут", "Шаг", "Покупок" };

        [MenuItem("Tools/CatMerge/Print Balance Table")]
        public static void PrintFromMenu()
        {
            var database = AssetDatabase.LoadAssetAtPath<CatDatabase>(CatDatabaseGenerator.DatabasePath);
            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfig>(CatDatabaseGenerator.BalancePath);
            if (database == null || balance == null)
            {
                Debug.LogError("[CatMerge] CatDatabase/BalanceConfig not found. Run Tools/CatMerge/Generate Cat Configs first.");
                return;
            }
            Print(database, balance);
        }

        [MenuItem("CONTEXT/BalanceConfig/Print Balance Table")]
        private static void PrintFromBalanceContext(MenuCommand command)
        {
            var database = AssetDatabase.LoadAssetAtPath<CatDatabase>(CatDatabaseGenerator.DatabasePath);
            if (database == null) { PrintFromMenu(); return; }
            Print(database, (BalanceConfig)command.context);
        }

        public static void Print(CatDatabase database, BalanceConfig balance)
        {
            int maxLevel = database.MaxLevel;
            var sim = EconomySimulator.Run(balance.ToEconomyParams(maxLevel));

            var rows = new string[maxLevel + 1][];
            rows[0] = Headers;
            bool hasManualIncome = false;
            var inv = CultureInfo.InvariantCulture;

            for (int level = 1; level <= maxLevel; level++)
            {
                var cat = database.Get(level);
                double income = cat != null ? cat.incomePerSec : 0;
                double formula = balance.IncomeForLevel(level);
                bool manual = Math.Abs(income - formula) > formula * 1e-6;
                hasManualIncome |= manual;

                double reached = sim.ReachedAt[level];
                double step = level > 1 ? reached - sim.ReachedAt[level - 1] : double.NaN;

                rows[level] = new[]
                {
                    level.ToString(inv),
                    NumberFormatter.Format(income) + (manual ? "*" : ""),
                    BalanceMath.KittensForLevel(level).ToString(inv),
                    NumberFormatter.Format(balance.KittenPrice(level, 0)),
                    "до ур. " + ShopRules.MaxBuyableLevel(level, balance.shopLevelOffset).ToString(inv),
                    FormatDuration(reached),
                    FormatDuration(step),
                    double.IsNaN(reached) ? "—" : sim.PurchasesAt[level].ToString(inv),
                };
            }

            var sb = new StringBuilder();
            sb.AppendLine(string.Format(inv,
                "[CatMerge] Баланс: {0} ур. Котёнок ур.N стоит {1} × {2}^(N-1), ×{3} за каждую покупку этого уровня. " +
                "Магазин продаёт до (макс. уровень − {4}). Доход {5}/с × {6} за уровень. Старт: {7} монет + {8} котят, мест {9}.",
                maxLevel, balance.baseKittenPrice, balance.levelPriceMultiplier, balance.priceGrowth, balance.shopLevelOffset,
                balance.baseIncomePerSec, balance.incomeMultiplier, balance.startCoins, balance.startCats, balance.capacity));
            AppendTable(sb, rows);
            sb.AppendLine();
            sb.AppendLine("= котят 1ур — сколько котят 1 ур. сливается в одного кота этого уровня (2^(ур-1)).");
            sb.AppendLine("Цена в магазине — первый котёнок этого уровня (потом дорожает с каждой покупкой).");
            sb.AppendLine("Открывает покупку — какой максимальный уровень продаёт магазин, когда достигнут этот.");
            sb.AppendLine("Достигнут / Шаг / Покупок — симуляция жадного игрока: покупает лучшего доступного котёнка, " +
                          "сразу сливает, без тапов, офлайна и престижа. Время на клики не учитывается.");
            if (sim.StopReason != null) sb.AppendLine("Симуляция остановлена: " + sim.StopReason + ".");
            if (hasManualIncome) sb.AppendLine("* доход в ассете отличается от формулы BalanceConfig.");

            string text = sb.ToString();
            EditorGUIUtility.systemCopyBuffer = text;
            Debug.Log(text + "(Таблица скопирована в буфер обмена: в консоли шрифт не моноширинный.)", balance);
        }

        private static void AppendTable(StringBuilder sb, string[][] rows)
        {
            var widths = new int[Headers.Length];
            foreach (var row in rows)
                for (int c = 0; c < row.Length; c++)
                    widths[c] = Math.Max(widths[c], row[c].Length);

            int total = 3 * (widths.Length - 1);
            foreach (int w in widths) total += w;

            for (int r = 0; r < rows.Length; r++)
            {
                for (int c = 0; c < rows[r].Length; c++)
                {
                    if (c > 0) sb.Append(" | ");
                    sb.Append(c == 0 ? rows[r][c].PadRight(widths[c]) : rows[r][c].PadLeft(widths[c]));
                }
                sb.AppendLine();
                if (r == 0) sb.AppendLine(new string('-', total));
            }
        }

        private static string FormatDuration(double s)
        {
            if (double.IsNaN(s)) return "—";
            var inv = CultureInfo.InvariantCulture;
            if (s < 60) return s.ToString("0", inv) + "с";
            if (s < 3600) return (s / 60).ToString("0.#", inv) + "м";
            if (s < 86400) return (s / 3600).ToString("0.#", inv) + "ч";
            return (s / 86400).ToString("0.#", inv) + "д";
        }
    }
}
