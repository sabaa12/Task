using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Server.Protos;
using System.Text;

namespace GrpcServer.Services
{
    public class Service : GrpcService.GrpcServiceBase
    {
        public override async Task GetInfo(GrpcRequest request, IServerStreamWriter<GrpcResponse> responseStream, ServerCallContext context)
        {

            List<dynamic> logs = ConvertAnyToList(request.Payload);
            var groups = logs.GroupBy(log => (string)log.type);

            string currentDirectory = Directory.GetCurrentDirectory();
            string rootDirectory = Path.Combine(currentDirectory, "root");

            await WriteTofile(responseStream, groups, rootDirectory);

        }

        private static async Task WriteTofile(IServerStreamWriter<GrpcResponse> responseStream, IEnumerable<IGrouping<string, dynamic>> groups, string rootDirectory)
        {
            string currentDate = DateTime.Now.ToString("yyyy-MM-dd");

            foreach (var group in groups)
            {
                string typeDirectory = Path.Combine(rootDirectory, group.Key);

                if (!Directory.Exists(typeDirectory))
                {
                    Directory.CreateDirectory(typeDirectory);
                }

                string filePath = Path.Combine(typeDirectory, currentDate + ".json").Replace('\\', '/');

                try
                {
                    string Json = "";
                    if (File.Exists(filePath))
                    {
                        var json = File.ReadAllText(filePath);
                        var list = JsonConvert.DeserializeObject<List<dynamic>>(json);

                        if (list != null)
                        {
                            list.AddRange(group);
                        }
                        else
                        {
                            list = new List<dynamic> { JToken.Parse(json), group };
                        }

                        Json = JsonConvert.SerializeObject(list, Formatting.Indented);
                    }
                    else
                    {
                        var jsonArray = JArray.FromObject(group);
                        Json = jsonArray.ToString(Formatting.Indented);

                    }

                    File.WriteAllText(filePath, Json);
                    await responseStream.WriteAsync(new GrpcResponse { Type = group.Key });


                }
                catch (Exception)
                {
                    await responseStream.WriteAsync(new GrpcResponse { Type = string.Empty });
                }
            }
        }
        public static List<dynamic> ConvertAnyToList(Any any)
        {
            var bytes = any.Value.ToByteArray();

            List<dynamic> list = JsonConvert.DeserializeObject<List<dynamic>>(Encoding.ASCII.GetString(bytes));

            return list;
        }

    }
}
