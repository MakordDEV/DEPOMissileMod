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

    public static GameObject MasterPrefab;
    public static GameObject NetworkMissilePrefab;
    public static GameObject ExplosionPrefab;
    public static string[] SkinNames;

    public static void InitializeNetworkPrefab(GameObject master)
    {
        MasterPrefab = master;
        NetworkMissilePrefab = UnityEngine.Object.Instantiate(MasterPrefab);
        UnityEngine.Object.DontDestroyOnLoad(NetworkMissilePrefab);
        NetworkMissilePrefab.SetActive(false);

        Component originalMissile = MasterPrefab.GetComponent("Missile");
        if (originalMissile != null)
        {
            try
            {
                FieldInfo skinsField = originalMissile.GetType().GetField("MissileSkins", BindingFlags.Public | BindingFlags.Instance);
                if (skinsField != null)
                {
                    GameObject[] skins = skinsField.GetValue(originalMissile) as GameObject[];
                    if (skins != null)
                    {
                        SkinNames = new string[skins.Length];
                        for (int i = 0; i < skins.Length; i++)
                        {
                            if (skins[i] != null)
                            {
                                SkinNames[i] = skins[i].name;
                            }
                        }
                    }
                }

                FieldInfo explosionField = originalMissile.GetType().GetField("ExplosionParticleSystem", BindingFlags.Public | BindingFlags.Instance);
                if (explosionField != null)
                {
                    ExplosionPrefab = explosionField.GetValue(originalMissile) as GameObject;
                }
            }
            catch (Exception ex)
            {
                MissileModPlugin.LogError("Failed to extract fields from Missile: " + ex.Message);
            }
        }

        Component netMissile = NetworkMissilePrefab.GetComponent("Missile");
        if (netMissile != null) UnityEngine.Object.DestroyImmediate(netMissile);

        Component netWarning = NetworkMissilePrefab.GetComponent("WarningMissile");
        if (netWarning != null) UnityEngine.Object.DestroyImmediate(netWarning);

        foreach (var audio in NetworkMissilePrefab.GetComponentsInChildren<AudioSource>(true))
        {
            UnityEngine.Object.DestroyImmediate(audio);
        }

        foreach (var cam in NetworkMissilePrefab.GetComponentsInChildren<Camera>(true))
        {
            UnityEngine.Object.DestroyImmediate(cam);
        }

        if (SkinNames != null)
        {
            foreach (string sName in SkinNames)
            {
                if (!string.IsNullOrEmpty(sName))
                {
                    Transform t = FindChildRecursive(NetworkMissilePrefab.transform, sName);
                    if (t != null)
                    {
                        t.gameObject.SetActive(false);
                    }
                }
            }
        }
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    public static void SpawnMissile(string name, string coords, string skin, string ip, Dictionary<string, Dictionary<string, object>> components = null)
    {
        if (NetworkMissilePrefab == null)
        {
            MissileModPlugin.LogWarning("NetworkMissilePrefab is not initialized yet. Waiting for MasterPrefab.");
            return;
        }

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

        string uniqueKey = $"{name}_{ip ?? "null"}";

        if (missiles.ContainsKey(uniqueKey))
        {
            UnityEngine.Object.Destroy(missiles[uniqueKey]);
            missiles.Remove(uniqueKey);
            MissileModPlugin.LogWarning($"Previous missile with key '{uniqueKey}' was replaced.");
        }

        GameObject missile = UnityEngine.Object.Instantiate(NetworkMissilePrefab, pos, rot);
        missile.name = $"missile_{uniqueKey}";

        if (SkinNames != null)
        {
            try
            {
                int targetIndex = -1;
                int currentIndex = 0;
                foreach (object skinEnumVal in Enum.GetValues(typeof(MissileSkin)))
                {
                    if (skinEnumVal.ToString().Equals(skin, StringComparison.OrdinalIgnoreCase))
                    {
                        targetIndex = currentIndex;
                        break;
                    }
                    currentIndex++;
                }

                if (targetIndex >= 0 && targetIndex < SkinNames.Length)
                {
                    string targetSkinName = SkinNames[targetIndex];
                    if (!string.IsNullOrEmpty(targetSkinName))
                    {
                        Transform targetSkin = FindChildRecursive(missile.transform, targetSkinName);
                        if (targetSkin != null)
                        {
                            targetSkin.gameObject.SetActive(true);
                        }
                    }
                }
                else
                {
                    MissileModPlugin.LogWarning($"Skin index out of bounds or not found for: {skin}");
                }
            }
            catch (Exception ex)
            {
                MissileModPlugin.LogWarning($"Failed to apply skin '{skin}': {ex.Message}");
            }
        }

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
    }

    public static void ExplodeMissile(string name, string ip)
    {
        string uniqueKey = $"{name}_{ip ?? "null"}";

        if (missiles.TryGetValue(uniqueKey, out var missile))
        {
            if (ExplosionPrefab != null)
            {
                UnityEngine.Object.Instantiate(ExplosionPrefab, missile.transform.position, missile.transform.rotation);
            }
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
}