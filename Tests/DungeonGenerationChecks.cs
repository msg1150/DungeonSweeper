using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class DungeonGenerationChecks
{
    public static void Run(Action<bool,string> check)
    {
        var sizes = new[] { new Vector2Int(4,4),new Vector2Int(4,64),new Vector2Int(64,4),new Vector2Int(20,12),new Vector2Int(32,20),new Vector2Int(64,64) };
        foreach (var size in sizes)
        {
            bool valid = true, dimensions = true, stairs = true, enclosed = true;
            for (int seed = 0; seed < 20; seed++)
            {
                var settings = new DungeonGenerationSettings { width = size.x,height = size.y,floorCount = 3,seed = seed };
                var maps = DungeonMapGenerator.Generate(settings);
                for (int floor = 0; floor < maps.Length; floor++)
                {
                    var map = maps[floor]; valid &= map.IsValid(out _);
                    dimensions &= map.width == size.x && map.height == size.y && map.tiles.Length == size.x*size.y;
                    stairs &= map.hasStairsUp == (floor > 0) && map.hasStairsDown == (floor < maps.Length-1);
                    for (int x = 0; x < map.width; x++) enclosed &= map.Tile(x,0) != DungeonTile.Floor && map.Tile(x,map.height-1) != DungeonTile.Floor;
                    for (int y = 0; y < map.height; y++) enclosed &= map.Tile(0,y) != DungeonTile.Floor && map.Tile(map.width-1,y) != DungeonTile.Floor;
                }
            }
            check(valid && dimensions && stairs && enclosed,$"generated {size.x}x{size.y} maps retain exact dimensions, connected floors, reachable stairs and closed boundaries across 20 seeds");
        }
        var mixed = new DungeonGenerationSettings { floorCount = 3,individualSizes = true,seed = -123,
            floorSizes = new List<Vector2Int> { new(24,16),new(16,12),new(32,20) } };
        var first = DungeonMapGenerator.Generate(mixed); var duplicate = DungeonMapGenerator.Generate(mixed);
        check(first.Select((map,index) => map.width == mixed.floorSizes[index].x && map.height == mixed.floorSizes[index].y).All(value => value),
            "each generated floor accepts its own grid dimensions");
        check(first.Select((map,index) => map.tiles.SequenceEqual(duplicate[index].tiles) && map.entrance == duplicate[index].entrance
            && map.stairsUp == duplicate[index].stairsUp && map.stairsDown == duplicate[index].stairsDown).All(value => value), "same generation settings and seed reproduce the reviewed map and markers");
        mixed.seed++;
        var retry = DungeonMapGenerator.Generate(mixed);
        check(first.Select((map,index) => !map.tiles.SequenceEqual(retry[index].tiles)).Any(value => value), "a new seed creates a different candidate");
        var unityState = UnityEngine.Random.state;
        int expected = UnityEngine.Random.Range(0,int.MaxValue); UnityEngine.Random.state = unityState;
        DungeonMapGenerator.Generate(mixed);
        check(UnityEngine.Random.Range(0,int.MaxValue) == expected,"preview generation leaves gameplay random state unchanged");
        UnityEngine.Random.state = unityState;
        mixed.floorSizes[1] = new Vector2Int(65,12);
        bool rejected = false;
        try { DungeonMapGenerator.Generate(mixed); } catch (ArgumentException) { rejected = true; }
        check(rejected,"generator rejects unsupported dimensions instead of silently clipping a request");
        var maximum = DungeonMapGenerator.Generate(new DungeonGenerationSettings { floorCount = 16,width = 4,height = 4 });
        check(maximum.Length == 16 && maximum.All(map => map.IsValid(out _)),"maximum floor count works even for minimal floor grids");
    }
}
