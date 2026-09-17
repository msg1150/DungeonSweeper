using System.Collections.Generic;
using UnityEngine;

public sealed class LootDefinition
{
    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public int Value { get; }

    public LootDefinition(string name, int width, int height, int value)
    {
        Name = name;
        Width = width;
        Height = height;
        Value = value;
    }
}

public sealed class DismantleDifficulty
{
    public int RequiredSuccesses { get; }
    public int MaxFailures { get; }
    public float PointerSpeed { get; }
    public float SuccessWindowSize { get; }

    public DismantleDifficulty(int requiredSuccesses, int maxFailures, float pointerSpeed, float successWindowSize)
    {
        RequiredSuccesses = requiredSuccesses;
        MaxFailures = maxFailures;
        PointerSpeed = pointerSpeed;
        SuccessWindowSize = successWindowSize;
    }
}

public sealed class CorpseRunData
{
    public GameObject Visual { get; }
    public string Name { get; }
    public DismantleDifficulty Difficulty { get; }
    public IReadOnlyList<LootDefinition> Loot { get; }
    public bool IsProcessed { get; private set; }

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
        Visual.SetActive(false);
    }
}

public sealed class DismantleSession
{
    public DismantleDifficulty Difficulty { get; }
    public int Successes { get; private set; }
    public int Failures { get; private set; }
    public float PointerPosition { get; private set; }
    public float WindowStart { get; }
    public bool IsInSuccessWindow => PointerPosition >= WindowStart && PointerPosition <= WindowStart + Difficulty.SuccessWindowSize;
    public bool IsComplete => Successes >= Difficulty.RequiredSuccesses;
    public bool IsDestroyed => Failures >= Difficulty.MaxFailures;

    private float direction = 1f;

    public DismantleSession(DismantleDifficulty difficulty)
    {
        Difficulty = difficulty;
        WindowStart = Random.Range(.16f, .68f);
    }

    public void Tick(float deltaTime)
    {
        PointerPosition += direction * Difficulty.PointerSpeed * deltaTime;
        if (PointerPosition >= 1f || PointerPosition <= 0f)
        {
            PointerPosition = Mathf.Clamp01(PointerPosition);
            direction *= -1f;
        }
    }

    public void RegisterAttempt()
    {
        if (IsInSuccessWindow) Successes++;
        else Failures++;
    }
}
