using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class MeasurementsUploader : MonoBehaviour
{
    public string host;
    public int port;

    /// <summary>
    /// The experiment that this build is for. This is not the participant/trial ID - experimentid determines where the log files are written.
    /// </summary>
    public string experimentid;

    /// <summary>
    /// The folder in which the experimental data will be written.
    /// </summary>
    public string user;

    public event EventHandler OnUploadSuccessful;

    /// <summary>
    /// This hardcoded version is used by the server to detect when older clients are connecting
    /// </summary>
    private const string version = "0.1";

    public void Send<T>(T serialisable)
    {
        var json = JsonUtility.ToJson(serialisable);
        StartCoroutine(PostRequest(json));
    }

    private IEnumerator PostRequest(string json)
    {
        UriBuilder uriBuilder = new UriBuilder();
        uriBuilder.Scheme = "https";
        uriBuilder.Host = host;
        uriBuilder.Port = port;

        // it is important to create new handlers for each request, as old handlers (even the certificate handler) are disposed of, and will fail if re-used.

        var uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        var downloadHandler = new DownloadHandlerBuffer();
        var certificateHandler = GetCertificateHandler();

        var req = new UnityWebRequest(uriBuilder.Uri, "POST");
        req.uploadHandler = uploadHandler;
        req.downloadHandler = downloadHandler;
        req.certificateHandler = certificateHandler;
        req.SetRequestHeader("content-type", "application/json");
        req.SetRequestHeader("x-key", "placeholder");
        req.SetRequestHeader("x-id", experimentid);
        req.SetRequestHeader("x-user", user);
        req.SetRequestHeader("x-version", version);

        yield return req.SendWebRequest();

        if(req.result == UnityWebRequest.Result.ConnectionError)
        {
            Debug.LogError("Measurements upload failed. Network error " + req.error);
        }
        else
        {
            switch (req.responseCode)
            {
                case 200:
                    Debug.Log("Measurements Uploaded");
                    OnUploadSuccessful.Invoke(this, EventArgs.Empty);
                    break;
                default:
                    Debug.LogError(string.Format("{0} {1} {2} {3}",
                        "Measurements upload failed with server error",
                        req.responseCode.ToString(), 
                        req.error,
                        downloadHandler.text));
                    break;
            }          
        }
    }

    private CertificateHandler GetCertificateHandler()
    {
        switch (Application.platform)
        {
            case RuntimePlatform.WebGLPlayer:
                return new DummyCertificateHandler();   // On the web, the browser will authenticate the certificate for us.
            default:
                return new PrivateCertificateHandler();
        }
    }

    private class PrivateCertificateHandler : CertificateHandler
    {
        /// <summary>
        /// This public key is produced for vrgroupdatacollection.cs.ucl.ac.uk. To work with a different server, 
        /// use OpenSSL to generate a new certificate and replace this string in your application.
        /// </summary>
        private static string PUB_KEY = "3082010A0282010100F4740A1130CF347FC2AD4FE8F8A11D910" +
            "C8C648E04A1EF266779C1E5551F204FF97D408CC85AA1BA6AE4CDBDDB6650F7D0A36C3F3DDAD8F9" +
            "D973121E534C3BB135199E160334408375E423CF3C9E4CAF0B8AA10828A153DD953E72356551A04" +
            "BE63AB2FB8CD4DE9B13C0877AED0EEDFD7B0FC29CDB399AC0E0335FF4FBD94DF814EBE6D273A936" +
            "2C2F473CB039C88EC4AF4C7F592B25AE996A3A729F02BB4B9575B51AD51576E2BC691CA511CDD9B" +
            "D66C9D834EB675FF3A35308C595E5AC2B4831B6AC719BDFCF5FF84555D0232F995E21F037C186D7" +
            "6076EC32A83AC2A8979E0058527F7E5B5E35377B2AD12169A3B25FCA91E6C146B679729F524641D" +
            "483F90203010001";

        protected override bool ValidateCertificate(byte[] certificateData)
        {
            X509Certificate2 certificate = new X509Certificate2(certificateData);
            string pk = certificate.GetPublicKeyString();
            return pk.Equals(PUB_KEY);
        }
    }

    private class DummyCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true; // encryption with no authentication
        }
    }

}
