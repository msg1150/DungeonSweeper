using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>Copied only into the isolated validation project, including its release player.</summary>
public static class SaveProtectionChecks
{
    private static string PathFor(int slot) => Path.Combine(GameSaveService.SaveDirectory, $"slot-{slot + 1:D2}.sav");
    private static void ClearSlot(int slot)
    {
        foreach (string suffix in new[] { "", ".bak", ".tmp" })
        {
            string protectedPath = PathFor(slot) + suffix;
            string developmentPath = Path.ChangeExtension(PathFor(slot), ".json") + suffix;
            if (File.Exists(protectedPath)) File.Delete(protectedPath);
            if (File.Exists(developmentPath)) File.Delete(developmentPath);
        }
    }
    private static bool Rejected(byte[] file, int slot)
    {
        try { SaveFileProtection.Unprotect(file, slot); return false; }
        catch (Exception error) when (error is CryptographicException || error is InvalidDataException) { return true; }
    }
    private static byte[] Flip(byte[] original, int index)
    {
        byte[] copy = (byte[])original.Clone(); copy[index] ^= 0x80; return copy;
    }
    private static bool Same(byte[] a, byte[] b) => Convert.ToBase64String(a) == Convert.ToBase64String(b);

    public static void Run(Action<bool, string> check)
    {
        string normalized = Path.GetFullPath(GameSaveService.SaveDirectory).Replace('\\', '/');
        if (!normalized.EndsWith("/.utmp/UnityValidation/ValidationSaves", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Protection checks must never operate on user saves.");
        Directory.CreateDirectory(GameSaveService.SaveDirectory);
        for (int slot = 4; slot <= 8; slot++) ClearSlot(slot);
        var data = new GameSaveData { town = new TownProgressData { gold = 145, supplyKits = 2 }, playSeconds = 9.25 };
        string json = JsonUtility.ToJson(data);
        byte[] encrypted = SaveFileProtection.Protect(json, 4);
        check(SaveFileProtection.Unprotect(encrypted, 4) == json, "native OS protection roundtrip");
        check(!Same(encrypted, SaveFileProtection.Protect(json, 4)), "same plaintext encrypts differently each time");
        check(!Encoding.UTF8.GetString(encrypted).Contains("\"gold\""), "protected bytes do not expose save fields");
        check(Rejected(encrypted, 5) && Rejected(encrypted, -1), "slot and autosave context authenticated");
        check(Rejected(Flip(encrypted, 0), 4), "altered file magic rejected");
        check(Rejected(Flip(encrypted, 8), 4), "altered format version rejected");
        check(Rejected(Flip(encrypted, 9), 4), "altered payload length rejected");
        check(Rejected(Flip(encrypted, encrypted.Length / 2), 4), "altered encrypted payload rejected");
        check(Rejected(Flip(encrypted, encrypted.Length - 1), 4), "altered integrity tag rejected");
        var truncated = new byte[encrypted.Length - 1]; Array.Copy(encrypted, truncated, truncated.Length);
        check(Rejected(truncated, 4), "truncated protected file rejected");
        var appended = new byte[encrypted.Length + 1]; Array.Copy(encrypted, appended, encrypted.Length);
        check(Rejected(appended, 4), "appended protected file rejected");
        check(Rejected(Encoding.UTF8.GetBytes(json), 4), "plain JSON cannot masquerade as protected bytes");
        check(Rejected(new byte[SaveFileProtection.MaximumFileBytes + 1], 4), "oversized byte input rejected");
        bool tooLarge = false;
        try { SaveFileProtection.Protect(new string('x', SaveFileProtection.MaximumPlaintextBytes + 1), 4); }
        catch (InvalidDataException) { tooLarge = true; }
        check(tooLarge, "oversized plaintext rejected before encryption");

        check(GameSaveService.TrySave(4, data, out _), "service writes protected save");
        byte[] first = File.ReadAllBytes(PathFor(4));
        check(GameSaveService.TryLoad(4, out var loaded, out _) && loaded.town.gold == 145, "service restores protected values");
        File.WriteAllBytes(PathFor(5), first);
        check(!GameSaveService.TryLoad(5, out _, out _), "renaming a valid save into another slot rejected");
        File.WriteAllText(PathFor(5), json);
        File.WriteAllText(PathFor(5) + ".bak", json);
        check(!GameSaveService.TryLoad(5, out _, out _), "plain primary and backup both rejected");
        check(!GameSaveService.GetSlots()[6].CanLoad, "invalid protected slot disabled in load list");
        ClearSlot(5);
        using (var huge = new FileStream(PathFor(5), FileMode.Create, FileAccess.Write))
            huge.SetLength(SaveFileProtection.MaximumFileBytes + 1L);
        check(!GameSaveService.TryLoad(5, out _, out _), "oversized disk file rejected");

        data.town.gold = 290;
        check(GameSaveService.TrySave(4, data, out _), "protected atomic overwrite succeeds");
        check(Same(first, File.ReadAllBytes(PathFor(4) + ".bak")), "backup preserves original encrypted bytes");
        check(SaveFileProtection.Unprotect(File.ReadAllBytes(PathFor(4) + ".bak"), 4).Contains("\"gold\":145"), "backup is also encrypted and readable");
        File.WriteAllBytes(PathFor(4), Flip(File.ReadAllBytes(PathFor(4)), first.Length - 1));
        check(GameSaveService.TryLoad(4, out var recovered, out string notice) && recovered.town.gold == 145
            && !string.IsNullOrEmpty(notice), "authenticated backup recovers after tampering");
        data.town.gold = 435;
        check(GameSaveService.TrySave(4, data, out _), "save can replace a damaged primary");
        check(Same(first, File.ReadAllBytes(PathFor(4) + ".bak")), "damaged primary never replaces valid backup");
        using (var locked = File.Open(PathFor(4), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            data.town.gold = 580;
            check(!GameSaveService.TrySave(4, data, out _), "IO failure is reported without plaintext fallback");
            check(SaveFileProtection.Unprotect(File.ReadAllBytes(PathFor(4) + ".tmp"), 4).Contains("\"gold\":580"), "temporary write is encrypted even on failure");
        }
        check(GameSaveService.TryLoad(4, out var afterFailure, out _) && afterFailure.town.gold == 435, "failed write leaves current save intact");
        File.WriteAllBytes(PathFor(4), Flip(File.ReadAllBytes(PathFor(4)), File.ReadAllBytes(PathFor(4)).Length - 1));
        File.WriteAllBytes(PathFor(4) + ".bak", Flip(first, first.Length - 1));
        File.WriteAllText(Path.ChangeExtension(PathFor(4), ".json"), json);
        check(!GameSaveService.TryLoad(4, out _, out _), "two damaged protected files never fall back to plain JSON");

        ClearSlot(6);
        string legacyPath = Path.ChangeExtension(PathFor(6), ".json");
        var legacy = new GameSaveData { savedAtUtc = "2026-10-01T01:02:03.0000000Z", town = new TownProgressData { gold = 812 } };
        File.WriteAllText(legacyPath, JsonUtility.ToJson(legacy));
        legacy.town.gold = 406;
        File.WriteAllText(legacyPath + ".bak", JsonUtility.ToJson(legacy));
#if UNITY_EDITOR
        check(GameSaveService.TryLoad(6, out var imported, out _) && imported.town.gold == 812, "editor converts legacy primary");
        check(imported.savedAtUtc == legacy.savedAtUtc, "migration preserves original save timestamp");
        check(!File.Exists(legacyPath) && !File.Exists(legacyPath + ".bak"), "verified migration removes plaintext originals");
        check(SaveFileProtection.Unprotect(File.ReadAllBytes(PathFor(6) + ".bak"), 6).Contains("\"gold\":406"), "migration preserves legacy backup under protection");
        File.WriteAllBytes(PathFor(6), Flip(File.ReadAllBytes(PathFor(6)), 0));
        check(GameSaveService.TryLoad(6, out var legacyRecovered, out _) && legacyRecovered.town.gold == 406, "converted backup recovers independently");
        ClearSlot(7);
        string badLegacy = Path.ChangeExtension(PathFor(7), ".json");
        File.WriteAllText(badLegacy, "{broken");
        File.WriteAllText(badLegacy + ".bak", JsonUtility.ToJson(legacy));
        check(GameSaveService.TryLoad(7, out var fromBackup, out _) && fromBackup.town.gold == 406, "editor migrates valid backup of damaged legacy primary");
        check(!File.Exists(badLegacy) && !File.Exists(badLegacy + ".bak"), "backup migration leaves only encrypted saves");
        const string legacyKey = "DungeonSweeper.TownProgress.v1";
        string autoPath = Path.Combine(GameSaveService.SaveDirectory, "autosave.sav");
        byte[] previousAuto = File.Exists(autoPath) ? File.ReadAllBytes(autoPath) : null;
        byte[] previousAutoBackup = File.Exists(autoPath + ".bak") ? File.ReadAllBytes(autoPath + ".bak") : null;
        try
        {
            if (File.Exists(autoPath)) File.Delete(autoPath);
            if (File.Exists(autoPath + ".bak")) File.Delete(autoPath + ".bak");
            PlayerPrefs.SetString(legacyKey, JsonUtility.ToJson(legacy.town));
            GameSaveService.MigrateLegacySave();
            check(GameSaveService.TryLoad(-1, out var importedPrefs, out _) && importedPrefs.town.gold == 406,
                "editor converts old PlayerPrefs progress into protected autosave");
            check(!PlayerPrefs.HasKey(legacyKey), "successful conversion removes plaintext PlayerPrefs progress");
            legacy.town.gold = 999;
            PlayerPrefs.SetString(legacyKey, JsonUtility.ToJson(legacy.town));
            GameSaveService.MigrateLegacySave();
            check(GameSaveService.TryLoad(-1, out var unchanged, out _) && unchanged.town.gold == 406,
                "old PlayerPrefs cannot overwrite protected autosave");
            check(!PlayerPrefs.HasKey(legacyKey), "obsolete plaintext progress cleared when protected save exists");
        }
        finally
        {
            PlayerPrefs.DeleteKey(legacyKey); PlayerPrefs.Save();
            if (previousAuto != null) File.WriteAllBytes(autoPath, previousAuto);
            else if (File.Exists(autoPath)) File.Delete(autoPath);
            if (previousAutoBackup != null) File.WriteAllBytes(autoPath + ".bak", previousAutoBackup);
            else if (File.Exists(autoPath + ".bak")) File.Delete(autoPath + ".bak");
        }
#else
        check(!GameSaveService.TryLoad(6, out _, out _), "release player refuses legacy JSON imports");
        check(!File.Exists(PathFor(6)) && File.Exists(legacyPath), "release never upgrades attacker-supplied plain JSON");
        const string oldKey = "DungeonSweeper.TownProgress.v1";
        string auto = Path.Combine(GameSaveService.SaveDirectory, "autosave.sav");
        if (File.Exists(auto)) File.Delete(auto);
        if (File.Exists(auto + ".bak")) File.Delete(auto + ".bak");
        PlayerPrefs.SetString(oldKey, JsonUtility.ToJson(legacy.town));
        GameSaveService.MigrateLegacySave();
        check(!File.Exists(auto), "release ignores legacy PlayerPrefs progress");
        PlayerPrefs.DeleteKey(oldKey);
#endif
        var invalid = new GameSaveData { town = new TownProgressData { gold = -1 } };
        check(!GameSaveService.TrySave(8, invalid, out _), "negative progress rejected before writing");
        invalid.town.gold = 0; invalid.town.version = 99;
        check(!GameSaveService.TrySave(8, invalid, out _), "unsupported progress schema rejected");
        invalid.town.version = 1; invalid.town.lastContractBonus = 120;
        check(!GameSaveService.TrySave(8, invalid, out _), "inconsistent contract accounting rejected");
        for (int slot = 4; slot <= 8; slot++) ClearSlot(slot);
    }
}
