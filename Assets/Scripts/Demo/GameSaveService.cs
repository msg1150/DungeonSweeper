using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public sealed class SaveSlotInfo
{
    public int Index;
    public bool Exists, CanLoad;
    public GameSaveData Data;
    public string Error;
    public string Name => Index == -1 ? "자동 저장" : $"저장 슬롯 {Index + 1}";
}

/// <summary>One rotating autosave and configurable manual slots, with atomic replacement and backups.</summary>
public static class GameSaveService
{
    public const int AutosaveSlot = -1;
    public static int ManualSlotCount => Mathf.Max(1, GameFlowConfig.Active.manualSaveSlotCount);
    public static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");

    private static bool ValidSlot(int slot) => slot == AutosaveSlot || (slot >= 0 && slot < ManualSlotCount);
    private static string SlotPath(int slot) => Path.Combine(SaveDirectory,
        slot == AutosaveSlot ? "autosave.sav" : $"slot-{slot + 1:D2}.sav");

    public static bool TrySave(int slot, GameSaveData data, out string error)
    {
        error = null;
        if (!ValidSlot(slot) || data == null || !data.IsValid())
        {
            error = "저장할 데이터 또는 슬롯이 올바르지 않습니다.";
            return false;
        }
        try
        {
            Directory.CreateDirectory(SaveDirectory);
            string path = SlotPath(slot);
            string temporary = path + ".tmp";
            data.savedAtUtc = DateTime.UtcNow.ToString("O");
            File.WriteAllBytes(temporary, SaveFileProtection.Protect(JsonUtility.ToJson(data), slot));
            // A damaged primary must not replace an already valid recovery backup.
            if (File.Exists(path)) File.Replace(temporary, path, TryRead(path, slot, out _, out _) ? path + ".bak" : null);
            else File.Move(temporary, path);
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
            || exception is ArgumentException || exception is NotSupportedException || exception is InvalidDataException
            || exception is CryptographicException || exception is DllNotFoundException || exception is EntryPointNotFoundException)
        {
            Debug.LogError($"Save failed: {exception.Message}");
            error = "암호화 저장에 실패했습니다. 저장 폴더의 권한·디스크 공간과 Windows 실행 환경을 확인해 주세요.";
            return false;
        }
    }

    public static bool TryLoad(int slot, out GameSaveData data, out string error)
    {
        data = null;
        error = null;
        if (!ValidSlot(slot)) { error = "사용할 수 없는 저장 슬롯입니다."; return false; }
        string path = SlotPath(slot);
#if UNITY_EDITOR
        // Only migrate development saves when no protected save exists. Players never accept JSON.
        if (!File.Exists(path) && !File.Exists(path + ".bak")) TryImportDevelopmentSave(slot);
#endif
        if (TryRead(path, slot, out data, out error)) return true;
        if (TryRead(path + ".bak", slot, out data, out string backupError))
        {
            error = "이전 정상 저장본으로 복구했습니다.";
            return true;
        }
        if (!File.Exists(path)) error = backupError ?? "비어 있는 슬롯입니다.";
        return false;
    }

    private static bool TryRead(string path, int slot, out GameSaveData data, out string error)
    {
        data = null;
        error = null;
        if (!File.Exists(path)) return false;
        try
        {
            // Bound allocation before reading untrusted file contents.
            if (new FileInfo(path).Length > SaveFileProtection.MaximumFileBytes)
                throw new InvalidDataException("Protected save exceeds the supported size.");
            string json = SaveFileProtection.Unprotect(File.ReadAllBytes(path), slot);
            GameSaveData read = JsonUtility.FromJson<GameSaveData>(json);
            if (read == null || !read.IsValid()) { error = "저장 데이터가 손상되었거나 지원하지 않는 형식입니다."; return false; }
            data = read;
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
            || exception is ArgumentException || exception is CryptographicException
            || exception is NotSupportedException || exception is InvalidDataException || exception is DecoderFallbackException
            || exception is DllNotFoundException || exception is EntryPointNotFoundException)
        {
            error = "저장 데이터가 변조·손상되었거나 현재 PC/Windows 계정에서 해독할 수 없습니다.";
            return false;
        }
    }

    public static List<SaveSlotInfo> GetSlots()
    {
        List<SaveSlotInfo> result = new();
        for (int slot = AutosaveSlot; slot < ManualSlotCount; slot++)
        {
            bool loaded = TryLoad(slot, out GameSaveData data, out string error);
            string path = SlotPath(slot);
            result.Add(new SaveSlotInfo { Index = slot, Exists = File.Exists(path) || File.Exists(path + ".bak"),
                CanLoad = loaded, Data = data, Error = error });
        }
        return result;
    }

    public static void MigrateLegacySave()
    {
#if UNITY_EDITOR
        const string legacyKey = "DungeonSweeper.TownProgress.v1";
        if (!PlayerPrefs.HasKey(legacyKey)) return;
        // Previous development versions left this readable key after importing JSON.
        if (TryRead(SlotPath(AutosaveSlot), AutosaveSlot, out _, out _)
            || TryRead(SlotPath(AutosaveSlot) + ".bak", AutosaveSlot, out _, out _))
        {
            ClearLegacyProgress();
            return;
        }
        if (File.Exists(SlotPath(AutosaveSlot)) || File.Exists(SlotPath(AutosaveSlot) + ".bak")
            || File.Exists(DevelopmentSlotPath(AutosaveSlot)) || File.Exists(DevelopmentSlotPath(AutosaveSlot) + ".bak")) return;
        try
        {
            TownProgressData legacy = JsonUtility.FromJson<TownProgressData>(PlayerPrefs.GetString(legacyKey));
            if (legacy == null || legacy.version != 1) return;
            if (TrySave(AutosaveSlot, new GameSaveData { town = legacy, area = SaveArea.Town }, out _))
                ClearLegacyProgress();
        }
        catch (ArgumentException) { Debug.LogWarning("The previous town save could not be migrated."); }
#endif
    }

#if UNITY_EDITOR
    private static void ClearLegacyProgress()
    {
        PlayerPrefs.DeleteKey("DungeonSweeper.TownProgress.v1");
        PlayerPrefs.Save();
    }

    private static string DevelopmentSlotPath(int slot) => Path.ChangeExtension(SlotPath(slot), ".json");

    private static GameSaveData ReadDevelopmentSave(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            if (new FileInfo(path).Length > SaveFileProtection.MaximumPlaintextBytes) return null;
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
            return data != null && data.IsValid() ? data : null;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
            || exception is ArgumentException) { return null; }
    }

    private static void TryImportDevelopmentSave(int slot)
    {
        string legacy = DevelopmentSlotPath(slot);
        GameSaveData primary = ReadDevelopmentSave(legacy);
        GameSaveData backup = ReadDevelopmentSave(legacy + ".bak");
        GameSaveData chosen = primary ?? backup;
        if (chosen == null) return;
        // Preserve the original timestamp and the old backup's contents during conversion.
        try
        {
            string destination = SlotPath(slot);
            WriteImportedSave(destination, chosen, slot);
            if (backup != null) WriteImportedSave(destination + ".bak", backup, slot);
            if (!TryRead(destination, slot, out _, out _)) return;
            // Only remove the readable originals after their protected replacement was verified.
            if (File.Exists(legacy)) File.Delete(legacy);
            if (File.Exists(legacy + ".bak")) File.Delete(legacy + ".bak");
            if (slot == AutosaveSlot) ClearLegacyProgress();
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
            || exception is ArgumentException || exception is CryptographicException || exception is NotSupportedException || exception is InvalidDataException
            || exception is DllNotFoundException || exception is EntryPointNotFoundException)
        {
            Debug.LogWarning("The development save could not be fully migrated to the protected format.");
        }
    }

    private static void WriteImportedSave(string path, GameSaveData data, int slot)
    {
        File.WriteAllBytes(path + ".tmp", SaveFileProtection.Protect(JsonUtility.ToJson(data), slot));
        File.Move(path + ".tmp", path);
    }
#endif
}
