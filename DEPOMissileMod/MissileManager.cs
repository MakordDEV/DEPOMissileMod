using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// This part isnt ready yet. Maybe someone else will final this class, or i'll do it later.
/// </summary>
public static class MissileManager
{
    public static Dictionary<string, GameObject> missiles = new Dictionary<string, GameObject>();

    public static void SpawnMissile(string name, string coords, string skin, string ip, Dictionary<string, Dictionary<string, object>> components = null)
    {
        Vector3 pos;
        Quaternion rot;

        try
        {
            ParseCoords(coords, out pos, out rot);

            if (Mathf.Abs(pos.x) > 100000 || Mathf.Abs(pos.y) > 100000 || Mathf.Abs(pos.z) > 100000)
            {
                MissileModPlugin.LogWarning($"Rejected missile spawn due to large position: {pos}");
                return;
            }
        }
        catch (Exception e)
        {
            MissileModPlugin.LogError($"Failed to parse coords '{coords}' with error: {e.Message}");
            return;
        }

        GameObject prefab = GetMissilePrefabBySkin(skin);
        if (prefab == null)
        {
            MissileModPlugin.LogError($"Cannot spawn missile, prefab for skin '{skin}' is null.");
            return;
        }

        string uniqueKey = $"{name}_{ip ?? "null"}";

        if (missiles.ContainsKey(uniqueKey))
        {
            UnityEngine.Object.Destroy(missiles[uniqueKey]);
            missiles.Remove(uniqueKey);
            MissileModPlugin.LogWarning($"Previous missile with key '{uniqueKey}' was replaced.");
        }

        GameObject missile = UnityEngine.Object.Instantiate(prefab, pos, rot);
        missile.name = $"missile_{uniqueKey}";

        missile.SetActive(true);

        MissileModPlugin.LogInfo($"Missile '{uniqueKey}' spawned at {pos}");

        if (!missile.activeSelf || !missile.activeInHierarchy)
        {
            MissileModPlugin.LogWarning($"Missile '{missile.name}' is inactive (activeSelf: {missile.activeSelf}, activeInHierarchy: {missile.activeInHierarchy})");
        }

        if (components != null)
        {
            MissileModPlugin.LogInfo($"Applying {components.Count} components to missile '{missile.name}'");

            foreach (var compEntry in components)
            {
                string compTypeName = compEntry.Key;
                var fieldValues = compEntry.Value;

                Component targetComp = missile.GetComponent(compTypeName);
                if (targetComp == null)
                {
                    MissileModPlugin.LogWarning($"Component '{compTypeName}' not found on missile '{missile.name}'");
                    continue;
                }

                Type compType = targetComp.GetType();

                foreach (var fieldEntry in fieldValues)
                {
                    try
                    {
                        var field = compType.GetField(fieldEntry.Key, BindingFlags.Instance | BindingFlags.Public);
                        if (field != null)
                        {
                            object value = ConvertComponentValue(field.FieldType, fieldEntry.Value);
                            field.SetValue(targetComp, value);
                        }
                        else
                        {
                            MissileModPlugin.LogWarning($"Field '{fieldEntry.Key}' not found in component '{compTypeName}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        MissileModPlugin.LogWarning($"Failed to set {compTypeName}.{fieldEntry.Key} = {fieldEntry.Value}: {ex.Message}");
                    }
                }
            }
        }

        missiles[uniqueKey] = missile;
    }

    private static object ConvertComponentValue(Type targetType, object value)
    {
        if (value == null)
            return null;

        try
        {
            if (targetType == typeof(Vector3))
            {
                var dict = value as Newtonsoft.Json.Linq.JObject;
                if (dict != null)
                {
                    float x = dict.Value<float>("x");
                    float y = dict.Value<float>("y");
                    float z = dict.Value<float>("z");
                    return new Vector3(x, y, z);
                }
            }
            else if (targetType == typeof(Quaternion))
            {
                var dict = value as Newtonsoft.Json.Linq.JObject;
                if (dict != null)
                {
                    float x = dict.Value<float>("x");
                    float y = dict.Value<float>("y");
                    float z = dict.Value<float>("z");
                    float w = dict.Value<float>("w");
                    return new Quaternion(x, y, z, w);
                }
            }
            else if (targetType == typeof(Color))
            {
                var dict = value as Newtonsoft.Json.Linq.JObject;
                if (dict != null)
                {
                    float r = dict.Value<float>("r");
                    float g = dict.Value<float>("g");
                    float b = dict.Value<float>("b");
                    float a = dict.Value<float>("a");
                    return new Color(r, g, b, a);
                }
            }
            else
            {
                return Convert.ChangeType(value, targetType);
            }
        }
        catch
        {
            // ignored
        }

        return value;
    }


    public static bool IsExists(string key)
    {
        return missiles.ContainsKey(key);
    }

    public static void MoveMissile(string name, string coords, string ip)
    {
        string uniqueKey = $"{name}_{ip ?? "null"}";

        if (!missiles.TryGetValue(uniqueKey, out var missile))
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

        missile.transform.SetPositionAndRotation(pos, rot);
        MissileModPlugin.LogInfo($"Moved missile '{uniqueKey}' to position {pos}");
    }

    public static void ExplodeMissile(string name, string ip)
    {
        string uniqueKey = $"{name}_{ip ?? "null"}";

        if (missiles.TryGetValue(uniqueKey, out var missile))
        {
            UnityEngine.Object.Destroy(missile);
            missiles.Remove(uniqueKey);
            MissileModPlugin.LogInfo($"Missile '{uniqueKey}' destroyed");
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

        for (int i = 0; i < c.Length; i++)
        {
            c[i] = c[i].Replace(',', '.');
        }

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
        MissileModPlugin.LogInfo($"Requested skin: {name}");

        foreach (MissileSkin skin in Enum.GetValues(typeof(MissileSkin)))
        {
            if (skin.ToString().Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                string prefabName = $"Missile 3D TOUCH ME - {skin}";
                GameObject sceneObj = GameObject.Find(prefabName);

                if (sceneObj == null)
                {
                    MissileModPlugin.LogError($"Missile prefab '{prefabName}' not found in scene.");
                    return null;
                }

                GameObject copy = UnityEngine.Object.Instantiate(sceneObj);
                copy.name = $"MissileSkin_{skin}_PrefabCopy";

                copy.SetActive(false);

                return copy;
            }
        }

        MissileModPlugin.LogWarning($"Skin not found in enum: {name}");
        return null;
    }
}
