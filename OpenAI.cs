using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System.Collections.Specialized;
using System.Dynamic;
using System.Net.Http.Headers;
using System.Text;
using System.Web;

namespace AIlibrary
{

    public class Assistant
    {
        private readonly string _authorization;
        private readonly string _url;

        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Model { get; private set; }

        public string[] FileIds { get; private set; }
        public object Metadata { get; private set; }

        internal Assistant(string authorization, string url)
        {
            _authorization = authorization;
            _url = url;
        }

        public async Task<Assistant> CreateAsync(string model, string name = null, string instructions = null, List<IOpenAITool> tools = null, List<string> file_ids = null)
        {
            AssistantResult result = null;
            var httpClient = HttpClientFactory.GetHttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, _url);

            // 添加请求头
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authorization);

            request.Headers.Add("OpenAI-Beta", "assistants=v1");

            // 设置请求体为JSON
            dynamic requestBody = new ExpandoObject();
            requestBody.model = model;


            if (!string.IsNullOrWhiteSpace(name))
            {
                requestBody.name = name;
            }

            if (!string.IsNullOrWhiteSpace(instructions))
            {
                requestBody.instructions = instructions;
            }

            if (file_ids != null && file_ids.Count > 0)
            {
                requestBody.file_ids = file_ids;
            }

            if (tools != null && tools.Count > 0)
            {
                requestBody.tools = tools.Select(x => x.ToJson()).ToList();
            }


            request.Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");


            HttpResponseMessage response = await httpClient.SendAsync(request);


            string responseContent = await response.Content.ReadAsStringAsync();

            result = JsonConvert.DeserializeObject<AssistantResult>(responseContent, new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new DefaultNamingStrategy()
                }
            });

            Id = result.Id;
            Name = result.Name;
            FileIds = result.FileIds;
            Metadata = result.Metadata;


            return this;

        }
    }


    internal class AssistantResult
    {
        [JsonProperty("id")]
        internal string Id { get; set; }

        [JsonProperty("name")]
        internal string Name { get; set; }
        [JsonProperty("model")]
        internal string Model { get; set; }
        [JsonProperty("file_ids")]
        internal string[] FileIds { get; set; }
        [JsonProperty("metadata")]
        internal object Metadata { get; set; }
    }


    internal class ThreadResult
    {
        [JsonProperty("id")]
        internal string Id { get; set; }
        [JsonProperty("metadata")]
        internal object Metadata { get; set; }
    }

    public class SendMessage
    {

        public SendMessage(string content)
        {
            Content = content;
        }
        [JsonProperty("role")]
        public string Role
        {
            get; private set;
        } = "user";

        [JsonProperty("content")]
        public string Content
        {
            get; private set;
        }
        [JsonProperty("file_ids")]
        public string[] FileIds
        {
            get; set;
        }
        [JsonProperty("metadata")]
        public object Metadata
        {
            get; set;
        }
    }

    public class Messages
    {
        [JsonProperty("object")]
        public string MessagesObject { get; set; }

        [JsonProperty("data")]
        public List<Message> Data { get; set; }
        [JsonProperty("first_id")]
        public string First
        { get; set; }
        [JsonProperty("last_id")]
        public string Last
        { get; set; }
        [JsonProperty("has_more")]
        public bool HasMore
        { get; set; }

    }


    public class Message
    {

        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("object")]
        public string MessageObject { get; set; }
        [JsonProperty("created_at")]
        public int? Created { get; set; }
        [JsonProperty("thread_id")]
        public string ThreadId { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }
        [JsonProperty("content")]
        public JArray Content { get; set; }
        public object[] file_ids { get; set; }
        [JsonProperty("assistant_id")]
        public string AssistantId { get; set; }
        [JsonProperty("run_id")]

        public string RunId { get; set; }

        [JsonProperty("metadata")]
        public JObject Metadata { get; set; }






    }


    public class Thread : IDisposable
    {
        private readonly string _authorization;
        private readonly string _url;



        public string Id { get; set; }

        public object Metadata { get; set; }

        public Thread(string authorization, string url)
        {
            _authorization = authorization;
            _url = url;
        }

        private Dictionary<string, Func<Dictionary<string, string>, string>> _handler = new Dictionary<string, Func<Dictionary<string, string>, string>>();

        public Thread CleanFunctions()
        {
            _handler.Clear();
            return this;

        }

        public Thread CleanFunction(string functionname)
        {
            if (_handler.ContainsKey(functionname))
            {
                _handler.Remove(functionname);
            }
            return this;
        }

        public Thread WithFunction(string functionname, Func<Dictionary<string, string>, string> func)
        {
            _handler.Add(functionname, func);
            return this;
        }


        public async Task<Messages> GetMessageList(int? limit = null, string order = null, string after = null, string before = null)
        {
            Messages messages = null;
            NameValueCollection queryParameters = new NameValueCollection();

            queryParameters.Add("limit", limit.ToString());
            queryParameters.Add("order", order);
            queryParameters.Add("after", after);
            queryParameters.Add("before", before);


            var array = queryParameters.AllKeys.Where(t => queryParameters[t] != null).Select(s => string.Format("{0}={1}", HttpUtility.UrlEncode(s), HttpUtility.UrlEncode(queryParameters[s]))).ToArray();
            string url = string.Empty;
            if (array.Length > 0)
            {
                url = $"{_url}/{Id}/messages?{string.Join("&", array)}";
            }
            else
            {
                url = $"{_url}/{Id}/messages";
            }

            var httpClient = HttpClientFactory.GetHttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            // 添加请求头
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authorization);

            request.Headers.Add("OpenAI-Beta", "assistants=v1");

            HttpResponseMessage response = await httpClient.SendAsync(request);

            string responseContent = await response.Content.ReadAsStringAsync();

            messages = JsonConvert.DeserializeObject<Messages>(responseContent);

            return messages;


        }

        public async Task<Run> RunAsync(string assistantid, string model = null, string instructions = null, string additional_instructions = null, List<IOpenAITool> tools = null)
        {
            Run run = null;
            var httpClient = HttpClientFactory.GetHttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/{Id}/runs");
            // 添加请求头
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authorization);

            request.Headers.Add("OpenAI-Beta", "assistants=v1");

            dynamic requestBody = new ExpandoObject();
            requestBody.assistant_id = assistantid;

            if (!string.IsNullOrWhiteSpace(model))
            {
                requestBody.model = model;
            }

            if (!string.IsNullOrWhiteSpace(instructions))
            {
                requestBody.instructions = instructions;
            }
            if (!string.IsNullOrWhiteSpace(additional_instructions))
            {
                requestBody.additional_instructions = additional_instructions;
            }



            if (tools != null && tools.Count > 0)
            {
                requestBody.tools = tools.Select(x => x.ToJson()).ToList();
            }

            string requestbody = requestbody = JsonConvert.SerializeObject(requestBody);

            request.Content = new StringContent(requestbody, Encoding.UTF8, "application/json");


            HttpResponseMessage response = await httpClient.SendAsync(request);

            string responseContent = await response.Content.ReadAsStringAsync();

            var temp = JsonConvert.DeserializeObject<RunData>(responseContent);

            if (temp != null)
            {
                run = new Run(_authorization, _url, _handler);
                var tempproperties = temp.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                foreach (var prop in tempproperties)
                {
                    var value = prop.GetValue(temp);
                    if (value != null)
                    {
                        run.GetType().GetProperty(prop.Name).SetValue(run, value);
                    }
                }
            }

            return run;

        }

        public async Task<Thread> SendAsync(SendMessage message)
        {
            if (string.IsNullOrEmpty(Id))
            {
                throw new Exception("Please create a thread first");
            }
            var httpClient = HttpClientFactory.GetHttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/{Id}/messages");
            // 添加请求头
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authorization);

            request.Headers.Add("OpenAI-Beta", "assistants=v1");

            string requestbody = requestbody = JsonConvert.SerializeObject(message, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            });

            request.Content = new StringContent(requestbody, Encoding.UTF8, "application/json");


            HttpResponseMessage response = await httpClient.SendAsync(request);

            return this;
        }

        public async Task<Thread> CreateAsync(List<SendMessage> messages = null)
        {
            ThreadResult result = null;
            var httpClient = HttpClientFactory.GetHttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, _url);

            // 添加请求头
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authorization);

            request.Headers.Add("OpenAI-Beta", "assistants=v1");

            string requestbody = string.Empty;
            if (messages != null)
            {
                var obj = new { messages = messages };
                requestbody = JsonConvert.SerializeObject(obj);
            }


            request.Content = new StringContent(requestbody, Encoding.UTF8, "application/json");


            HttpResponseMessage response = await httpClient.SendAsync(request);


            string responseContent = await response.Content.ReadAsStringAsync();

            result = JsonConvert.DeserializeObject<ThreadResult>(responseContent);

            Id = result.Id;
            Metadata = result.Metadata;



            return this;
        }

        public void Dispose()
        {
            if (!string.IsNullOrEmpty(Id))
            {
                var httpClient = HttpClientFactory.GetHttpClient();
                var request = new HttpRequestMessage(HttpMethod.Delete, $"{_url}/{Id}");

                // 添加请求头
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authorization);

                request.Headers.Add("OpenAI-Beta", "assistants=v1");







                HttpResponseMessage response = httpClient.Send(request);


                string responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();




            }

        }
    }


    internal class RunData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("object")]
        public string RunObject { get; set; }
        [JsonProperty("created_at")]
        public int? Created { get; set; }
        [JsonProperty("assistant_id")]
        public string AssistantId { get; set; }
        [JsonProperty("thread_id")]
        public string ThreadId { get; set; }
        [JsonProperty("status")]
        public string Status { get; set; }
        [JsonProperty("started_at")]
        public int? Started { get; set; }
        [JsonProperty("expires_at")]
        public int? Expires { get; set; }
        [JsonProperty("cancelled_at")]
        public int? Cancelled { get; set; }
        [JsonProperty("failed_at")]
        public int? Failed { get; set; }
        [JsonProperty("completed_at")]
        public int? Completed { get; set; }
        [JsonProperty("required_action")]


        public RequiredAction RequiredAction { get; set; }
        [JsonProperty("last_error")]
        public RunError LastError { get; set; }
        [JsonProperty("model")]
        public string Model { get; set; }
        [JsonProperty("instructions")]
        public string Instructions { get; set; }
        [JsonProperty("file_ids")]
        public string[] FileIds { get; set; }
        [JsonProperty("usage")]
        public RunUsage Usage { get; set; }

    }


    public class Run
    {
        private readonly string _authorization;
        private readonly string _url;

        private Dictionary<string, Func<Dictionary<string, string>, string>> _handler = new Dictionary<string, Func<Dictionary<string, string>, string>>();

        internal Run(string authorization, string url, Dictionary<string, Func<Dictionary<string, string>, string>> handler)
        {
            _authorization = authorization;
            _url = url;
            _handler = handler;
        }



        public string Id { get; set; }


        public string RunObject { get; set; }

        public int? Created { get; set; }

        public string AssistantId { get; set; }

        public string ThreadId { get; set; }
        public string Status { get; set; }

        public int? Started { get; set; }

        public int? Expires { get; set; }

        public int? Cancelled { get; set; }

        public int? Failed { get; set; }

        public int? Completed { get; set; }


        public RequiredAction RequiredAction { get; set; }

        public RunError LastError { get; set; }
        public string Model { get; set; }
        public string Instructions { get; set; }

        public string[] FileIds { get; set; }

        public RunUsage Usage { get; set; }


        public async Task<Run> RetrieveAsync(string threadid)
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                throw new Exception("Please set the run id");
            }
            var httpClient = HttpClientFactory.GetHttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_url}/{threadid}/runs/{Id}");
            // 添加请求头
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authorization);

            request.Headers.Add("OpenAI-Beta", "assistants=v1");



            HttpResponseMessage response = await httpClient.SendAsync(request);

            string responseContent = await response.Content.ReadAsStringAsync();

            var temp = JsonConvert.DeserializeObject<RunData>(responseContent);


            if (temp != null)
            {

                var tempproperties = temp.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                foreach (var prop in tempproperties)
                {
                    var value = prop.GetValue(temp);

                    this.GetType().GetProperty(prop.Name).SetValue(this, value);

                }
            }
            while (true)
            {
                if (Status == "requires_action")
                {
                    var toolcalls = RequiredAction.GetToolCalls();
                    List<ToolOutPut> outputs = new List<ToolOutPut>();
                    foreach (var item in toolcalls)
                    {
                        if (_handler.ContainsKey(item.Function["name"]))
                        {
                            Dictionary<string, string> args = JsonConvert.DeserializeObject<Dictionary<string, string>>(item.Function["arguments"]);
                            string output = string.Empty;
                            try
                            {
                                output = _handler[item.Function["name"]].Invoke(args);
                            }
                            catch (Exception e)
                            {
                                output = e.Message;
                            }

                            ToolOutPut toolOutPut = new ToolOutPut() { ToolId = item.Id, Output = output };
                            outputs.Add(toolOutPut);
                        }
                    }
                    await SubmitToolOutPutsAsync(threadid, outputs);
                }

                else if (Status == "queued" || Status == "in_progress" || Status == "cancelling" || Status == "queued")
                {
                    var httpClient1 = HttpClientFactory.GetHttpClient();
                    var request1 = new HttpRequestMessage(HttpMethod.Get, $"{_url}/{threadid}/runs/{Id}");
                    // 添加请求头
                    request1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authorization);

                    request1.Headers.Add("OpenAI-Beta", "assistants=v1");



                    HttpResponseMessage response1 = await httpClient.SendAsync(request1);

                    string responseContent1 = await response1.Content.ReadAsStringAsync();

                    var temp1 = JsonConvert.DeserializeObject<RunData>(responseContent1);


                    if (temp1 != null)
                    {

                        var tempproperties = temp1.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                        foreach (var prop in tempproperties)
                        {
                            var value = prop.GetValue(temp1);

                            this.GetType().GetProperty(prop.Name).SetValue(this, value);

                        }
                    }
                }
                else
                {
                    break;
                }
                System.Threading.Thread.Sleep(1000);
            }


            return this;
        }


        private async Task<Run> SubmitToolOutPutsAsync(string threadid, List<ToolOutPut> outputs)
        {
            var httpClient = HttpClientFactory.GetHttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/{threadid}/runs/{Id}/submit_tool_outputs");
            // 添加请求头
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authorization);

            request.Headers.Add("OpenAI-Beta", "assistants=v1");


            var obj = new { tool_outputs = outputs };
            string requestbody = JsonConvert.SerializeObject(obj);

            request.Content = new StringContent(requestbody, Encoding.UTF8, "application/json");


            HttpResponseMessage response = await httpClient.SendAsync(request);


            string responseContent = await response.Content.ReadAsStringAsync();

            var temp = JsonConvert.DeserializeObject<RunData>(responseContent);

            if (temp != null)
            {

                var tempproperties = temp.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                foreach (var prop in tempproperties)
                {
                    var value = prop.GetValue(temp);

                    this.GetType().GetProperty(prop.Name).SetValue(this, value);

                }
            }


            return this;
        }
    }


    public class ToolOutPut
    {

        [JsonProperty("tool_call_id")]
        public string ToolId { get; set; }

        [JsonProperty("output")]
        public string Output { get; set; }



    }

    public class RunError
    {
        [JsonProperty("code")]
        public string Code { get; set; }
        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class RunUsage
    {

        public int prompt_tokens { get; set; }
        public int completion_tokens { get; set; }
        public int total_tokens { get; set; }
    }

    public class RequiredAction
    {
        [JsonProperty("type")]
        public string Type
        { get; set; }

        [JsonProperty("submit_tool_outputs")]
        public SubmitToolOutput SubmitToolOutputs { get; set; }

        public List<ToolCalls> GetToolCalls() { return SubmitToolOutputs?.ToolCalls; }
    }

    public class SubmitToolOutput
    {
        [JsonProperty("tool_calls")]
        public List<ToolCalls> ToolCalls { get; set; }
    }

    public class ToolCalls
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("function")]
        public Dictionary<string, string> Function { get; set; }
    }


    public class HttpClientFactory
    {
        private HttpClient _httpClient;

        private static object obj = new object();

        private static HttpClientFactory _factory;

        private HttpClientFactory()
        {
            var handler = new HttpClientHandler
            {
                MaxConnectionsPerServer = 100
            };

            _httpClient = new HttpClient(handler);
        }

        public static HttpClient GetHttpClient()
        {
            if (_factory == null)
            {
                lock (obj)
                {
                    if (_factory == null)
                    {
                        _factory = new HttpClientFactory();
                    }
                }

            }
            return _factory._httpClient;
        }
    }

    public interface IOpenAITool
    {
        public string Type { get; }

        public JObject ToJson();

    }

    public class OpenAICodeInterpreter : IOpenAITool
    {
        private string _type;
        public string Type { get => _type; private set => _type = "code_interpreter"; }

        public JObject ToJson()
        {
            JObject obj = new JObject();
            obj["type"] = _type;
            return obj;
        }

    }

    public class OpenAIRetrieval : IOpenAITool
    {
        private string _type;
        public string Type { get => _type; private set => _type = "retrieval"; }

        public JObject ToJson()
        {
            JObject obj = new JObject();
            obj["type"] = _type;
            return obj;
        }
    }


    public class OpenAIFunction : IOpenAITool
    {
        private string _type;
        public string Type { get => _type; private set => _type = "function"; }

        public string Name { get; set; }

        public string Description { get; set; }

        public List<OpenAIFunctionProperty> Properties { get; set; } = new List<OpenAIFunctionProperty>();


        public JObject ToJson()
        {
            JObject obj = new JObject();
            obj["type"] = "function";
            JObject jfun = new JObject();
            JObject jpara = new JObject();
            jfun["name"] = Name;
            jfun["description"] = Description;
            jfun["parameters"] = jpara;
            jpara["type"] = "object";

            if (Properties.Count > 0)
            {
                JArray jrequired = new JArray();
                JObject jproperties = new JObject();
                jpara["parameters"] = jproperties;

                foreach (var property in Properties)
                {
                    JObject jproperty = new JObject();
                    jproperty["type"] = property.Type;
                    jproperty["description"] = property.Description;
                    if (property.Enum.Count > 0)
                    {
                        JArray jenum = new JArray();
                        jproperty["enum"] = jenum;
                        foreach (var enumitem in property.Enum)
                        {
                            jenum.Add(enumitem);
                        }
                    }
                    if (property.IsRequired)
                    {
                        jrequired.Add(property.Name);
                    }
                    jproperties[property.Name] = jproperty;
                }
            }

            obj["function"] = jfun;


            return obj;
        }
    }

    public class OpenAIFunctionProperty
    {
        public string Name { get; set; }
        public string Type { get; set; }

        public List<string> Enum { get; set; } = new List<string>();
        public string Description { get; set; }

        public bool IsRequired { get; set; }
    }


    public class OpenAIFactory
    {
        private static Dictionary<string, OpenAI> dict = new Dictionary<string, OpenAI>();

        private static object obj = new object();

        public static void Remove(string id)
        {
            lock (obj)
            {
                if (dict.ContainsKey(id))
                {
                    dict.Remove(id);
                }
            }
        }

        public static bool Validate(string id)
        {
            return dict.ContainsKey(id);
        }
        public static OpenAI GetInstance(string id)
        {
            if (!dict.ContainsKey(id))
            {
                lock (obj)
                {
                    if (!dict.ContainsKey(id))
                    {
                        var openai = new OpenAI("");
                        dict.Add(id, openai);
                        return openai;
                    }
                }
            }
            return dict[id];



        }
        private OpenAIFactory()
        {

        }
    }


    public class OpenAI
    {
        public string APIUrl
        {
            get; private set;
        }

        public string APIKey { get; private set; }

        public OpenAI(string key, string url = "https://api.openai.com")
        {
            APIKey = key;
            APIUrl = url;
            Assistant = new Assistant($"{key}", $"{APIUrl}/v1/assistants");

            Thread = new Thread($"{key}", $"{APIUrl}/v1/threads");

            Embedding = new Embedding($"{key}", $"{APIUrl}/v1/embeddings");

        }

        public Thread Thread
        { get; private set; }


        public Assistant Assistant
        {
            get; private set;
        }

        public Embedding Embedding
        {
            get; private set;
        }

    }

    public class Embedding
    {

        private readonly string _authorization;
        private readonly string _url;


        internal Embedding(string authorization, string url)
        {
            _authorization = authorization;
            _url = url;
        }
        public async Task<float[]> CreateAsync(string text, string model, string encoding_format = "float")
        {
            var httpClient = HttpClientFactory.GetHttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, _url);

            // 添加请求头
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authorization);

            var requestBody = new { input = text, model = model, encoding_format = encoding_format };




            request.Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");


            HttpResponseMessage response = await httpClient.SendAsync(request);


            string responseContent = await response.Content.ReadAsStringAsync();

            var result = JsonConvert.DeserializeObject(responseContent) as JObject;

            float[] vecoter = ((JArray)result.GetValue("embedding")).Select(s => s.Value<float>()).ToArray();
            return vecoter;
        }
    }
}
