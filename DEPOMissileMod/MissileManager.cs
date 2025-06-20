using System;
using System.Collections.Generic;
using UnityEngine;

public static class MissileManager
{
    public static Dictionary<string, GameObject> missiles = new Dictionary<string, GameObject>();

    public static void SpawnMissile(string name, string coords, string skin, string ip)
    {
        if (name == MissileModPlugin.steam_id) return;

        Vector3 pos;
        Quaternion rot;

        try
        {
            ParseCoords(coords, out pos, out rot);
        }
        catch (Exception e)
        {
            MissileModPlugin.LogError($"Failed to parse coords '{coords}' with error: {e.Message}");
            return;
        }

        GameObject prefab = GetMissilePrefabBySkin(skin);
        if (prefab == null) return;

        string uniqueKey = $"{name}_{ip ?? "null"}";

        GameObject missile = UnityEngine.Object.Instantiate(prefab, pos, rot);
        missile.name = $"missile_{uniqueKey}";

        missiles[uniqueKey] = missile;
    }

    public static bool IsExists(string key)
    {
        return missiles.ContainsKey(key);
    }

    public static void MoveMissile(string name, string coords, string ip)
    {
        string uniqueKey = $"{name}_{ip ?? "null"}";

        if (!missiles.ContainsKey(uniqueKey))
        {
            MissileModPlugin.LogWarning($"MoveMissile: missile with key '{uniqueKey}' does not exist");
            return;
        }

        Vector3 pos;
        Quaternion rot;

        try
        {
            ParseCoords(coords, out pos, out rot);
        }
        catch (Exception e)
        {
            MissileModPlugin.LogError($"Failed to parse coords '{coords}' with error: {e.Message}");
            return;
        }

        missiles[uniqueKey].transform.SetPositionAndRotation(pos, rot);
        MissileModPlugin.LogInfo($"Moved missile '{uniqueKey}' to position {pos}");
    }

    public static void ExplodeMissile(string name, string ip)
    {
        string uniqueKey = $"{name}_{ip ?? "null"}";

        if (missiles.ContainsKey(uniqueKey))
        {
            UnityEngine.Object.Destroy(missiles[uniqueKey]);
            missiles.Remove(uniqueKey);
        }
        else
        {
            MissileModPlugin.LogWarning($"ExplodeMissile: missile with key '{uniqueKey}' does not exist");
        }
    }

    private static void ParseCoords(string coordStr, out Vector3 pos, out Quaternion rot)
    {
        if (string.IsNullOrEmpty(coordStr))
            throw new ArgumentException("coordStr is null or empty");

        string[] c = coordStr.Split(new string[] { ",," }, StringSplitOptions.None);

        if (c.Length != 7)
            throw new FormatException($"Invalid coord string format: expected 7 parts but got {c.Length}");

        pos = new Vector3(
            float.Parse(c[0], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(c[1], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(c[2], System.Globalization.CultureInfo.InvariantCulture)
        );

        rot = new Quaternion(
            float.Parse(c[3], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(c[4], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(c[5], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(c[6], System.Globalization.CultureInfo.InvariantCulture)
        );
    }

    public static GameObject GetMissilePrefabBySkin(string name)
    {
        foreach (MissileSkin skin in Enum.GetValues(typeof(MissileSkin)))
        {
            MissileModPlugin.LogInfo($"Available skin: {skin}");
            if (skin.ToString().Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return Missile.Instance.MissileSkins[(int)skin];
            }
        }
        MissileModPlugin.LogWarning($"Skin not found: {name}");
        return null;
    }
}
