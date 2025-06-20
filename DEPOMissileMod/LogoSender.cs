using System.Collections;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

/// <summary>
/// Handles sending the DP logo to UDP server.
/// Currently inactive, but saved for future use.
/// </summary>
public class LogoSender : MonoBehaviour
{
    private float timer = 0f;
    private const float interval = 10f;
    private UdpClient udpClient;
    private IPEndPoint remoteEP;
    private CambiarLogoDP cambiarLogoDP;
    private bool initialized = false;

    private void Awake()
    {
        udpClient = new UdpClient();
        remoteEP = new IPEndPoint(Dns.GetHostAddresses("busiatep.ru")[0], 9999);
    }

    private void Start()
    {
        cambiarLogoDP = GetComponent<CambiarLogoDP>();
        if (cambiarLogoDP != null)
        {
            initialized = true;
        }
    }

    private void Update()
    {
        if (!initialized) return;

        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer = 0f;
            StartCoroutine(CaptureAndSendLogo());
        }
    }

    private IEnumerator CaptureAndSendLogo()
    {
        yield return new WaitForEndOfFrame();

        int slotIndex = GlobalData.ranuraActualGuardado - 1;
        if (slotIndex < 0 || slotIndex >= cambiarLogoDP.materialLogo.Length)
        {
            MissileModPlugin.LogWarning("Logo index out of range");
            yield break;
        }

        Material logoMaterial = cambiarLogoDP.materialLogo[slotIndex];
        Texture texture = logoMaterial.mainTexture;

        if (texture == null)
        {
            MissileModPlugin.LogWarning("Logo texture is missing");
            yield break;
        }

        int width = texture.width;
        int height = texture.height;

        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture currentRT = RenderTexture.active;

        Graphics.Blit(texture, rt);
        RenderTexture.active = rt;

        Texture2D tex2D = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex2D.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex2D.Apply();

        RenderTexture.active = currentRT;
        RenderTexture.ReleaseTemporary(rt);

        FlipTextureVertically(tex2D);

        byte[] jpgData = tex2D.EncodeToJPG(90);

        if (jpgData.Length > 60000)
        {
            MissileModPlugin.LogWarning($"JPG too large for UDP: {jpgData.Length} bytes");
        }

        try
        {
            udpClient.Send(jpgData, jpgData.Length, remoteEP);
            MissileModPlugin.LogInfo($"JPG logo sent ({jpgData.Length} bytes) to {remoteEP}");
        }
        catch (System.Exception ex)
        {
            MissileModPlugin.LogError("Error sending JPG: " + ex.Message);
        }
    }

    private void FlipTextureVertically(Texture2D texture)
    {
        Color[] pixels = texture.GetPixels();
        int width = texture.width;
        int height = texture.height;

        for (int y = 0; y < height / 2; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int topIndex = y * width + x;
                int bottomIndex = (height - 1 - y) * width + x;

                Color temp = pixels[topIndex];
                pixels[topIndex] = pixels[bottomIndex];
                pixels[bottomIndex] = temp;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
    }

    private void OnDestroy()
    {
        udpClient?.Close();
    }
}