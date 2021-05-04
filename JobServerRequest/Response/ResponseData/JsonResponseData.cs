using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobServer.Request
{
    /// <summary>
    /// A simple <see cref="IResponseData"/> Implementation, that allows to serialize the Response Data into JSON
    /// </summary>
    public class JsonResponseData : IResponseData
    {
        protected Dictionary<string, object> Data { get; set; }

        public void AddValue(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                return;

            this.Data.Add(key, value);
        }

        public void AddValue(string key, string[] value)
        {
            if (string.IsNullOrWhiteSpace(key) || value == null)
                return;

            this.Data.Add(key, value);
        }

        public void AddValue(string key, Dictionary<string, string> value)
        {
            if (string.IsNullOrWhiteSpace(key) || value == null)
                return;

            this.Data.Add(key, value);
        }

        public JsonResponseData()
        {
            this.Data = new Dictionary<string, object>();
        }

        public string Serialize()
        {
            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions();
            jsonSerializerOptions.Converters.Add(new JsonResponseDataConverter());
            try
            {
                string json = JsonSerializer.Serialize(this, typeof(JsonResponseData), jsonSerializerOptions);
                return json;
            }
            catch (Exception ex)
            {
                return $"Error Serializing Response Data: {ex.Message}";
            }
        }

        public class JsonResponseDataConverter : JsonConverter<JsonResponseData>
        {
            public override JsonResponseData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                JsonResponseData jsonResponseData = new JsonResponseData();
                while(reader.Read())
                {
                    if(reader.TokenType == JsonTokenType.EndObject)
                    {
                        return jsonResponseData;
                    }

                    if(reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string property = reader.GetString();
                        reader.Read();
                        switch(reader.TokenType)
                        {
                            case JsonTokenType.StartArray: // We have an array
                                jsonResponseData.Data.Add(property, JsonSerializer.Deserialize<string[]>(ref reader));
                                break;
                            case JsonTokenType.String:
                                jsonResponseData.Data.Add(property, reader.GetString());
                                break;
                            case JsonTokenType.StartObject:
                                jsonResponseData.Data.Add(property, JsonSerializer.Deserialize<Dictionary<string,string>>(ref reader));
                                break;
                        }
                    }
                }


                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, JsonResponseData value, JsonSerializerOptions options)
            {
                JsonResponseData jsonResponseData = value;
                if(value == null)
                {
                    return;
                }

                writer.WriteStartObject();
                foreach(string key in jsonResponseData.Data.Keys)
                {
                    writer.WritePropertyName(key);
                    if(jsonResponseData.Data[key] is string)
                    {
                        writer.WriteStringValue(jsonResponseData.Data[key].ToString());
                    }
                    else if(jsonResponseData.Data[key] is string[])
                    {
                        JsonSerializer.Serialize(writer, jsonResponseData.Data[key], typeof(string[]));
                    }
                    else if(jsonResponseData.Data[key] is Dictionary<string, string>)
                    {
                        JsonSerializer.Serialize(writer, jsonResponseData.Data[key], typeof(Dictionary<string,string>));
                    }
                }
                writer.WriteEndObject();
            }
        }
    }
}
