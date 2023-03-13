using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Newtonsoft.Json;
using Server.Protos;

namespace FileListener
{
    class Program
    {
        static async Task Main(string[] args)
        {

            using var watcher = new FileSystemWatcher(@"D:\Test");

            watcher.NotifyFilter = NotifyFilters.Attributes
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Security
                                 | NotifyFilters.Size;

            watcher.Created += OnCreated;
            watcher.Error += OnError;

            watcher.Filter = "*.json";
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;

            Console.WriteLine("Press enter to exit.");
            Console.ReadLine();

          

        }
        private static readonly object _lockObject = new object();

        private static void OnCreated(object source, FileSystemEventArgs e)
        {
            var path = e.FullPath;
            List<dynamic> batch = new List<dynamic>();
            while (IsFileLocked(e.FullPath))
            {
                Thread.Sleep(500);
            }

            lock (_lockObject)
            {

                using (var fileStream = new FileStream(path, FileMode.Open))
                using (var streamReader = new StreamReader(fileStream))
                {
                    string line;

                    while ((line = streamReader.ReadLine()) != null)
                    {
                        var jsonReader = new JsonTextReader(new StringReader(line))
                        {
                            SupportMultipleContent = true
                        };
                        var jsonSerializer = new JsonSerializer();
                        batch.Add(System.Text.Json.JsonSerializer.Deserialize<dynamic>(line));

                    }


                }
                string jsonBatch = System.Text.Json.JsonSerializer.Serialize(batch);

                try
                {
                    SendRequest(jsonBatch,batch);
                    File.Delete(e.FullPath);

                }
                catch (Exception)
                {

                    Console.WriteLine("error occured"); ;
                }

            }



        }

        private static async Task SendRequest(string jsonBatch, List<dynamic> batch)
        {
            var channel = GrpcChannel.ForAddress("http://localhost:5042");
           // var client = new GrpcService.GrpcServiceClient(channel);
            var client=new GrpcService.GrpcServiceClient(channel);
            
            // create a google.protobuf.Any object and pack the DynamicMessage into it
             using (var call = client.GetInfo(new GrpcRequest { Payload = ConvertListToAny(batch) }))
            {
                while (await call.ResponseStream.MoveNext())
                {
                    Console.WriteLine($"this type was successfully sent {call.ResponseStream.Current.Type}");
                }
            }

        }
        public static Any ConvertListToAny<T>(List<T> list)
        {
            // Serialize the list to a byte array using System.Text.Json.
            byte[] bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(list);

            // Create a new Any object and set its properties.
            Any any = new Any();
            any.Value = ByteString.CopyFrom(bytes);
            any.TypeUrl = $"type.googleapis.com/{typeof(List<T>).FullName}";

            return any;
        }
        private static void OnError(object source, ErrorEventArgs e)
        {
            Console.WriteLine($"FileSystemWatcher error: {e.GetException().Message}");
        }
        private static bool IsFileLocked(string filePath)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {

                }
            }
            catch (IOException)
            {

                return true;
            }


            return false;
        }
    }
}
