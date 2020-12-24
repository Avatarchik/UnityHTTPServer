﻿using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Reflection;


public class UnityHTTPServer : MonoBehaviour
{
    [SerializeField]
    public int port;
    [SerializeField]
    public string SaveFolder;
    [SerializeField]
    public bool UseStreamingAssetsPath = false;
    [SerializeField]
    public int bufferSize = 16;
    public static UnityHTTPServer Instance;

    public MonoBehaviour controller;
    SimpleHTTPServer myServer;
    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (myServer == null)
        {
            Init();
        }
    }
    void Init()
    {
        StartServer();
    }

    public void StartServer()
    {
        myServer = new SimpleHTTPServer(GetSaveFolderPath, port, controller, bufferSize);
    }
    string GetSaveFolderPath
    {
        get
        {
            if (UseStreamingAssetsPath)
            {
                return Application.streamingAssetsPath;
            }
            return SaveFolder;
        }
    }
    public static string GetHttpUrl()
    {
        return $"http://{GetLocalIPAddress()}:" + Instance.myServer.Port + "/";
    }

    /// <summary>
    /// Get the Host IPv4 adress
    /// </summary>
    /// <returns>IPv4 address</returns>
    public static string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }
    public void StopServer()
    {
        Application.Quit();
    }

    void OnApplicationQuit()
    {
        myServer.Stop();
    }


    class SimpleHTTPServer
    {
        static int bufferSize = 16;
        public System.Object _methodController;
        private readonly string[] _indexFiles =
        {
                    "index.html",
                    "index.htm",
                    "default.html",
                    "default.htm"
            };

        private static IDictionary<string, string> _mimeTypeMappings = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            {
			#region extension to MIME type list
					{ ".asf", "video/x-ms-asf" },
                    { ".asx", "video/x-ms-asf" },
                    { ".avi", "video/x-msvideo" },
                    { ".bin", "application/octet-stream" },
                    { ".cco", "application/x-cocoa" },
                    { ".crt", "application/x-x509-ca-cert" },
                    { ".css", "text/css" },
                    { ".deb", "application/octet-stream" },
                    { ".der", "application/x-x509-ca-cert" },
                    { ".dll", "application/octet-stream" },
                    { ".dmg", "application/octet-stream" },
                    { ".ear", "application/java-archive" },
                    { ".eot", "application/octet-stream" },
                    { ".exe", "application/octet-stream" },
                    { ".flv", "video/x-flv" },
                    { ".gif", "image/gif" },
                    { ".hqx", "application/mac-binhex40" },
                    { ".htc", "text/x-component" },
                    { ".htm", "text/html" },
                    { ".html", "text/html" },
                    { ".ico", "image/x-icon" },
                    { ".img", "application/octet-stream" },
                    { ".svg", "image/svg+xml" },
                    { ".iso", "application/octet-stream" },
                    { ".jar", "application/java-archive" },
                    { ".jardiff", "application/x-java-archive-diff" },
                    { ".jng", "image/x-jng" },
                    { ".jnlp", "application/x-java-jnlp-file" },
                    { ".jpeg", "image/jpeg" },
                    { ".jpg", "image/jpeg" },
                    { ".js", "application/x-javascript" },
                    { ".mml", "text/mathml" },
                    { ".mng", "video/x-mng" },
                    { ".mov", "video/quicktime" },
                    { ".mp3", "audio/mpeg" },
                    { ".mpeg", "video/mpeg" },
                    { ".mp4", "video/mp4" },
                    { ".mpg", "video/mpeg" },
                    { ".msi", "application/octet-stream" },
                    { ".msm", "application/octet-stream" },
                    { ".msp", "application/octet-stream" },
                    { ".pdb", "application/x-pilot" },
                    { ".pdf", "application/pdf" },
                    { ".pem", "application/x-x509-ca-cert" },
                    { ".pl", "application/x-perl" },
                    { ".pm", "application/x-perl" },
                    { ".png", "image/png" },
                    { ".prc", "application/x-pilot" },
                    { ".ra", "audio/x-realaudio" },
                    { ".rar", "application/x-rar-compressed" },
                    { ".rpm", "application/x-redhat-package-manager" },
                    { ".rss", "text/xml" },
                    { ".run", "application/x-makeself" },
                    { ".sea", "application/x-sea" },
                    { ".shtml", "text/html" },
                    { ".sit", "application/x-stuffit" },
                    { ".swf", "application/x-shockwave-flash" },
                    { ".tcl", "application/x-tcl" },
                    { ".tk", "application/x-tcl" },
                    { ".txt", "text/plain" },
                    { ".war", "application/java-archive" },
                    { ".wbmp", "image/vnd.wap.wbmp" },
                    { ".wmv", "video/x-ms-wmv" },
                    { ".xml", "text/xml" },
                    { ".xpi", "application/x-xpinstall" },
                    { ".zip", "application/zip" },
			#endregion
			};
        private Thread _serverThread;
        private string _rootDirectory;
        private HttpListener _listener;
        private int _port;

        public int Port
        {
            get { return _port; }
            private set { }
        }

        /// <summary>
        /// Construct server with given port, path ,controller and buffer.
        /// </summary>
        /// <param name="path">伺服器 root 的實體路徑</param>
        /// <param name="port">Port 開放 http 訪問的 port</param>
        /// <param name="mc">處理方法的控制器，邏輯類似 ASP MVC</param>
        public SimpleHTTPServer(string path, int port, System.Object mc, int buffer)
        {
            this._methodController = mc;
            this.Initialize(path, port);
        }

        /// <summary>
        /// Construct server with given port, path and buffer.
        /// </summary>
        /// <param name="path">伺服器 root 的實體路徑</param>
        /// <param name="port">Port 開放 http 訪問的 port</param>
        public SimpleHTTPServer(string path, int port, int buffer)
        {
            bufferSize = buffer;
            this.Initialize(path, port);
        }

        /// <summary>
        /// Stop Server
        /// </summary>
        public void Stop()
        {
            _serverThread.Abort();
            _listener.Stop();
        }

        private void Listen()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://*:" + _port.ToString() + "/");
            _listener.Start();
            while (true)
            {
                try
                {
                    HttpListenerContext context = _listener.GetContext();
                    Process(context);
                }
                catch (Exception ex)
                {
                    print(ex);
                }
            }
        }

        private void Process(HttpListenerContext context)
        {
            string filename = context.Request.Url.AbsolutePath;
            //					print(filename);
            filename = filename.Substring(1);

            if (string.IsNullOrEmpty(filename))
            {
                foreach (string indexFile in _indexFiles)
                {
                    if (File.Exists(Path.Combine(_rootDirectory, indexFile)))
                    {
                        filename = indexFile;
                        break;
                    }
                }
            }

            filename = Path.Combine(_rootDirectory, filename);

            Dictionary<string, object> namedParameters = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(context.Request.Url.Query))
            {
                print(context.Request.Url.Query);
                var query = context.Request.Url.Query.Replace("?", "").Split('&');
                foreach (var item in query)
                {
                    var t = item.Split('=');


                    namedParameters.Add(t[0], t[1]);
                }
            }

            var method = TryParseToController(context.Request.Url);

            if (File.Exists(filename))
            {
                try
                {
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    Stream input = new FileStream(filename, FileMode.Open);

                    //Adding permanent http response headers
                    string mime;
                    context.Response.ContentType = _mimeTypeMappings.TryGetValue(Path.GetExtension(filename), out mime) ? mime : "application/octet-stream";
                    context.Response.ContentLength64 = input.Length;
                    context.Response.AddHeader("Date", DateTime.Now.ToString("r"));
                    context.Response.AddHeader("Last-Modified", System.IO.File.GetLastWriteTime(filename).ToString("r"));

                    byte[] buffer = new byte[1024 * bufferSize];
                    int nbytes;
                    while ((nbytes = input.Read(buffer, 0, buffer.Length)) > 0)
                        context.Response.OutputStream.Write(buffer, 0, nbytes);
                    input.Close();


                    context.Response.OutputStream.Flush();
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    print(ex);
                }

            }
            //A ASP.Net MVC like controller route
            else if (method != null)
            {
                context.Response.ContentType = "application/json";

                object result;
                result = method.InvokeWithNamedParameters(_methodController, namedParameters);
if(result == null){
result = new VoidResult {msg ="Success"};
}
#if UseLitJson
                string jsonString = LitJson.JsonMapper.ToJson(result);
#else
				string jsonString = JsonUtility.ToJson(result);
#endif
                byte[] jsonByte = Encoding.UTF8.GetBytes(jsonString);
                Stream jsonStream = new MemoryStream(jsonByte);
                byte[] buffer = new byte[1024 * bufferSize];
                int nbytes;
                while ((nbytes = jsonStream.Read(buffer, 0, buffer.Length)) > 0)
                    context.Response.OutputStream.Write(buffer, 0, nbytes);
                jsonStream.Close();
                context.Response.OutputStream.Flush();
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }

            context.Response.OutputStream.Close();
        }

        private void Initialize(string path, int port)
        {
            this._rootDirectory = path;
            this._port = port;
            _serverThread = new Thread(this.Listen);
            _serverThread.Start();
        }

        System.Reflection.MethodInfo TryParseToController(Uri uri)
        {
            string methodName = uri.Segments[1].Replace("/", "");
            System.Reflection.MethodInfo method = null;
            try
            {
                method = _methodController.GetType().GetMethod(methodName);
            }
            catch
            {
                method = null;
            }

            return method;
        }

        class  VoidResult{
            public string msg;
        }
    }
}

//MethodInfo 可使用具名變數的擴充方法
public static class ReflectionExtensions
{

    public static object InvokeWithNamedParameters(this MethodBase self, object obj, IDictionary<string, object> namedParameters)
    {
        return self.Invoke(obj, MapParameters(self, namedParameters));
    }

    public static object[] MapParameters(MethodBase method, IDictionary<string, object> namedParameters)
    {
        ParameterInfo[] paramInfos = method.GetParameters().ToArray();
        object[] parameters = new object[paramInfos.Length];
        int index = 0;
        foreach (var item in paramInfos)
        {
            object parameterName;
            if (!namedParameters.TryGetValue(item.Name, out parameterName))
            {
                parameters[index] = Type.Missing;
                index++;
                continue;
            }
            parameters[index] = ObjectCastTypeByParameterInfo(item, parameterName);
            index++;
        }
        return parameters;
    }
    static object ObjectCastTypeByParameterInfo(ParameterInfo parameterInfo, object value)
    {
        if (parameterInfo.ParameterType == typeof(int) ||
            parameterInfo.ParameterType == typeof(System.Int32) ||
            parameterInfo.ParameterType == typeof(System.Int16) ||
            parameterInfo.ParameterType == typeof(System.Int64))
        {
            return (int)Convert.ChangeType(value, typeof(int));
        }
        else
        {
            return value;
        }

    }
}