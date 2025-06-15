using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace AIlibrary
{
    public class VertexAIService : IDisposable
    {
        public string Url { get; set; }
        public string ProjectId { get; set; } = "prj-gen-ai-9571";
        public string LocationId { get; set; } = "us-central1";

        public string Publisher { get; set; } = "google";

        public string ModeId { get; set; } = "gemini-1.5-pro-002";

        public string Token { get; set; }

        private List<FunctionCallItem> _functionCalls = new List<FunctionCallItem>();

        private HttpClient _httpClient;

        private Dictionary<string, Func<Dictionary<string, object>, Task<object>>> _keyValuePairs = new Dictionary<string, Func<Dictionary<string, object>, Task<object>>>();

        public VertexAIService()
        {
            var handler = new HttpClientHandler();
          //  handler.ClientCertificates.Add(new System.Security.Cryptography.X509Certificates.X509Certificate2("xxx.pem"));
          _httpClient = new HttpClient(handler);
        }

        public VertexAIService(HttpClient  httpClient)
        {
            _httpClient = httpClient; 

        }

        public async Task<byte[]> Embeding(string embeding)
        {
            var body = new { instances = new[] { new { content = embeding } } };
            var json = JsonConvert.SerializeObject(body);

            StringContent content = new StringContent(json,Encoding.UTF8,"application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
            HttpResponseMessage response = await _httpClient.PostAsync($"{Url}/v1/projects/{ProjectId}/locations/{LocationId}/publishers/{Publisher}/models/text-embedding-004:predict",content);

            response.EnsureSuccessStatusCode();

            var responsestring = await response.Content.ReadAsStringAsync();

            var rootObject = JsonConvert.DeserializeObject(responsestring) as JObject;

            float[] values = rootObject["predictions"][0]["embeddings"]["values"].ToObject<float[]>();
            byte[] byteArray= values.SelectMany(BitConverter.GetBytes).ToArray();
              
            return byteArray;
        }

        public async Task<string> ChatCompletion (ChatHistory history, GenerationConfig config = null)
        {
            var historynosys = history.GetHistory().Where(t => t.Role != ChatRole.System).ToList();
            dynamic body = new ExpandoObject ();
            body.contents = new List<dynamic>();

            foreach (var item in historynosys)
            {
                dynamic itemobj = new ExpandoObject ();
                itemobj.role = item.Role == ChatRole.User ? "user" : "model";
                itemobj.parts=new List<dynamic>();
                dynamic itemfobj = new ExpandoObject ();
                itemfobj.role = "user";
                itemfobj.parts = new List<dynamic>();
                body.contents.Add(itemobj);
                var deleteparts = new List<dynamic>();
                foreach (var part in item.Parts)
                {
                    dynamic partobj = new ExpandoObject ();
                    itemobj.parts.Add(partobj);
                    if(part.Type==ChatMessageType.Text)
                    {
                        partobj.text = part.Content;
                    }
                    else if (part.Type==ChatMessageType.Image)
                    {
                        dynamic filedata = new ExpandoObject ();
                        partobj.inlineData = filedata;
                        filedata.mimeType = "image/png";
                        filedata.data = part.Content;
                        deleteparts.Add(part);
                    }
                    else if (part.Type==ChatMessageType.FunctionCall)
                    {
                        var fpart = part as FunctionCall;
                        partobj.functionCall = new ExpandoObject();
                        var functioncall = fpart.Functioncall;
                        partobj.functionCall.name = functioncall.Name;
                        partobj.functionCall.args = new ExpandoObject();
                        foreach (var arg in functioncall.Args)
                        {
                            ((IDictionary<string,object>)partobj.functionCall.args).Add(arg.Key,arg.Value);
                        }

                        if(fpart.Functioncall != null)
                        {
                            var fresponse = fpart.Functioncall.FunctionResponse;
                            if(fresponse != null)
                            {
                                dynamic partfobj = new ExpandoObject();
                                partfobj .functionResponse = new ExpandoObject();
                                partfobj.functionResponse.name=((FunctionCall)part).Functioncall.Name;
                                partfobj.functionResponse.response = fresponse;
                                itemfobj.parts.Add(partfobj);
                            }
                        }
                    }
                }

                foreach(var delitem in deleteparts)
                {
                    item.Parts.Remove(delitem);
                }
                if(itemfobj.parts.Count>0)
                {
                    body.contents.Add(itemfobj);
                }
            }

            var historysys = history.GetHistory().FirstOrDefault(t => t.Role == ChatRole.System);
            if(historysys != null)
            {
                dynamic sysobj = new ExpandoObject();
                sysobj.role = "system";
                sysobj.parts = new List<dynamic>();
                sysobj.parts.Add(new { text= historysys.Parts[0].Content });
                body.systemInstruction = sysobj;
            }

            if(config !=null)
            {
                dynamic configobj = new ExpandoObject();
                if(config.Temperature.HasValue)
                {
                    configobj.temperature= config.Temperature.Value;
                }

                if (config.TopP.HasValue)
                {
                    configobj.topP = config.TopP.Value;
                }
                if (!string.IsNullOrEmpty(config.ResponseMimeType))
                {
                    configobj.responseMimeType = config.ResponseMimeType;
                }

                if (config.ResponseSchema !=null)
                {
                    configobj.responseSchema = config.ResponseSchema;
                }
                body.generationConfig =configobj;
            }

            if(_functionCalls.Count>0)
            {
                body.tools = new List<dynamic>();
                foreach(var item in _functionCalls)
                {
                    dynamic tool = new ExpandoObject();
                    body.tools.Add(tool);
                    tool.function_declarations = new List<dynamic>();
                    dynamic fd= new ExpandoObject();
                    tool.function_declarations.Add(fd);
                    fd.name = item.Name;
                    fd.description = item.Description;
                    fd.parameters = new ExpandoObject();
                    fd.parameters.required = new List<string>();
                    fd.parameters.type ="object";
                    fd.parameters.properties = new ExpandoObject();
                    var expandoDict = fd.parameters.properties as IDictionary<string, object>;

                    foreach (var param in item.FunctionCallItemParams)
                    {
                        dynamic paramobj = new ExpandoObject();
                        paramobj.type =param.Type.ToString().ToLower();
                        paramobj.description = param.Description;
                        if(param.EnumValues != null)
                        {
                            ((IDictionary<string, object>)paramobj)["enum"] = param.EnumValues;
                        }
                        if(param.IsRequired)
                        {
                            fd.parameters.required.Add(param.Name);
                        }
                        expandoDict.Add(param.Name, paramobj);
                    }
                }
            }

            string json = JsonConvert.SerializeObject(body);
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
            HttpResponseMessage response = await _httpClient.PostAsync($"{Url}/v1/projects/{ProjectId}/locations/{LocationId}/publishers/{Publisher}/models/{ModeId}:generateContent", content);

            response.EnsureSuccessStatusCode();
            var responsestring = await response.Content.ReadAsStringAsync();

            var jo = JsonConvert.DeserializeObject(responsestring) as JObject;
            var parts = jo["candidates"][0]["content"]["parets"] as JArray;
            if (parts.Last.Children<JProperty>().Where(t => t.Name == "functionCall").Count() > 0)
            {
                var funcalllist = new List<FunctionCall>();
                var tasks = new List<Task<object>>();

                foreach (var fc in parts)
                {
                    var chatfunctioncall = new ChatFunctionCall { Name = fc["functionCall"]["name"].ToString(), Args = fc["functionCall"]["args"].ToObject<Dictionary<string, object>>() };
                    var func = _keyValuePairs[chatfunctioncall.Name];
                    if (func != null)
                    {
                        tasks.Add(func.Invoke(fc["functionCall"]["args"].ToObject<Dictionary<string, object>>()));
                    }
                    var fcall = new FunctionCall { Functioncall = chatfunctioncall };
                    funcalllist.Add(fcall);
                }
                Task.WaitAll(tasks.ToArray());
                for (int i = 0; i < funcalllist.Count; i++)
                {
                    funcalllist[i].Functioncall.FunctionResponse = tasks[i].Result;
                }
                history.AddFunctionCall(funcalllist);
                return await ChatCompletion(history, config);
            }
            var removedhistory = history.GetHistory().Where(t => t.Parts[0] is FunctionCall).ToList();
            foreach (var item in removedhistory)
            {
                history.GetHistory().Remove(item);
            }

            return jo["candidates"][0]["content"]["parets"][0]["text"].ToString();
        }

        public VertexAIService DefineFunction (FunctionCallItem item , Func<Dictionary<string,object>, Task<object>> func)
        {
            var fitem = _functionCalls.FirstOrDefault(t => t.Name == item.Name);
            if(fitem!=null)
            {
                _functionCalls.Remove(fitem);
                _keyValuePairs.Remove(fitem.Name);
            }

            _functionCalls.Add(item);
            _keyValuePairs.Add(item.Name, func);
            return this;

        }

        public void Dispose()
        {
           _functionCalls .Clear();
            _keyValuePairs .Clear();
            _httpClient?.Dispose();
        }
    }

    public class ChatHistory
    {
        private List<ChatHistoryItem> _list;

        public ChatHistory()
        {
            _list = new List<ChatHistoryItem>();
        }

        internal List<ChatHistoryItem> GetHistory()
        {
            return _list;
        }

        public void AddUserMessage(string content)
        {
            _list.Add(new ChatHistoryItem { Role = ChatRole.User, Parts = new List<IChatMessage> { new ChatMessage { Content = content } } });

        }

        internal void AddFunctionCall(List<FunctionCall> fcall)
        {
            var item = new ChatHistoryItem { Role = ChatRole.Assistant, Parts = new List<IChatMessage>() };
            item.Parts.AddRange(fcall);
            _list.Add(item);    
        }

        public void AddAssistantMessage(string content)
        {
            _list.Add(new ChatHistoryItem { Role = ChatRole.Assistant, Parts = new List<IChatMessage> { new ChatMessage { Content = content } } });
        }

        public void AddSystemMessage (string content)
        {
            var systemMessage = _list.FirstOrDefault(t => t.Role == ChatRole.System);
            if(systemMessage==null)
            {
                systemMessage = new ChatHistoryItem { Role = ChatRole.System };
                _list.Insert(0, systemMessage);
            }
            systemMessage.Parts = new List<IChatMessage> { new ChatMessage { Content = content } };
        }
    }

    public class FunctionCallItem
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public List<FunctionCallItemParam> FunctionCallItemParams { get; set; }
    }

    public class ChatHistoryItem
    {
        public ChatRole Role { get; set; }
        public List<IChatMessage> Parts { get; set; }
    }

    public enum ChatRole
    {
        User, Assistant,System
    }

    public static class ChatMessageType
    {
        public const string Text = "text";
        public const string Image = "image";
        internal const string FunctionCall = "functionCall";
        internal const string FunctionResponse = "functionResponse";
    }

    public interface IChatMessage
    {
        public string Type {  get; set; }
        public string Content { get; set; }

    }

    public class ChatMessage:IChatMessage
    {
        public string Type { get; set; } = ChatMessageType.Text;
        public string Content { get; set; }

        public ChatMessage()
        {

        }

        public ChatMessage(byte[] bytes)
        {
            Type = ChatMessageType.Image;
            Content=Convert.ToBase64String(bytes);
        }
    }

    internal class FunctionCall : IChatMessage
    {
        public string Type { get; set; }=ChatMessageType.FunctionCall;
        public string Content { get; set; }

        internal ChatFunctionCall  Functioncall { get; set; }
    }

    internal class ChatFunctionCall
    {
        internal string Name { get; set; }
        internal Dictionary<string,object> Args { get; set; }
        internal dynamic FunctionResponse { get; set; }
    }

    public class GenerationConfig
    {
        public float? Temperature { get; set; }
        public float? TopP {  get; set; }

        public string ResponseMimeType { get; set; }
        public dynamic? ResponseSchema { get; set; }
    }

    public class FunctionCallItemParam
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public FunctionCallItemParamType Type { get; set; }
        public bool IsRequired { get; set; }
        public List<string> EnumValues  { get; set; }

    }

    public enum FunctionCallItemParamType
    {
        STRING,INTEGER,BOOLEAN,NUMBER
    }
}

