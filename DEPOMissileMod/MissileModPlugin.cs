using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

[BepInPlugin("ru.makorddev.depomissilemod", "DEPO missile mod", "1.0")]
public class MissileModPlugin : BaseUnityPlugin
{
    private UdpClient udpClient;
    private IPEndPoint remoteEP;

    public static UdpClient StaticUdpClient;
    public static IPEndPoint StaticRemoteEP;

    public static MissileModPlugin Instance;

    public static string steam_id;

    private ConcurrentQueue<string> receivedMessages = new ConcurrentQueue<string>();

    public void Awake()
    {
        udpClient = new UdpClient(0);
        remoteEP = new IPEndPoint(Dns.GetHostAddresses("busiatep.ru")[0], 9999);

        StaticUdpClient = udpClient;
        StaticRemoteEP = remoteEP;

        Instance = this;
        var harmony = new Harmony("ru.makorddev.depomissilemod.harmony");
        harmony.PatchAll();

        StartCoroutine(SendHeartbeat());

        Logger.LogInfo($"DEPO missile mod initialized.");
    }

    public void Start()
    {
        StartCoroutine(SayHello());
        RunSendGetCordLoop();
    }

    private void OnApplicationQuit()
    {
        SendUdpMessage("bye");
    }

    private void OnDestroy()
    {
        udpClient?.Close();
    }
    IEnumerator SayHello()
    {
        bool ready = false;

        while (!ready)
        {
            try
            {
                steam_id = SteamUser.GetSteamID().m_SteamID.ToString();
                SendUdpMessage("hello;" + steam_id);
                Logger.LogInfo("SteamID: " + steam_id);
                ready = true;
            }
            catch (Exception)
            {
                // steam isnt ready 
            }

            if (!ready) yield return new WaitForSeconds(5f);
        }
    }

    IEnumerator SendHeartbeat()
    {
        while (true)
        {
            SendUdpMessage("imalive");
            yield return new WaitForSeconds(25f);
        }
    }

    private async void RunSendGetCordLoop()
    {
        try
        {
            while (true)
            {
                await SendGetCordAsync();
                await Task.Delay(50);
            }
        }
        catch (Exception ex)
        {
            LogError("[RunSendGetCordLoop] Exception: " + ex);
        }
    }


    private async Task SendGetCordAsync()
    {
        string scene = SceneManager.GetActiveScene().name;
        string url = $"https://busiatep.ru:9998/get_cord?scene={scene}";

        try
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                var operation = request.SendWebRequest();

                while (!operation.isDone)
                    await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    LogError($"[SendGetCord] HTTP error: {request.error}");
                    return;
                }

                string json = request.downloadHandler.text;

                if (string.IsNullOrEmpty(json))
                    return;

                List<MissileInfo> missiles;
                try
                {
                    missiles = JsonConvert.DeserializeObject<List<MissileInfo>>(json);
                }
                catch (Exception ex)
                {
                    LogError("[SendGetCord] JSON parse error: " + ex.Message);
                    return;
                }

                HashSet<string> receivedKeys = new HashSet<string>();

                foreach (var missile in missiles)
                {
                    if (missile.scene != scene)
                        continue;

                    string key = $"{missile.name}_{missile.ip}";
                    receivedKeys.Add(key);

                    if (MissileManager.IsExists(key))
                    {
                        MissileManager.MoveMissile(missile.name, missile.coords, missile.ip);
                    }
                    else
                    {
                        MissileManager.SpawnMissile(
                            missile.name,
                            missile.coords,
                            missile.skin,
                            missile.ip,
                            missile.components 
                        );
                    }
                }

                var currentKeys = new List<string>(MissileManager.missiles.Keys);
                foreach (var key in currentKeys)
                {
                    if (!receivedKeys.Contains(key))
                    {
                        int underscoreIndex = key.LastIndexOf('_');
                        if (underscoreIndex > 0)
                        {
                            string name = key.Substring(0, underscoreIndex);
                            string ip = key.Substring(underscoreIndex + 1);
                            MissileManager.ExplodeMissile(name, ip);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            LogError($"[SendGetCord] Exception: {e}");
        }
    }

    public static void SendUdpMessage(string message)
    {
        if (StaticUdpClient == null || StaticRemoteEP == null) return;
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            StaticUdpClient.Send(data, data.Length, StaticRemoteEP);
        }
        catch (Exception ex)
        {
            Instance.Logger.LogError("UDP send error: " + ex);
        }
    }

    public static void LogInfo(string message) => Instance.Logger.LogInfo(message);
    public static void LogError(string message) => Instance.Logger.LogError(message);
    public static void LogWarning(string message) => Instance.Logger.LogWarning(message);
}

// temp turned off
//[HarmonyPatch(typeof(CambiarLogoDP))]
//[HarmonyPatch("Start")]
//public class CambiarLogoDPPatch
//{
//    static void Postfix(CambiarLogoDP __instance)
//    {
//        if (__instance.gameObject.GetComponent<LogoSender>() == null)
//        {
//            __instance.gameObject.AddComponent<LogoSender>();
//        }
//    }
//}

public static class MissileInspector
{
    public static string SerializeMissileToJson(GameObject go)
    {
        var result = new Dictionary<string, object>();

        result["name"] = go.name;
        result["position"] = go.transform.position;
        result["rotation"] = go.transform.rotation;
        result["scale"] = go.transform.localScale;

        var componentData = new List<Dictionary<string, object>>();

        foreach (var comp in go.GetComponents<Component>())
        {
            if (comp == null) continue;

            var compInfo = new Dictionary<string, object>();
            Type type = comp.GetType();
            compInfo["type"] = type.FullName;

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                try
                {
                    var value = field.GetValue(comp);
                    compInfo[field.Name] = value;
                }
                catch { }
            }

            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
                try
                {
                    var value = prop.GetValue(comp, null);
                    compInfo[prop.Name] = value;
                }
                catch { }
            }

            componentData.Add(compInfo);
        }

        result["components"] = componentData;

        return JsonConvert.SerializeObject(result, Formatting.None,
            new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            });
    }
}


[HarmonyPatch(typeof(Missile))]
public class MissilePatches
{
    private static HashSet<Missile> launchedMissiles = new HashSet<Missile>();
    private static MissileLauncher launcher;

    private static GameObject[] cachedMissilePrefabs;

    [HarmonyPostfix]
    [HarmonyPatch("FixedUpdate")]
    public static void Postfix_FixedUpdate(Missile __instance)
    {
        try
        {
            var state = __instance.MyMissileState;
            if (state == MissileStates.Moving)
            {
                Vector3 pos = __instance.transform.position;
                Quaternion rot = __instance.transform.rotation;
                string coords = FormatPositionAndRotation(pos, rot);
                string scene = SceneManager.GetActiveScene().name;

                if (launcher == null)
                {
                    launcher = UnityEngine.Object.FindObjectOfType<MissileLauncher>();
                }

                if (!launchedMissiles.Contains(__instance))
                {
                    launchedMissiles.Add(__instance);

                    string playerId = SteamUser.GetSteamID().m_SteamID.ToString();
                    string skin = "unknown";
                    try
                    {
                        var equippedMissile = SecondaryQuestManager.Instance.GetEquipedMissile();
                        if (equippedMissile != null)
                            skin = equippedMissile.ToString();
                    }
                    catch (Exception ex)
                    {
                        MissileModPlugin.LogError("Error getting missile skin: " + ex.Message);
                    }

                    string msgLaunch = $"launch_missile;{playerId};{scene};{coords};{skin}";
                    MissileModPlugin.SendUdpMessage(msgLaunch);

                    string jsonPrefab = MissileInspector.SerializeMissileToJson(__instance.gameObject);

                    string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonPrefab));

                    MissileModPlugin.SendUdpMessage($"missile_prefab_data;{playerId};{scene};{encoded}");
                }
                else
                {
                    string msgMove = $"move_missile;{scene};{coords}";
                    MissileModPlugin.SendUdpMessage(msgMove);
                }
            }
            else
            {
                if (launchedMissiles.Contains(__instance))
                {
                    launchedMissiles.Remove(__instance);
                }
            }
        }
        catch (Exception ex)
        {
            MissileModPlugin.LogInfo("Error in Postfix_FixedUpdate: " + ex);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch("Explode")]
    public static void Prefix_Explode(Missile __instance)
    {
        try
        {
            Vector3 pos = __instance.transform.position;
            Quaternion rot = __instance.transform.rotation;
            string coords = FormatPositionAndRotation(pos, rot);
            string scene = SceneManager.GetActiveScene().name;
            string msg = $"explode_missile;{scene};{coords}";
            MissileModPlugin.SendUdpMessage(msg);

            if (launchedMissiles.Contains(__instance))
                launchedMissiles.Remove(__instance);
        }
        catch (Exception ex)
        {
            MissileModPlugin.LogInfo("Error in Prefix_Explode: " + ex);
        }
    }

    private static string FormatPositionAndRotation(Vector3 pos, Quaternion rot)
    {
        return $"{pos.x},,{pos.y},,{pos.z},,{rot.x},,{rot.y},,{rot.z},,{rot.w}";
    }
}