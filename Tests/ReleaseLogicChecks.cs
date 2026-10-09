using System;
using System.Reflection;
using UnityEngine;

/// <summary>Release regressions shared by the real Unity editor and Windows player.</summary>
public static class ReleaseLogicChecks
{
    private static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (ArgumentException) { return true; }
    }

    public static void Run(Action<bool, string> check)
    {
        float multiplier = DungeonTuning.Active.lootValueMultiplier;
        GameSettingsData options = GameSettings.Copy();
        try
        {
            DungeonTuning.Active.lootValueMultiplier = 1f;
            var bag = new GridInventory();
            check(!bag.TryPlace(null, 0, 0), "missing loot cannot mutate inventory");
            check(!bag.TryPlace(new LootDefinition("edge", 1, 1, 1), int.MaxValue, 0), "extreme grid coordinate rejected without overflow");
            check(bag.TryPlace(new LootDefinition("large reward", 1, 1, int.MaxValue), 0, 0)
                && bag.TryPlace(new LootDefinition("second reward", 1, 1, int.MaxValue), 1, 0)
                && bag.TotalValue == int.MaxValue, "inventory reward saturates without becoming negative");
            check(!bag.TryMove(bag.GetCell(0, 0), 1, 0) && bag.GetCell(0, 0) != 0 && bag.GetCell(1, 0) != 0,
                "failed item move restores original occupied cells");
            bag.Clear();
            DungeonTuning.Active.lootValueMultiplier = .5f;
            check(bag.TryPlace(new LootDefinition("rounding", 1, 1, 3), 0, 0) && bag.TotalValue == 2, "economy rounding remains consistent");
            DungeonTuning.Active.lootValueMultiplier = float.PositiveInfinity;
            bag.Clear();
            check(bag.TryPlace(new LootDefinition("fallback", 1, 1, 7), 0, 0) && bag.TotalValue == 7, "invalid multiplier cannot poison saved totals");
            check(Throws(() => new LootDefinition("invalid", int.MaxValue, 2, 1)), "invalid rectangle rejected before allocation");
            check(Throws(() => new LootDefinition("invalid", 1, 1, -1)), "negative loot price rejected");
            check(Throws(() => LootDefinition.CreateShaped("duplicate", 1, LootShape.Hide, Vector2Int.zero, Vector2Int.zero)), "duplicate shape cells rejected");
            var shape = LootDefinition.CreateShaped("rotation", 9, LootShape.Hide, Vector2Int.zero, Vector2Int.right, Vector2Int.up);
            LootDefinition rotated = shape;
            for (int i = 0; i < 4; i++) rotated = rotated.RotatedClockwise();
            bool sameCells = rotated.Width == shape.Width && rotated.Height == shape.Height;
            foreach (Vector2Int original in shape.OccupiedCells)
            {
                bool found = false; foreach (Vector2Int cell in rotated.OccupiedCells) if (cell == original) found = true;
                sameCells &= found;
            }
            check(sameCells && shape.Value == rotated.Value, "four rotations preserve shape and value");
            var drop = new MonsterLootEntry { dropChance = 0f };
            check(drop.Roll() == null, "zero chance never drops loot");
            drop.dropChance = 1f; drop.minPrice = 15; drop.maxPrice = 20;
            bool allValid = true;
            for (int i = 0; i < 128; i++) { var loot = drop.Roll(); allValid &= loot != null && loot.Value >= 15 && loot.Value <= 20; }
            check(allValid, "certain drops always produce prices in configured range");
            drop.minPrice = drop.maxPrice = int.MaxValue;
            check(drop.Roll().Value >= 0, "maximum configured price does not overflow random range");
            check(ReferenceEquals(CasualArtLibrary.WhiteSprite, CasualArtLibrary.WhiteSprite), "fallback graphics reused across dungeon visits");
            bool wideWindowsFit = true;
            for (int i = 0; i < 128; i++)
            {
                var wideWindow = new DismantleSession(new DismantleDifficulty(3, 3, 1f, .95f));
                wideWindowsFit &= wideWindow.WindowStart >= 0f && wideWindow.WindowStart + .95f <= 1f;
            }
            check(wideWindowsFit, "wide dismantle windows stay inside the bar across repeated random samples");
            var bounce = new DismantleSession(new DismantleDifficulty(3, 3, 1f, .2f));
            bounce.Tick(2.25f);
            check(Mathf.Abs(bounce.PointerPosition - .25f) < .0001f, "delayed dismantle tick preserves multiple boundary reflections");
            bounce.Tick(float.NaN);
            check(Mathf.Abs(bounce.PointerPosition - .25f) < .0001f, "invalid dismantle elapsed time cannot poison session");
            check(Throws(() => new DismantleDifficulty(0, 1, 1f, .2f)), "invalid dismantle difficulty is rejected");
            var previewObject = new GameObject("Rotation Preview Validation");
            try
            {
                var hud = previewObject.AddComponent<DungeonDemoHud>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var toggle = typeof(DungeonDemoHud).GetMethod("ToggleRotation", flags);
                var display = typeof(DungeonDemoHud).GetMethod("DisplayLoot", flags);
                toggle.Invoke(hud, new object[] { shape });
                var preview = (LootDefinition)display.Invoke(hud, new object[] { shape });
                check(ReferenceEquals(preview, display.Invoke(hud, new object[] { shape })) && !ReferenceEquals(preview, shape),
                    "rotated HUD previews reuse the same shape without per-repaint copies");
                toggle.Invoke(hud, new object[] { shape });
                check(ReferenceEquals(shape, display.Invoke(hud, new object[] { shape })), "toggling rotation off restores original shape");
            }
            finally { UnityEngine.Object.Destroy(previewObject); }

            var defaults = GameSettings.Defaults();
            var draft = GameSettings.Copy(); draft.masterVolume = float.NaN; draft.musicVolume = float.PositiveInfinity;
            draft.effectsVolume = -2f; draft.frameRate = 13;
            GameSettings.Apply(draft, false);
            check(GameSettings.Current.masterVolume == 1f && GameSettings.Current.musicVolume == .8f
                && GameSettings.Current.effectsVolume == 0f && GameSettings.Current.frameRate == 60,
                "invalid device settings sanitized before use");
            draft.masterVolume = .125f;
            check(GameSettings.Current.masterVolume == 1f && AudioListener.volume == 1f, "editing applied draft cannot change committed settings");
            check(GameSettings.Defaults().qualityLevel == defaults.qualityLevel && GameSettings.Defaults().width == defaults.width,
                "default button retains original defaults after applying options");
            check(Throws(() => GameSettings.Apply(null, false)), "missing options rejected explicitly");

            var actor = new GameObject("Movement State Validation");
            try
            {
                var motion = actor.AddComponent<PlayerMovement>();
                motion.Restore(new PlayerMotionSaveData { isDashing = true, dashSeconds = .1f, cooldownSeconds = .7f,
                    dashDirection = Vector2.right, lastDirection = Vector2.up });
                var state = motion.Capture();
                check(state.isDashing && state.dashSeconds == .1f && state.cooldownSeconds == .7f && state.dashDirection == Vector2.right,
                    "saved dash direction and timers restored");
                check(actor.GetComponent<Rigidbody2D>().collisionDetectionMode == CollisionDetectionMode2D.Continuous,
                    "player uses continuous wall collision during dash");
                motion.SetMovementEnabled(false);
                check(!motion.IsDashing && actor.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero,
                    "activity lock cancels dash and clears motion");
            }
            finally { UnityEngine.Object.Destroy(actor); }
        }
        finally { DungeonTuning.Active.lootValueMultiplier = multiplier; GameSettings.Apply(options, false); }
    }
}
