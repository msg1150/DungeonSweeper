using System.Collections.Generic;
using UnityEngine;

/// <summary>한 탐험에서의 시체 상태. 표시 객체는 처리 후에도 저장 위치를 유지하도록 런이 소유한다.</summary>
public sealed class CorpseRunData
{
    public GameObject Visual { get; }
    public string Name { get; }
    public DismantleDifficulty Difficulty { get; }
    public IReadOnlyList<LootDefinition> Loot { get; }
    public bool IsProcessed { get; private set; }
    public int MonsterIndex { get; set; }
    public string PrefabId { get; set; }
    private DismantleSession session;
    public DismantleSession Session => session ??= new DismantleSession(Difficulty);
    public DismantleSaveData CaptureSession() => session?.Capture();
    public void RestoreSession(DismantleSaveData state) => session = state == null ? null : DismantleSession.Restore(Difficulty, state);

    public CorpseRunData(GameObject visual, string name, DismantleDifficulty difficulty, params LootDefinition[] loot)
    {
        Visual = visual;
        Name = name;
        Difficulty = difficulty;
        Loot = loot;
    }

    public void MarkProcessed()
    {
        IsProcessed = true;
        // 즉시 풀에 반환하면 이 런의 저장 위치가 다음 대여 위치로 바뀐다. 런 종료까지 소유한다.
        Visual.SetActive(false);
    }
}
