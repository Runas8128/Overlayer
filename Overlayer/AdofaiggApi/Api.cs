using Newtonsoft.Json;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Overlayer.AdofaiggApi.Types;
using AgLevel = Overlayer.AdofaiggApi.Types.Level;
using System;

namespace Overlayer.AdofaiggApi
{
    public class Api
    {
        public static bool EscapeParameter { get; set; } = false;
        public const string API = "https://adofai.gg:9200/api/v1";
        static readonly WebClient api;
        static Api()
        {
            api = new WebClient();
            api.Encoding = Encoding.UTF8;
        }
        Api(string header) => this.header = header;
        public readonly string header;
        public static readonly Api Level = new Api("/levels");
        public static readonly Api PlayLogs = new Api("/playLogs");
        public static readonly Api Ranking = new Api("/ranking");
        public async Task<Response<T>> Request<T>(params Parameter[] parameters) where T : Json
            => await Request<T>(new Parameters(parameters));
        public async Task<Response<T>> Request<T>(Parameters parameters) where T : Json
        {
            string reqUrl = $"{API}{header}{parameters}";
            Main.Logger.Log($"Request Url: {reqUrl}");
            string json = await api.DownloadStringTaskAsync(reqUrl);
            Response<T> r = JsonConvert.DeserializeObject<Response<T>>(json);
            r.json = json;
            return r;
        }
        public static AgLevel GetLevel(int id)
            => JsonConvert.DeserializeObject<AgLevel>(api.DownloadString($"{API}/levels/{id}"));
    }
}
