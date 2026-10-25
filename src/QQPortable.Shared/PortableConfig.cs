using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace QQPortable.Shared
{
    /// <summary>
    /// 便携根目录 QQPortable.json 的唯一读取入口。
    /// 保留文件中未知字段，设置程序和插件只更新 autoStart。
    /// </summary>
    internal sealed class PortableConfig
    {
        public const string FileName = "QQPortable.json";

        private readonly Dictionary<string, object> values;

        private PortableConfig(string path, Dictionary<string, object> values)
        {
            FilePath = path;
            this.values = values;
        }

        public string FilePath { get; }

        public bool AutoStart
        {
            get
            {
                if (!values.TryGetValue("autoStart", out object value))
                {
                    return false;
                }
                if (value is bool flag)
                {
                    return flag;
                }
                throw new InvalidDataException("autoStart 必须是 true 或 false：" + FilePath);
            }
            set
            {
                values["autoStart"] = value;
            }
        }

        public static PortableConfig LoadOrCreate(string root)
        {
            string path = Path.Combine(Path.GetFullPath(root), FileName);
            if (!File.Exists(path))
            {
                Dictionary<string, object> defaults = new Dictionary<string, object>
                {
                    ["autoStart"] = false,
                };
                WriteFile(path, defaults);
                return new PortableConfig(path, defaults);
            }

            string text = File.ReadAllText(path, Encoding.UTF8);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> parsed;
            try
            {
                parsed = serializer.Deserialize<Dictionary<string, object>>(text);
            }
            catch (Exception error)
            {
                throw new InvalidDataException("配置文件不是有效的 JSON：" + path, error);
            }
            if (parsed == null)
            {
                throw new InvalidDataException("配置文件内容为空：" + path);
            }
            return new PortableConfig(path, parsed);
        }

        public void Save()
        {
            WriteFile(FilePath, values);
        }

        private static void WriteFile(string path, Dictionary<string, object> values)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, JsonText.Format(values), new UTF8Encoding(false));
            try
            {
                if (File.Exists(path))
                {
                    File.Replace(temporary, path, null);
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            catch
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
                throw;
            }
        }
    }

    /// <summary>
    /// JavaScriptSerializer 不提供缩进输出；配置文件需要保持可直接阅读的格式。
    /// </summary>
    internal static class JsonText
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static string Format(object value)
        {
            StringBuilder builder = new StringBuilder();
            Write(builder, value, 0);
            builder.Append('\n');
            return builder.ToString();
        }

        private static void Write(StringBuilder builder, object value, int depth)
        {
            if (value is Dictionary<string, object> map)
            {
                if (map.Count == 0)
                {
                    builder.Append("{}");
                    return;
                }
                builder.Append("{\n");
                bool first = true;
                foreach (KeyValuePair<string, object> pair in map)
                {
                    if (!first) builder.Append(",\n");
                    first = false;
                    Indent(builder, depth + 1);
                    builder.Append(Serializer.Serialize(pair.Key));
                    builder.Append(": ");
                    Write(builder, pair.Value, depth + 1);
                }
                builder.Append('\n');
                Indent(builder, depth);
                builder.Append('}');
                return;
            }

            if (value is object[] array)
            {
                if (array.Length == 0)
                {
                    builder.Append("[]");
                    return;
                }
                builder.Append("[\n");
                for (int index = 0; index < array.Length; index++)
                {
                    if (index > 0) builder.Append(",\n");
                    Indent(builder, depth + 1);
                    Write(builder, array[index], depth + 1);
                }
                builder.Append('\n');
                Indent(builder, depth);
                builder.Append(']');
                return;
            }

            if (value == null)
            {
                builder.Append("null");
                return;
            }

            if (value is bool flag)
            {
                builder.Append(flag ? "true" : "false");
                return;
            }

            if (value is string text)
            {
                builder.Append(Serializer.Serialize(text));
                return;
            }

            if (value is int || value is long || value is double || value is decimal)
            {
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            builder.Append(Serializer.Serialize(value));
        }

        private static void Indent(StringBuilder builder, int depth)
        {
            builder.Append(' ', depth * 2);
        }
    }
}
